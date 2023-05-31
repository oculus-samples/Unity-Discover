using System;
using System.Collections.Generic;
using Unity.Collections;
using Oculus.Avatar2;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;

namespace Oculus.Skinning.GpuSkinning
{
    internal interface IOvrGpuJointSkinner
    {
        OvrSkinningTypes.SkinningQuality Quality { get; set; }
    }

    // Missing C++ template metaprogramming to avoiding needing
    // separate classes for this
    internal abstract class OvrGpuSkinnerBase<TDrawCallType> : IOvrGpuSkinner where TDrawCallType : IOvrGpuSkinnerDrawCall
    {
        private OvrAvatarGpuSkinningController _parentController = null;
        public override OvrAvatarGpuSkinningController ParentController {
            get { return _parentController; }
            set { _parentController = value; }
        }

        // Declare the delegate (if using non-generic pattern).
        public delegate void ArrayGrowthEventHandler(object sender, Texture newArray);

        // Declare the event.
        public event ArrayGrowthEventHandler ArrayResized;

        public int Width => _outputTex.width;
        public int Height => _outputTex.height;

        public readonly GraphicsFormat outputFormat;

        private const int INITIAL_SLICE_COUNT = 1;

        // number of "output depth texels" per "atlas packer" slice
        private readonly int _numDepthTexelsPerSlice = 1;

        protected OvrGpuSkinnerBase(
            string name,
            int width,
            int height,
            GraphicsFormat texFormat,
            FilterMode texFilterMode,
            int depthTexelsPerSlice,
            OvrExpandableTextureArray neutralPoseTexture,
            Shader skinningShader)
        {
            OvrAvatarLog.Assert(skinningShader);

            outputFormat = texFormat;

            _neutralPoseTex = neutralPoseTexture;
            _skinningShader = skinningShader;
            _numDepthTexelsPerSlice = depthTexelsPerSlice;

            if (texFormat == GraphicsFormat.R16G16B16A16_UNorm)
            {
                var scale = GpuSkinningConfiguration.Instance.SkinnerUnormScale;
                _outputScaleBias = new Vector2(1.0f / (2.0f * scale), 0.5f);
            }
            else
            {
                _outputScaleBias = new Vector2(1.0f, 0.0f);
            }
            // TODO: Use RenderTextureDescriptor
#if UNITY_2019_3_OR_NEWER
            _outputTex = new RenderTexture(width, height, 0, texFormat, MIP_COUNT);
#else
            _outputTex = new RenderTexture(width, height, 0, texFormat);
            _outputTex.useMipMap = MIP_COUNT > 1;
#endif
            _outputTex.name = name;

            _atlasPackerId = CAPI.OvrGpuSkinning_AtlasPackerCreate(
                (uint)width,
                (uint)height,
                CAPI.AtlasPackerPackingAlgortihm.Runtime);

            // Add some initial draw calls for the initial slices
            _drawCallsForSlice = new List<List<TDrawCallType>>(INITIAL_SLICE_COUNT);
            for (int slice = 0; slice < INITIAL_SLICE_COUNT; slice++)
            {
                _drawCallsForSlice.Add(new List<TDrawCallType>());
            }

            _handleToBlockData = new Dictionary<OvrSkinningTypes.Handle, BlockData>();

            _outputTex.filterMode = texFilterMode;
            _outputTex.useMipMap = false;
            _outputTex.autoGenerateMips = false;
            _outputTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            _outputTex.volumeDepth = INITIAL_SLICE_COUNT * _numDepthTexelsPerSlice;
        }

        public override void Destroy()
        {
            if (_outputTex != null)
            {
                _outputTex.Release();
                _outputTex = null;
            }

            if (_drawCallsForSlice.Count > 0)
            {
                foreach (List<TDrawCallType> drawCallsList in _drawCallsForSlice)
                {
                    foreach (TDrawCallType drawCall in drawCallsList)
                    {
                        drawCall.Destroy();
                    }
                    drawCallsList.Clear();
                }
                _drawCallsForSlice.Clear();
            }
        }

