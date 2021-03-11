/* DataManager.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 */

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public class DataImpressionGroup : IHasDataset
    {
        /// <summary>
        ///     Room-scale (Unity rendering space) bounds that all data should
        ///     be contained within
        /// </summary>
        public Bounds GroupContainer { get; }

        /// <summary>
        ///     Transformation from the original data space into the room-scale
        ///     bounds. Multiply by a vector to go from group-space into data-space.
        /// </summary>
        public Matrix4x4 GroupToDataMatrix;

        /// <summary>
        ///     The actual bounds (contained within DataContainer) of the
        ///     group-scale dataset
        /// </summary>
        public Bounds GroupBounds;

        /// <summary>
        ///     GameObject to place all Data Impressions under
        /// </summary>
        public GameObject GroupRoot { get; }

        /// <summary>
        ///     Human-readable name for the data impression group
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Unique identifier for this group
        /// </summary>
        public Guid Uuid { get; }


        private Dictionary<Guid, IDataImpression> _impressions = new Dictionary<Guid, IDataImpression>();
        private Dictionary<Guid, EncodedGameObject> gameObjectMapping = new Dictionary<Guid, EncodedGameObject>();

        public DataImpressionGroup(string name, Bounds bounds, Transform parent)
            : this(name, Guid.NewGuid(), bounds, Vector3.zero, Quaternion.identity, parent) { }

        public DataImpressionGroup(string name, Guid uuid, Bounds bounds, Vector3 position, Quaternion rotation, Transform parent)
        {
            Uuid = uuid;
            Name = name;

            GroupContainer = bounds;

            GroupRoot = new GameObject("DataImpressionGroup " + name);
            GroupRoot.transform.SetParent(parent, false);
            GroupRoot.transform.localPosition = position;
            GroupRoot.transform.localRotation = rotation;

            ResetBoundsAndTransformation();
            Debug.LogFormat("Added DataImpressionGroup {0}", name);
        }

        public void AddDataImpression(IDataImpression impression, bool allowOverwrite = true)
        {
            // Make sure the new impression matches the rest of the impressions'
            // datasets. ImpressionsGroups MUST have only one dataset.
            Dataset ds = GetDataset();
            Dataset impressionDs = impression.GetDataset();
            if (impressionDs != null && ds != null && ds?.Path != impressionDs?.Path)
            {
                Debug.LogErrorFormat("Refusing to add DataImpression with a different dataset than this DataImpressionGroup's dataset:\nExpected: {0}\nGot: {1}", ds?.Path, impressionDs?.Path);
                return;
            }

            if (HasDataImpression(impression.Uuid))
            {
                if (allowOverwrite)
                {
                    _impressions[impression.Uuid] = impression;
                }
                else
                {
                    Debug.LogWarningFormat("Skipping register data impression (already exists): {0}", impression.Uuid);
                    return;
                }
            }
            else
            {
                _impressions[impression.Uuid] = impression;
                GameObject impressionGameObject = new GameObject();
                impressionGameObject.transform.parent = GroupRoot.transform;
                impressionGameObject.name = impression.GetType().ToString();

                EncodedGameObject ego = impressionGameObject.AddComponent<EncodedGameObject>();
                gameObjectMapping[impression.Uuid] = ego;
            }

            PrepareImpression(impression);
        }

        /// <summary>
        ///     Remove data impression, returning if this data impression group is
        ///     empty after the removal of such impression.
        /// </summary>
        public bool RemoveDataImpression(Guid uuid)
        {
            if (_impressions.ContainsKey(uuid))
            {
                _impressions.Remove(uuid);
                GameObject.Destroy(gameObjectMapping[uuid].gameObject);
                gameObjectMapping.Remove(uuid);
            }
            return _impressions.Count == 0;
        }

        public IDataImpression GetDataImpression(Guid uuid)
        {
            IDataImpression dataImpression = null;
            _impressions.TryGetValue(uuid, out dataImpression);
            return dataImpression;
        }

        public List<T> GetDataImpressionsOfType<T>()
        where T : IDataImpression
        {
            return _impressions
                .Select((kv) => kv.Value)
                .Where((imp) => imp.GetType().IsAssignableFrom(typeof(T)))
                .Select((imp) => (T) imp).ToList();
        }

        public bool HasDataImpression(Guid uuid)
        {
            return _impressions.ContainsKey(uuid);
        }

        public EncodedGameObject GetEncodedGameObject(Guid uuid)
        {
            EncodedGameObject dataImpression = null;
            gameObjectMapping.TryGetValue(uuid, out dataImpression);
            return dataImpression;
        }

        public bool HasEncodedGameObject(Guid uuid)
        {
            return gameObjectMapping.ContainsKey(uuid);
        }

        public Dictionary<Guid, IDataImpression> GetDataImpressions()
        {
            return _impressions;
        }


        public void Clear()
        {
            List<Guid> toRemove = _impressions.Keys.ToList();
            foreach (var impressionUuid in toRemove)
            {
                RemoveDataImpression(impressionUuid);
            }
        }

        /// <summary>
        ///     Get the dataset that all impressions in this DataImpressionGroup are
        ///     associated with. All DataImpressionGroups MUST have only one dataset.
        /// </summary>
        public Dataset GetDataset()
        {
            foreach (var impression in _impressions)
            {
                Dataset impressionDs = impression.Value.GetDataset();
                // Find the first one that exists and return it
                if (impressionDs != null)
                {
                    return impressionDs;
                }
            }
            return null;
        }

        /// <summary>
        ///     From scratch, recalculate the bounds of this DataImpressionGroup. Start with
        ///     a zero-size bounding box and expand until it encapsulates all
        ///     datasets.
        /// </summary>
        public void RecalculateBounds()
        {
            ResetBoundsAndTransformation();

            Dataset ds = GetDataset();
            if (ds != null)
            {
                foreach (var keyData in ds.GetAllKeyData())
                {
                    RawDataset rawDataset;
                    ABREngine.Instance.Data.TryGetRawDataset(keyData.Value.Path, out rawDataset);
                    Bounds originalBounds = rawDataset.bounds;

                    if (ds.DataSpaceBounds.size.magnitude <= float.Epsilon)
                    {
                        // If the size is zero (first keyData), then start with its
                        // bounds (make sure to not assume we're including (0, 0, 0) in
                        // the bounds)
                        ds.DataSpaceBounds = originalBounds;
                        NormalizeWithinBounds.Normalize(GroupContainer, originalBounds, out GroupToDataMatrix, out GroupBounds);
                    }
                    else
                    {
                        NormalizeWithinBounds.NormalizeAndExpand(
                            GroupContainer,
                            originalBounds,
                            ref GroupBounds,
                            ref GroupToDataMatrix,
                            ref ds.DataSpaceBounds
                        );
                    }
                }
            }
        }

        [FunctionDebugger]
        public void RenderImpressions()
        {
            try
            {
                foreach (var impression in _impressions)
                {
                    PrepareImpression(impression.Value);
                }

                foreach (var impression in _impressions)
                {
                    impression.Value.ComputeKeyDataRenderInfo();
                }

                foreach (var impression in _impressions)
                {
                    impression.Value.ComputeRenderInfo();
                }

                foreach (var impression in _impressions)
                {
                    Guid uuid = impression.Key;
                    impression.Value.ApplyToGameObject(gameObjectMapping[uuid]);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error while rendering impressions");
                Debug.LogError(e);
            }
        }

        private void ResetBoundsAndTransformation()
        {
            GroupToDataMatrix = Matrix4x4.identity;
            GroupBounds = new Bounds();
            Dataset ds = GetDataset();
            if (ds != null)
            {
                ds.DataSpaceBounds = new Bounds();
            }
        }

        private void PrepareImpression(IDataImpression impression)
        {
            // Make sure the bounding box is correct
            // Mostly matters if there's a live ParaView connection
            RecalculateBounds();

            // Make sure the parent is assigned properly
            gameObjectMapping[impression.Uuid].gameObject.transform.SetParent(GroupRoot.transform, false);
            
            // Unsure why this needs to be explicitly set but here it is,
            // zeroing position and rotation so each data impression encoded
            // game object is centered on the dataset...
            gameObjectMapping[impression.Uuid].gameObject.transform.localPosition = Vector3.zero;
            gameObjectMapping[impression.Uuid].gameObject.transform.localRotation = Quaternion.identity;

            // Display the UUID in editor
            gameObjectMapping[impression.Uuid].SetUuid(impression.Uuid);
        }
    }
}