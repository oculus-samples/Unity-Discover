using System;
using System.Collections.Generic;

using Oculus.Avatar2;

using Unity.Collections;

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;

namespace Oculus.Skinning.GpuSkinning
{
    public class OvrGpuMorphTargetsCombiner
    {
        private const string logScope = "OvrGpuMorphTargetsCombiner";
        // Declare the delegate (if using non-generic pattern).
        public delegate void ArrayGrowthEventHandler(OvrGpuMorphTargetsCombiner sender, RenderTexture newArray);

        public string name => _outputTex.name;
        public int Width => _outputTex.width;
        public int Height => _outputTex.height;

        public GraphicsFormat OutputTexFormat { get; }

        // Declare the event.
        public event ArrayGrowthEventHandler ArrayResized;

        private const int NUM_INITIAL_SLICES = 1;

        public OvrGpuMorphTargetsCombiner(
            string name,
            int width,        // The width is equivalent to the number of verts in the morph target, plus one.
            int height,       // The height is based on the number of attributes, 2 for pos/norm, and 3 for pos/norm/tan
            GraphicsFormat texFormat,  // float, half, or "SNorm10", but since morph targets deal in a LARGE number of SMALL displacements, "SNorm10" is preferred
            OvrExpandableTextureArray morphTargetsSource,
            Vector3 positionRange,
            Vector3 normalRange,
            Vector3 tangentRange,
            bool hasTangents,
            bool useSnorm10,
            Shader combineMorphTargetsShader)
        {
            OutputTexFormat = texFormat;

            _combineMorphTargetsShader = combineMorphTargetsShader;

            GetRenderTexDescriptor(width, height, NUM_INITIAL_SLICES, texFormat, out var description);
            _outputTex = new RenderTexture(description);
            _outputTex.name = name;

            ConfigureRenderTexture(_outputTex);

            _atlasPackerId = CAPI.OvrGpuSkinning_AtlasPackerCreate(
                (uint)width,
                (uint)height,
                CAPI.AtlasPackerPackingAlgortihm.Runtime);

            _morphTargetsSourceTex = morphTargetsSource;

            _ranges[0] = positionRange;
            _ranges[1] = normalRange;
            _ranges[2] = tangentRange;
            _hasTangents = hasTangents;
            _useSNorm10 = useSnorm10;

            _drawCallsForSlice.Add(new List<OvrGpuCombinerDrawCall>(1));

            // Compute unaffected verts tex coord
            AddUnaffectedVertexBlock();
        }

        public void Destroy()
        {
            if (_outputTex != null)
            {
                _outputTex.Release();
            }

            foreach (List<OvrGpuCombinerDrawCall> drawCallsList in _drawCallsForSlice)
            {
                foreach (OvrGpuCombinerDrawCall drawCall in drawCallsList)
                {
                    drawCall.Destroy();
                }
            }

            CAPI.OvrGpuSkinning_AtlasPackerDestroy(_atlasPackerId);
        }

        internal IntPtr GetMorphBuffer(OvrSkinningTypes.Handle handle)
        {
            return _handleToBlockData.TryGetValue(handle, out var blockData)
                ? blockData.combinerDrawCall.GetMorphWeightsBuffer(blockData.handleInDrawCall)
                : default;
        }

        internal bool FinishMorphUpdate(OvrSkinningTypes.Handle handle)
        {
            var hasBlock = _handleToBlockData.TryGetValue(handle, out var blockData);
            if (hasBlock)
            {
                bool morphWeightsUpdated = blockData.combinerDrawCall.MorphWeightsBufferUpdateComplete(blockData.handleInDrawCall);
                if (morphWeightsUpdated && parentController != null)
                {
                    parentController.AddActiveCombiner(this);
                }
            }
            return hasBlock;
        }