        // The shapesRect is specified in texels
        protected OvrSkinningTypes.Handle PackBlockAndExpandOutputIfNeeded(
            int widthInOutputTex,
            int heightInOutputTex)
        {
            // TODO* Call out to atlas packer to get packing rectangle, but, for now
            // assume a rectangle
            if (widthInOutputTex < _outputTex.width || heightInOutputTex < _outputTex.height)
            {
                return OvrSkinningTypes.Handle.kInvalidHandle;
            }

            OvrSkinningTypes.Handle packerHandle = CAPI.OvrGpuSkinning_AtlasPackerAddBlock(
                _atlasPackerId,
                (uint)widthInOutputTex,
                (uint)heightInOutputTex);

            var packResult = CAPI.OvrGpuSkinning_AtlasPackerResultsForBlock(
                _atlasPackerId,
                packerHandle);

            // See if tex array needs to grow
            // Tex slice is a 0 based index, where num tex array slices isn't
            if (packResult.texSlice >= _outputTex.volumeDepth / _numDepthTexelsPerSlice)
            {
                GrowRenderTexture((int)(packResult.texSlice + 1));
            }

            return packerHandle;
        }

        protected void AddBlockDataForHandle(
            CAPI.ovrTextureLayoutResult packResult,
            OvrSkinningTypes.Handle packerHandle,
            TDrawCallType drawCallThatCanFit,
            OvrSkinningTypes.Handle drawCallHandle)
        {
            _handleToBlockData[packerHandle] = new BlockData
            {
                skinnerDrawCall = drawCallThatCanFit,
                handleInDrawCall = drawCallHandle,
            };
        }

        protected BlockData GetBlockDataForHandle(OvrSkinningTypes.Handle handle)
        {
            return _handleToBlockData.TryGetValue(handle, out BlockData dataForThisBlock) ? dataForThisBlock : null;
        }

        protected TDrawCallType GetDrawCallThatCanFit(
            int texSlice,
            Func<TDrawCallType, bool> canFitFunc,
            Func<TDrawCallType> creatorFunc)
        {
            // TODO: This doesn't seem ideal?
            while (texSlice >= _drawCallsForSlice.Count)
            {
                _drawCallsForSlice.Add(new List<TDrawCallType>());
            }

            List<TDrawCallType> drawCallsList = _drawCallsForSlice[texSlice];

            foreach (TDrawCallType drawCall in drawCallsList)
            {
                if (canFitFunc.Invoke(drawCall))
                {
                    return drawCall;
                }
            }

            // No existing draw call can fit, make a new one
            TDrawCallType newDrawCall = creatorFunc();
            drawCallsList.Add(newDrawCall);
            return newDrawCall;
        }

        public void RemoveBlock(OvrSkinningTypes.Handle handle)
        {
            if (_handleToBlockData.TryGetValue(handle, out BlockData dataForThisBlock))
            {
                dataForThisBlock.skinnerDrawCall.RemoveBlock(dataForThisBlock.handleInDrawCall);
                _handleToBlockData.Remove(handle);
            }

            // TODO* Remove from packer when one is available
            CAPI.OvrGpuSkinning_AtlasPackerRemoveBlock(_atlasPackerId, handle);
        }

        public override CAPI.ovrTextureLayoutResult GetLayoutInOutputTex(OvrSkinningTypes.Handle handle)
        {
            return CAPI.OvrGpuSkinning_AtlasPackerResultsForBlock(_atlasPackerId, handle);
        }

        // public abstract NativeSlice<OvrJointsData.JointData>? GetJointTransformMatricesArray(
        //     OvrSkinningTypes.Handle handle);

        // public abstract void UpdateJointTransformMatrices(OvrSkinningTypes.Handle handle);

        private readonly SkinningOutputFrame[] destinations = { SkinningOutputFrame.FrameZero, SkinningOutputFrame.FrameOne };

