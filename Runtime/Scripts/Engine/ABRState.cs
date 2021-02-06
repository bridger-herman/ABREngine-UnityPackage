/* ABRState.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 */

using System.Threading.Tasks;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using JsonDiffPatchDotNet;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public class ABRStateParser
    {
        private IABRStateLoader _loader;

        public static ABRStateParser GetParser<T>()
        where T : ResourceStateFileLoader
        {
            ResourceStateFileLoader loader = new ResourceStateFileLoader();
            ABRStateParser parser = new ABRStateParser();
            parser._loader = loader as T;
            return parser;
        }

        public async Task<JObject> LoadState(string name, JObject previousState)
        {
            string stateText = await UnityThreadScheduler.Instance.RunMainThreadWork(() => _loader.GetState(name));

            JObject stateJson = JObject.Parse(stateText);

            // Check the diff from the previous state
            JsonDiffPatch jdp = new JsonDiffPatch();
            JToken diffFromPrevious = jdp?.Diff(previousState, stateJson);
            JToken impressionsToken = diffFromPrevious?.SelectToken("impressions");

            RawABRState state = JsonConvert.DeserializeObject<RawABRState>(stateText);

            var assembly = Assembly.GetExecutingAssembly();
            Type dataImpressionType = typeof(DataImpression);
            List<string> impressionTypeStrings = assembly.GetTypes()
                .Where((t) => t.IsSubclassOf(dataImpressionType) && t != dataImpressionType)
                .Select((t) => t.GetCustomAttribute<ABRPlateType>()?.plateType)
                .ToList();
            List<Type> impressionTypes = assembly.GetTypes()
                .Where((t) => t.IsSubclassOf(dataImpressionType) && t != dataImpressionType)
                .Select((t) => Type.GetType(t?.FullName))
                .ToList();

            foreach (var impression in state.impressions)
            {

                if (impressionsToken != null)
                {
                    // Skip this impression if it doesn't appear in the diff
                    if (!impressionsToken.ToObject<JObject>().ContainsKey(impression.Key))
                    {
                        continue;
                    }
                }

                // Find what type of impression to create
                string plateType = impression.Value.plateType;
                int foundIndex = impressionTypeStrings.IndexOf(plateType);
                if (foundIndex < 0)
                {
                    Debug.LogWarningFormat("Plate type `{0}` does not exist in this system", plateType);
                    continue;
                }

                Queue<string> visAssetsToLoad = new Queue<string>();
                Queue<string> rawDataToLoad = new Queue<string>();
                foreach (var inputValue in impression.Value.inputValues)
                {
                    // If the input genre is a key data or a visasset, we need
                    // to load it
                    if (inputValue.Value.inputGenre == ABRInputGenre.KeyData)
                    {
                        rawDataToLoad.Enqueue(inputValue.Value.inputValue);
                    }
                    if (inputValue.Value.inputGenre == ABRInputGenre.VisAsset)
                    {
                        visAssetsToLoad.Enqueue(inputValue.Value.inputValue);
                    }
                }

                foreach (var visAsset in visAssetsToLoad)
                {
                    // See if we already have the VisAsset; if not then load it
                    var visAssetUUID = new Guid(visAsset);
                    IVisAsset existing;
                    VisAssetManager.Instance.TryGetVisAsset(visAssetUUID, out existing);
                    if (existing == null)
                    {
                        await UnityThreadScheduler.Instance.RunMainThreadWork(
                            () => VisAssetManager.Instance.LoadVisAsset(visAssetUUID)
                        );
                    }
                }

                foreach (var rawData in rawDataToLoad)
                {
                    // See if we already have the VisAsset; if not then load it
                    RawDataset existing;
                    DataManager.Instance.TryGetRawDataset(rawData, out existing);
                    if (existing == null)
                    {
                        await DataManager.Instance.LoadRawDatasetFromCache(rawData);
                    }
                }


                // Make the data impression; should only match one type
                Type impressionType = impressionTypes[foundIndex];
                ConstructorInfo impressionCtor =
                    impressionType.GetConstructor(new Type[] { typeof(string) });
                string[] impressionArgs = new string[] { impression.Value.uuid };
                IDataImpression dataImpression = impressionCtor.Invoke(impressionArgs) as IDataImpression;
                ABRInputIndexerModule impressionInputs = dataImpression.InputIndexer;

                List<ABRInputAttribute> actualInputs = impressionType.GetFields()
                    .Select((f) => f.GetCustomAttribute<ABRInputAttribute>())
                    .Where((f) => f != null).ToList();

                // Now that everything is loaded, go ahead and populate the state
                foreach (var inputValue in impression.Value.inputValues)
                {
                    var value = inputValue.Value;
                    IABRInput possibleInput = null;
                    if (value.inputGenre == ABRInputGenre.KeyData)
                    {
                        string datasetPath = DataPath.GetDatasetPath(value.inputValue);
                        Dataset dataset;
                        DataManager.Instance.TryGetDataset(datasetPath, out dataset);
                        if (dataset == null)
                        {
                            Debug.LogWarningFormat("Unable to find dataset `{0}`", datasetPath);
                            continue;
                        }
                        IKeyData keyData;
                        dataset.TryGetKeyData(value.inputValue, out keyData);
                        if (keyData == null)
                        {
                            Debug.LogWarningFormat("Unable to find Key Data `{0}`", value.inputValue);
                            continue;
                        }
                        possibleInput = keyData as IABRInput;
                    }
                    else if (value.inputGenre == ABRInputGenre.Variable)
                    {
                        string datasetPath = DataPath.GetDatasetPath(value.inputValue);
                        Dataset dataset;
                        DataManager.Instance.TryGetDataset(datasetPath, out dataset);
                        if (dataset == null)
                        {
                            Debug.LogWarningFormat("Unable to find dataset `{0}`", datasetPath);
                            continue;
                        }

                        if (DataPath.FollowsConvention(value.inputValue, DataPath.DataPathType.ScalarVar))
                        {
                            ScalarDataVariable variable;
                            dataset.TryGetScalarVar(value.inputValue, out variable);
                            possibleInput = variable as IABRInput;
                        }
                        else if (DataPath.FollowsConvention(value.inputValue, DataPath.DataPathType.ScalarVar))
                        {
                            VectorDataVariable variable;
                            dataset.TryGetVectorVar(value.inputValue, out variable);
                            possibleInput = variable as IABRInput;
                        }

                        if (possibleInput == null)
                        {
                            Debug.LogWarningFormat("Unable to find variable `{0}`", value.inputValue);
                        }
                    }
                    else if (value.inputGenre == ABRInputGenre.VisAsset)
                    {
                        IVisAsset visAsset = null;
                        await UnityThreadScheduler.Instance.RunMainThreadWork(() => 
                        {
                            VisAssetManager.Instance.TryGetVisAsset(new Guid(value.inputValue), out visAsset);
                        });
                        if (visAsset == null)
                        {
                            Debug.LogWarningFormat("Unable to find VisAsset `{0}`", value.inputValue);
                            continue;
                        }
                        possibleInput = visAsset as IABRInput;
                    }
                    else if (value.inputGenre == ABRInputGenre.Primitive)
                    {
                        // Attempt to construct the primitive from the type
                        // provided in the state file
                        Type inputType = Type.GetType(inputValue.Value.inputType);
                        ConstructorInfo inputCtor =
                            inputType.GetConstructor(
                                BindingFlags.Instance | BindingFlags.Public,
                                null,
                                CallingConventions.HasThis,
                                new Type[] { typeof(string) },
                                null
                        );
                        string[] args = new string[] { inputValue.Value.inputValue };
                        possibleInput = inputCtor?.Invoke(args) as IABRInput;
                        if (possibleInput == null)
                        {
                            Debug.LogWarningFormat("Unable to create primitive `{0}`", value.inputValue);
                        }
                    }
                    else
                    {
                        Debug.LogWarningFormat("Unsupported input genre `{0}`", value.inputGenre.ToString());
                    }

                    // Verify that the input matches with the parameter (to
                    // avoid possible name collisions), and check that it's
                    // assignable from the possibleInput
                    var actualInput = actualInputs.First((i) => inputValue.Key == i.inputName && i.parameterName == inputValue.Value.parameterName);
                    if (impressionInputs.CanAssignInput(inputValue.Key, possibleInput) && actualInput != null)
                    {
                        impressionInputs.AssignInput(inputValue.Key, possibleInput);
                    }
                }

                ABREngine.Instance.RegisterDataImpression(dataImpression);
            }

            return stateJson;
        }
    }

    /// <summary>
    ///     Raw ABR state to load from JSON. Should match the schema as much as
    ///     we need to.
    /// </summary>
    class RawABRState
    {
        public string version;
        public Dictionary<string, RawDataImpression> impressions;
    }

    class RawDataImpression
    {
        public string plateType;
        public string uuid;
        public string name;
        public RawRenderingData renderingData;
        public Dictionary<string, RawABRInput> inputValues;
    }

    class RawRenderingData { }
}