        // The shapesRect is specified in texels
        public OvrSkinningTypes.Handle AddMorphTargetBlock(
            in RectInt texelRectInSource,
            in Vector2Int outputSize,
            int sourceTexSlice,
            int numMorphTargets)
        {
            // TODO* Call out to atlas packer to get packing rectangle, but, for now
            // assume a rectangle
            if (!_morphTargetsSourceTex.CheckFit(texelRectInSource.size))
            {
                OvrAvatarLog.LogError($"Source texel dimensions ({texelRectInSource}) are too large for input texture " +
                    $"{_morphTargetsSourceTex.name} ({_morphTargetsSourceTex.Width}, {_morphTargetsSourceTex.Height})");
                return OvrSkinningTypes.Handle.kInvalidHandle;
            }

            if (!_outputTex.CheckFit(in outputSize))
            {
                OvrAvatarLog.LogError($"Output texel dimensions ({outputSize}) are too large for output texture " +
                    $"{_outputTex.name} ({_outputTex.width}, {_outputTex.height})");
                return OvrSkinningTypes.Handle.kInvalidHandle;
            }

            // TODO: These don't match due to expectedSize not including the "empty texel" row
            // TODO: Probably would be better to add 1 to width instead of adding an entire row for 1 texel?
            //var expectedSize = new Vector2Int(texelRectInSource.width, texelRectInSource.height / numMorphTargets);
            //if (expectedSize != outputSize)
            //{
            //    OvrAvatarLog.LogError($"Unexpected output size, got {outputSize} but expected {expectedSize}");
            //    return OvrSkinningTypes.Handle.kInvalidHandle;
            //}

            OvrSkinningTypes.Handle packerHandle = CAPI.OvrGpuSkinning_AtlasPackerAddBlock(
                _atlasPackerId, (uint)outputSize.x, (uint)outputSize.y);

            if (!packerHandle.IsValid())
            {
                OvrAvatarLog.LogError("Invalid packing handle returned by ovrGpuSkinning_AtlasPackerAddBlock");
                return OvrSkinningTypes.Handle.kInvalidHandle;
            }

            var packResult = CAPI.OvrGpuSkinning_AtlasPackerResultsForBlock(_atlasPackerId, packerHandle);

            // See if tex array needs to grow
            // Tex slice is a 0 based index, where num tex array slices isn't
            if (packResult.texSlice >= _outputTex.volumeDepth)
            {
                GrowRenderTexture((int)(packResult.texSlice + 1));
            }

            OvrGpuCombinerDrawCall drawCallThatCanFit = GetDrawCallThatCanFit((int)packResult.texSlice, numMorphTargets);

            var drawCallHandle = drawCallThatCanFit.AddMorphTargetsToMesh(
                texelRectInSource,
                sourceTexSlice,
                _morphTargetsSourceTex.Width,
                _morphTargetsSourceTex.Height,
                new RectInt(packResult.x, packResult.y, packResult.w, packResult.h),
                _outputTex.width,
                _outputTex.height,
                numMorphTargets);

            _handleToBlockData[packerHandle] = new BlockData
            {
                combinerDrawCall = drawCallThatCanFit,
                handleInDrawCall = drawCallHandle,
            };

            return packerHandle;
        }

        public void RemoveMorphTargetBlock(OvrSkinningTypes.Handle handle)
        {
            if (_handleToBlockData.TryGetValue(handle, out BlockData dataForThisBlock))
            {
                dataForThisBlock.combinerDrawCall.RemoveMorphTargetBlock(dataForThisBlock.handleInDrawCall);
                _handleToBlockData.Remove(handle);
            }
        }

        public CAPI.ovrTextureLayoutResult GetLayoutInCombinedTex(OvrSkinningTypes.Handle handle)
        {
            return CAPI.OvrGpuSkinning_AtlasPackerResultsForBlock(_atlasPackerId, handle);
        }

        public Vector3 GetTexCoordForUnaffectedVertices()
        {
            return _unaffectedVertsTexCoords;
        }

        public void CombineMorphTargetWithCurrentWeights()
        {
            Profiler.BeginSample("OvrGpuMorphTargetsCombiner::CombineMorphTargetWithCurrentWeights");

            Profiler.BeginSample("OvrGpuMorphTargetsCombiner.IterateSlices");
            // Call out to draw calls to do combining
            bool prevsRGB = GL.sRGBWrite;
            RenderTexture oldRT = RenderTexture.active;

            // For combining morph targets, don't care about previous render texture contents,
            // so we can discard and clear here, but in practice this was found to be unneccesary
            //_outputTex.DiscardContents();

            GL.sRGBWrite = false;

            int texSlice = 0;
            foreach (List<OvrGpuCombinerDrawCall> drawCallsList in _drawCallsForSlice)
            {
                Profiler.BeginSample("OvrGpuMorphTargetsCombiner.SubmitDrawCall");
                {
                    Graphics.SetRenderTarget(_outputTex, MIP_LEVEL, CubemapFace.Unknown, texSlice);
                    GL.Clear(false, true, Color.black); // no need to clear the depth buffer

                    foreach (var drawCall in drawCallsList)
                    {
                        drawCall.ForceDraw();
                    }
                }
                texSlice++;
                Profiler.EndSample(); // "OvrGpuMorphTargetsCombiner.SubmitDrawCall"
            }

            GL.sRGBWrite = prevsRGB;
            RenderTexture.active = oldRT;

            _outputTex.IncrementUpdateCount();
            Profiler.EndSample(); // "OvrGpuMorphTargetsCombiner.IterateSlices"
            Profiler.EndSample(); // "OvrGpuMorphTargetsCombiner::CombineMorphTargetWithCurrentWeights"
        }

        public RenderTexture GetCombinedShapesTexArray()
        {
            return _outputTex;
        }

