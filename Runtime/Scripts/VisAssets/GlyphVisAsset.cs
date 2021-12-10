/* GlyphVisAsset.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace IVLab.ABREngine
{
    public interface IGlyphVisAsset : IVisAsset
    {
        /// <summary>
        /// Get the mesh at an LOD for a single glyph visasset
        /// </summary>
        Mesh GetMesh(int lod);

        /// <summary>
        /// Get the normal map at an LOD for a single glyph visasset
        /// </summary>
        Texture2D GetNormalMap(int lod);

        /// <summary>
        /// Get the mesh at a particular index of the gradient
        /// </summary>
        Mesh GetMesh(int gradientIndex, int lod);

        /// <summary>
        /// Get the normal map at a particular index of the gradient
        /// </summary>
        Texture2D GetNormalMap(int gradientIndex, int lod);

        /// <summary>
        /// Get the mesh at a particular percentage (t-value) through the gradient
        /// </summary>
        Mesh GetMesh(float gradientT, int lod);

        /// <summary>
        /// Get the normal map at a particular percentage (t-value) through the gradient
        /// </summary>
        Texture2D GetNormalMap(float gradientT, int lod);
    }

    public class GlyphVisAsset : VisAsset, IGlyphVisAsset
    {
        public int VisAssetCount { get; } = 1;
        public List<Mesh> MeshLods { get; } = new List<Mesh>();
        public List<Texture2D> NormalMapLods { get; } = new List<Texture2D>();

        public GlyphVisAsset() : this(new Guid(), null, null) { }
        public GlyphVisAsset(List<Mesh> meshLods, List<Texture2D> normalMapLods) : this(Guid.NewGuid(), meshLods, normalMapLods) { }
        public GlyphVisAsset(Guid uuid, List<Mesh> meshLods, List<Texture2D> normalMapLods)
        {
            Uuid = uuid;
            MeshLods = meshLods;
            NormalMapLods = normalMapLods;
            ImportTime = DateTime.Now;
        }

        public Mesh GetMesh(int lod) => MeshLods[lod];
        public Texture2D GetNormalMap(int lod) => NormalMapLods[lod];
        public Mesh GetMesh(int gradientIndex, int lod) => GetMesh(lod);
        public Texture2D GetNormalMap(int gradientIndex, int lod) => GetNormalMap(lod);
        public Mesh GetMesh(float gradientT, int lod) => GetMesh(lod);
        public Texture2D GetNormalMap(float gradientT, int lod) => GetNormalMap(lod);
    }

    public class GlyphGradient : VisAssetGradient, IGlyphVisAsset, IVisAssetGradient<GlyphVisAsset>
    {
        public int VisAssetCount { get => VisAssets.Count; }
        public List<GlyphVisAsset> VisAssets { get; private set; }
        public List<float> Stops { get; private set; }

        public void Initialize(Guid uuid, List<GlyphVisAsset> visAssets, List<float> stops)
        {
            Uuid = uuid;
            VisAssets = visAssets;
            Stops = stops;
        }

        public Mesh GetMesh(int lod) => GetMesh(0, lod);
        public Texture2D GetNormalMap(int lod) => GetNormalMap(0, lod);
        public Mesh GetMesh(int gradientIndex, int lod) => VisAssets[gradientIndex].GetMesh(lod);
        public Texture2D GetNormalMap(int gradientIndex, int lod) => VisAssets[gradientIndex].GetNormalMap(lod);
        public Mesh GetMesh(float gradientT, int lod)
        {
            for (int i = 0; i < Stops.Count; i++)
            {
                if (Stops[i] >= gradientT)
                {
                    return GetMesh(i + 1);
                }
            }
            return default;
        }
        public Texture2D GetNormalMap(float gradientT, int lod)
        {
            for (int i = 0; i < Stops.Count; i++)
            {
                if (Stops[i] >= gradientT)
                {
                    return GetNormalMap(i + 1);
                }
            }
            return default;
        }
    }
}

