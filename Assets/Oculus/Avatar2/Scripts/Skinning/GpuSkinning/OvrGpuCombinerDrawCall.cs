// Check for differences in update vs current state and ignore if they match
//#define OVR_GPUSKINNING_DIFFERENCE_CHECK

using Oculus.Avatar2;

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Profiling;

namespace Oculus.Skinning.GpuSkinning
{
    internal class OvrGpuCombinerDrawCall
    {
        private const string logScope = "OvrGpuCombinerDrawCall";
        internal OvrGpuCombinerDrawCall(
            Shader combineShader,
            OvrExpandableTextureArray morphTargetsSourceTex,
            Vector4[] ranges,
            bool hasTangents,
            bool useSNorm10)
        {
            _morphTargetsSourceTex = morphTargetsSourceTex;
            _combineMaterial = new Material(combineShader);
            if (hasTangents)
            {
                _combineMaterial.EnableKeyword(OVR_HAS_TANGENTS);
            }
            if (useSNorm10)
            {
                _combineMaterial.EnableKeyword(OVR_MORPH_10_10_10_2);
            }
            _combineMaterial.SetVectorArray(MORPH_TARGET_RANGES_PROP, ranges);

            _combineMaterial.SetBuffer(MORPH_TARGET_WEIGHTS_PROP, OvrAvatarManager.Instance.GpuSkinningController.GetWeightsBuffer());

            _mesh = new Mesh
            {
                vertices = Array.Empty<Vector3>(),
                uv = Array.Empty<Vector2>(),
                colors = Array.Empty<Color>(),
                triangles = Array.Empty<int>()
            };

            _meshLayout = new OvrFreeListBufferTracker(MAX_QUADS);
            _handleToBlockData = new Dictionary<OvrSkinningTypes.Handle, BlockData>();

            _morphTargetsSourceTex.ArrayResized += MorphTargetsSourceTexArrayResized;
        }

        private void MorphTargetsSourceTexArrayResized(object sender, Texture2DArray newArray)
        {
            SetMorphSourceTexture(newArray);
        }

        internal void Destroy()
        {
            _morphTargetsSourceTex.ArrayResized -= MorphTargetsSourceTexArrayResized;

            if (_combineMaterial)
            {
                Material.Destroy(_combineMaterial);
            }
        }

        // The shapesRect is specified in texels
        internal OvrSkinningTypes.Handle AddMorphTargetsToMesh(
            RectInt texelRectInSource,
            int sourceTexSlice,
            int sourceTexWidth,
            int sourceTexHeight,
            RectInt texelRectInOutput,
            int outputTexWidth,
            int outputTexHeight,
            int numMorphTargets)
        {
            OvrSkinningTypes.Handle layoutHandle = _meshLayout.TrackBlock(numMorphTargets);

            if (!layoutHandle.IsValid())
            {
                return layoutHandle;
            }

            OvrFreeListBufferTracker.LayoutResult quadsLayout = _meshLayout.GetLayoutInBufferForBlock(layoutHandle);

            // Create Quads if needed
            int quadIndex = quadsLayout.startIndex;
            int vertStartIndex = quadIndex * NUM_VERTS_PER_QUAD;
            int blockIndex = layoutHandle.GetValue();

            if (vertStartIndex >= _mesh.vertexCount)
            {
                OvrCombinedMorphTargetsQuads.ExpandMeshToFitQuads(_mesh, numMorphTargets);
            }

            OvrCombinedMorphTargetsQuads.UpdateQuadsInMesh(
                vertStartIndex,
                blockIndex,
                quadIndex,
                texelRectInOutput,
                outputTexWidth,
                outputTexHeight,
                texelRectInSource,
                sourceTexSlice,
                sourceTexWidth,
                sourceTexHeight,
                numMorphTargets,
                _mesh);

            // Expand compute buffers and lists if needed
            int newNumBlocks = blockIndex + 1;

            _combineMaterial.SetFloat(BLOCK_ENABLED_PROP, 1.0f);

            // Add new mapping of handle to block data
            _handleToBlockData[layoutHandle] = new BlockData
            {
                blockIndex = blockIndex,
                indexInWeightsBuffer = 0,
                numMorphTargets = numMorphTargets
            };

            return layoutHandle;
        }