        // Note this function is used only to grow the texture by slices, not in the height and width directions (set in constructor).
        // See comments in the constructor for how those are calculated.
        private void GrowRenderTexture(int newNumSlices)
        {
            Debug.Assert(newNumSlices > _outputTex.volumeDepth);

            var descriptor = _outputTex.descriptor;
            descriptor.volumeDepth = newNumSlices;

            var newRt = new RenderTexture(descriptor);
            newRt.name = _outputTex.name;

            ConfigureRenderTexture(newRt);

            var oldTex = _outputTex;

            for (int slice = 0; slice < oldTex.volumeDepth; slice++)
            {
                Graphics.CopyTexture(
                    oldTex,    // src
                    slice,   // srcElement
                    MIP_LEVEL,  // srcMip
                    newRt,  // dst
                    slice,  // dstElement
                    MIP_LEVEL   // dstMip
                );
            }
            for (int i = oldTex.volumeDepth; i < newNumSlices; i++)
            {
                Graphics.SetRenderTarget(newRt, MIP_LEVEL, CubemapFace.Unknown, i);
                GL.Clear(true, true, Color.black);
            }

            _outputTex = newRt;
            oldTex.Release();

            // Expand draw call list
            for (int i = _drawCallsForSlice.Count; i < newNumSlices; i++)
            {
                _drawCallsForSlice.Add(new List<OvrGpuCombinerDrawCall>(1));
            }

            // Inform listeners
            ArrayResized?.Invoke(this, _outputTex);
        }

        private OvrGpuCombinerDrawCall GetDrawCallThatCanFit(int texSlice, int numMorphTargets)
        {
            List<OvrGpuCombinerDrawCall> drawCallsList = _drawCallsForSlice[texSlice];

            foreach (OvrGpuCombinerDrawCall drawCall in drawCallsList)
            {
                if (drawCall.CanFit(numMorphTargets))
                {
                    return drawCall;
                }
            }

            // No existing draw call can fit, make a new one
            OvrGpuCombinerDrawCall newDrawCall = new OvrGpuCombinerDrawCall(_combineMorphTargetsShader, _morphTargetsSourceTex, _ranges, _hasTangents, _useSNorm10);
            drawCallsList.Add(newDrawCall);
            return newDrawCall;
        }

        private const uint UnaffectedVertexBlockWidth = 1;
        private const uint UnaffectedVertexBlockHeight = 1;

        private void AddUnaffectedVertexBlock()
        {
            // Pack a single texel
            OvrSkinningTypes.Handle handle = CAPI.OvrGpuSkinning_AtlasPackerAddBlock(_atlasPackerId,
                UnaffectedVertexBlockWidth,
                UnaffectedVertexBlockHeight);
            Debug.Assert(handle.IsValid());

            // Expect this to always fit (should be called in constructor)
            var packResult = CAPI.OvrGpuSkinning_AtlasPackerResultsForBlock(_atlasPackerId, handle);

            // Store off texture coordinate (at texel center)
            _unaffectedVertsTexCoords = new Vector3(
                (2.0f * packResult.x + 1.0f) / (2.0f * _outputTex.width),
                (2.0f * packResult.y + 1.0f) / (2.0f * _outputTex.height),
                packResult.texSlice);
        }

        private const int MIP_COUNT = 0;
        private const int MIP_LEVEL = 0;
        private const int DEPTH_BITS = 0; // no depth

        private readonly Shader _combineMorphTargetsShader;
        private RenderTexture _outputTex;
        private Vector3 _unaffectedVertsTexCoords;

        private readonly OvrExpandableTextureArray _morphTargetsSourceTex;
        private readonly Vector4[] _ranges = new Vector4[3];
        private readonly bool _hasTangents;
        private readonly bool _useSNorm10;

        private readonly List<List<OvrGpuCombinerDrawCall>> _drawCallsForSlice
            = new List<List<OvrGpuCombinerDrawCall>>(1);

        public OvrAvatarGpuSkinningController parentController = null;

        private struct BlockData
        {
            public OvrSkinningTypes.Handle handleInDrawCall;
            public OvrGpuCombinerDrawCall combinerDrawCall;
        }

        private readonly Dictionary<OvrSkinningTypes.Handle, BlockData> _handleToBlockData
            = new Dictionary<OvrSkinningTypes.Handle, BlockData>();
        private readonly CAPI.AtlasPackerId _atlasPackerId;


        private static void GetRenderTexDescriptor(int width, int height, int slices, GraphicsFormat format, out RenderTextureDescriptor description)
        {
#if UNITY_2019_OR_NEWER
            description = new RenderTextureDescriptor(width, height, format, DEPTH_BITS, MIP_COUNT);

            description.stencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
            description.useDynamicScale = false;
#else
            // TODO: Loss of info in GraphicsFormat->RenderTextureFormat conversion
            description = new RenderTextureDescriptor(width, height, format.GetRenderTextureFormat(), DEPTH_BITS);
#endif

            description.sRGB = false;
            description.msaaSamples = 1;
            description.useMipMap = false;
            description.autoGenerateMips = false;
            description.vrUsage = VRTextureUsage.None;
            description.shadowSamplingMode = UnityEngine.Rendering.ShadowSamplingMode.None;
            description.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
            description.volumeDepth = slices;
        }
        private static void ConfigureRenderTexture(RenderTexture renderTexture)
        {
            renderTexture.useDynamicScale = false;
            renderTexture.filterMode = FilterMode.Point;
            renderTexture.anisoLevel = 0;
            renderTexture.wrapMode = TextureWrapMode.Clamp;

            var didCreate = renderTexture.IsCreated() || renderTexture.Create();
            Debug.Assert(didCreate);
        }
    }
}