        public override void UpdateOutputTexture()
        {
            Profiler.BeginSample("OvrGpuSkinnerBase.UpdateOutputTexture");
            // Call out to draw calls to do combining
            bool prevsRGB = GL.sRGBWrite;
            RenderTexture oldRT = RenderTexture.active;

            GL.sRGBWrite = false;

            Profiler.BeginSample("OvrGpuSkinnerBase.IterateDrawCalls");
            // For skinning, we do care about previous contents, keep them around by not clearing or discarding
            // unless every block across all draw calls are being updated here
            int depthSlice = 0;
            foreach (List<TDrawCallType> drawCallsList in _drawCallsForSlice)
            {
                // For interpolation purposes, each draw call can potentially write to multiple slices
                // in the output 3D texture (with different blocks enabled for the different slices)
                int sliceOffset = 0;
                foreach (var writeDestination in destinations)
                {
                    // we may not need this check and for loop if we have seperate frame 0 active list and frame 1 active list in the controller
                    // we'd probably want to call the whole function for either destination 0 or 1 as a parameter and determine slice offset.
                    if (CheckAnyNeedDraw(drawCallsList, writeDestination))
                    {
                        Profiler.BeginSample("OvrGpuSkinnerBase.OutputDrawCall");
                        // Each draw call
                        Graphics.SetRenderTarget(_outputTex, MIP_LEVEL, CubemapFace.Unknown, depthSlice + sliceOffset);

                        foreach (TDrawCallType drawCall in drawCallsList)
                        {
                            drawCall.Draw(writeDestination);
                        }

                        Profiler.EndSample(); //"OvrGpuSkinnerBase.OutputDrawCall"
                    }

                    sliceOffset++;
                }

                depthSlice++;
            }

            Profiler.EndSample(); // "OvrGpuSkinnerBase.IterateDrawCalls"

            GL.sRGBWrite = prevsRGB;
            RenderTexture.active = oldRT;

            Profiler.EndSample(); //"OvrGpuSkinnerBase.UpdateOutputTexture"
        }

        private bool CheckAnyNeedDraw(IList<TDrawCallType> drawCalls, SkinningOutputFrame outputFrame)
        {
            int callCount = drawCalls.Count;
            for (int idx = 0; idx < callCount; idx++)
            {
                if (drawCalls[idx].NeedsDraw(outputFrame))
                {
                    return true;
                }
            }

            return false;
        }

        public override RenderTexture GetOutputTex()
        {
            return _outputTex;
        }

        private void GrowRenderTexture(int newNumPackerSlices)
        {
            // Create new render texture of new size
            var newRt = new RenderTexture(
                _outputTex.width,
                _outputTex.height,
                DEPTH_BITS,
                _outputTex.format,
                MIP_COUNT);

            newRt.name = _outputTex.name;
            newRt.filterMode = _outputTex.filterMode;
            newRt.useMipMap = _outputTex.useMipMap;
            newRt.autoGenerateMips = _outputTex.autoGenerateMips;
            newRt.dimension = _outputTex.dimension;
            newRt.volumeDepth = newNumPackerSlices * _numDepthTexelsPerSlice;

            // TODO*: Copy over content from previous render texture?
            // When creating a new render texture, it starts empty, but to not "lose"
            // data from the previous render texture, the previous contents should be copied over
            // to the new texture
            _outputTex.Release();
            _outputTex = newRt;

            // Expand draw call list
            for (int i = _drawCallsForSlice.Count; i < newNumPackerSlices; i++)
            {
                _drawCallsForSlice.Add(new List<TDrawCallType>(1));
            }

            // Inform listeners
            ArrayResized?.Invoke(this, _outputTex);
        }
        public override void EnableBlockToRender(OvrSkinningTypes.Handle handle, SkinningOutputFrame outputFrame)
        {
            BlockData dataForBlock = GetBlockDataForHandle(handle);
            Debug.Assert(dataForBlock != null);
            bool skinningUpdated = dataForBlock.skinnerDrawCall.EnableBlock(dataForBlock.handleInDrawCall, outputFrame);
            if(skinningUpdated && _parentController != null) {
                _parentController.AddActiveSkinner(this);
            }
        }

        // Helper for Joint skinners
        protected void UpdateDrawCallQuality(OvrSkinningTypes.SkinningQuality quality)
        {
            foreach (var sliceList in _drawCallsForSlice)
            {
                foreach (var drawCall in sliceList)
                {
                    if (drawCall is IOvrGpuJointSkinnerDrawCall jointDrawCall)
                    {
                        jointDrawCall.Quality = quality;
                    }
                }
            }
        }

        public override GraphicsFormat GetOutputTexGraphicFormat() => outputFormat;

        protected readonly Shader _skinningShader;
        protected readonly OvrExpandableTextureArray _neutralPoseTex;
        protected readonly Vector2 _outputScaleBias;

        protected class BlockData
        {
            public OvrSkinningTypes.Handle handleInDrawCall;
            public TDrawCallType skinnerDrawCall;
        }

        private const int MIP_COUNT = 0;
        private const int MIP_LEVEL = 0;
        private const int DEPTH_BITS = 0; // no depth

        private RenderTexture _outputTex;
        private readonly CAPI.AtlasPackerId _atlasPackerId;

        private readonly List<List<TDrawCallType>> _drawCallsForSlice;
        private readonly Dictionary<OvrSkinningTypes.Handle, BlockData> _handleToBlockData;
    }
}