        internal void RemoveMorphTargetBlock(OvrSkinningTypes.Handle handle)
        {
            _meshLayout.FreeBlock(handle);
        }

        internal IntPtr GetMorphWeightsBuffer(OvrSkinningTypes.Handle handle)
        {
            if (_handleToBlockData.TryGetValue(handle, out BlockData blockData))
            {
                Debug.Assert(blockData.indexInWeightsBuffer == 0 && blockData.numMorphTargets < OvrComputeBufferPool.MAX_WEIGHTS);
                var entry = OvrAvatarManager.Instance.GpuSkinningController.GetNextEntryWeights(blockData.numMorphTargets);
                _combineMaterial.SetInt(MORPH_TARGET_WEIGHTS_OFFSET_PROP, entry.Offset);

                return entry.Data;
            }
            return default;
        }

        internal bool MorphWeightsBufferUpdateComplete(OvrSkinningTypes.Handle handle)
        {
            bool drawUpdateNeeded = false;
            if (_handleToBlockData.TryGetValue(handle, out BlockData dataForThisBlock))
            {
                MarkBlockUpdated(in dataForThisBlock);
                drawUpdateNeeded = true;
            }
            return drawUpdateNeeded;
        }

        internal void Draw()
        {
            if (_areAnyBlocksEnabled)
            {
                ForceDraw();
            }
        }

        internal void ForceDraw()
        {
            Profiler.BeginSample("OvrGpuCombinerDrawCall::ForceDraw");

            // Don't care about matrices as the shader used should handle clip space
            // conversions without matrices (due to how quads set up)
            bool didSetPass = _combineMaterial.SetPass(0);
            Debug.Assert(didSetPass);
            Graphics.DrawMeshNow(_mesh, Matrix4x4.identity);

            _combineMaterial.SetFloat(BLOCK_ENABLED_PROP, 0.0f);

            // Reset booleans and mark all blocks as disabled for next frame
            _areAnyBlocksEnabled = false;

            Profiler.EndSample();
        }

        internal bool CanFit(int numMorphTargets)
        {
            return _meshLayout.CanFit(numMorphTargets);
        }

        private void SetMorphSourceTexture(Texture2DArray morphTargetsSourceTex)
        {
            _combineMaterial.SetTexture(MORPH_TARGETS_SOURCE_TEX_PROP, morphTargetsSourceTex);
        }

        private void MarkBlockUpdated(in BlockData block)
        {
            Debug.Assert(block.blockIndex == 0);

            _combineMaterial.SetFloat(BLOCK_ENABLED_PROP, 1.0f);
            _areAnyBlocksEnabled = true;
        }

        private readonly Mesh _mesh;
        private readonly OvrFreeListBufferTracker _meshLayout;

        private readonly Material _combineMaterial;

        private bool _areAnyBlocksEnabled = false;

        private struct BlockData
        {
            public int blockIndex;
            public int indexInWeightsBuffer;
            public int numMorphTargets;
        }

        private readonly Dictionary<OvrSkinningTypes.Handle, BlockData> _handleToBlockData;

        private readonly OvrExpandableTextureArray _morphTargetsSourceTex;

        private const int NUM_VERTS_PER_QUAD = 4;
        private const int MAX_QUADS = ushort.MaxValue / NUM_VERTS_PER_QUAD;

        private const string OVR_HAS_TANGENTS = "OVR_HAS_TANGENTS";
        private const string OVR_MORPH_10_10_10_2 = "OVR_MORPH_10_10_10_2";

        private static readonly int MORPH_TARGETS_SOURCE_TEX_PROP = Shader.PropertyToID("u_MorphTargetSourceTex");
        private static readonly int MORPH_TARGET_WEIGHTS_PROP = Shader.PropertyToID("u_Weights");
        private static readonly int MORPH_TARGET_WEIGHTS_OFFSET_PROP = Shader.PropertyToID("u_WeightOffset");
        private static readonly int BLOCK_ENABLED_PROP = Shader.PropertyToID("u_BlockEnabled");
        private static readonly int MORPH_TARGET_RANGES_PROP = Shader.PropertyToID("u_MorphTargetRanges");

    }
}
