using Oculus.Avatar2;
using System;
using Unity.Collections;
using UnityEngine;

namespace Oculus.Skinning.GpuSkinning
{
    internal interface IOvrGpuJointSkinnerDrawCall
    {
        OvrSkinningTypes.SkinningQuality Quality { get; set; }
    }

    internal abstract class OvrGpuSkinnerBaseDrawCall<TBlockData> : IOvrGpuSkinnerDrawCall
        where TBlockData : struct
    {
        protected const int VECTOR3_SIZE_BYTES = sizeof(float) * 3;
        protected const int VECTOR4_SIZE_BYTES = sizeof(float) * 4;

        protected int NeutralPoseTexWidth => _neutralPoseTex.Width;
        protected int NeutralPoseTexHeight => _neutralPoseTex.Height;

        internal OvrGpuSkinnerBaseDrawCall(
            Shader skinningShader,
            Vector2 scaleBias,
            string[] shaderKeywords,
            OvrExpandableTextureArray neutralPoseTexture,
            int blockDataStrideBytes)
        {
            OvrAvatarLog.Assert(skinningShader != null);

            _skinningMaterial = new Material(skinningShader);
            _neutralPoseTex = neutralPoseTexture;

            _blockDataStrideBytes = blockDataStrideBytes;

            _blockEnabledFrameZero = Array.Empty<float>();
            _blockEnabledFrameOne = Array.Empty<float>();

            foreach (var kw in shaderKeywords)
            {
                _skinningMaterial.EnableKeyword(kw);
            }

            _mesh = new Mesh();
            _mesh.vertices = Array.Empty<Vector3>();
            _mesh.uv = Array.Empty<Vector2>();
            _mesh.colors = Array.Empty<Color>();
            _mesh.triangles = Array.Empty<int>();

            _meshLayout = new OvrFreeListBufferTracker(MAX_QUADS);

            _areAnyBlocksEnabledFrameZero = false;
            _areAnyBlocksEnabledFrameOne = false;

            _neutralPoseTex.ArrayResized += NeutralPoseTexResized;

            SetNeutralPoseTextureInMaterial(_neutralPoseTex.GetTexArray());
            SetBuffersInMaterial();
            SetScaleBiasInMaterial(scaleBias);
        }

        public virtual void Destroy()
        {
            _blockDataBuffer?.Release();

            if (_mesh != null)
            {
                Mesh.Destroy(_mesh);
            }

            if (_skinningMaterial != null)
            {
                Material.Destroy(_skinningMaterial);
            }

            _neutralPoseTex.ArrayResized -= NeutralPoseTexResized;
        }

        protected void TransitionShaderKeywords(string[] oldKeywords, string[] newKeywords)
        {
            foreach (var kw in oldKeywords)
            {
                _skinningMaterial.DisableKeyword(kw);
            }

            foreach (var kw in newKeywords)
            {
                _skinningMaterial.EnableKeyword(kw);
            }
        }
        protected void TransitionQualityKeywords(OvrSkinningTypes.SkinningQuality oldQuality, OvrSkinningTypes.SkinningQuality newQuality)
        {
            Debug.Assert(oldQuality != newQuality);
            TransitionShaderKeywords(
                OvrJointsData.ShaderKeywordsForJoints(oldQuality),
                OvrJointsData.ShaderKeywordsForJoints(newQuality)
            );
        }

        private void NeutralPoseTexResized(object sender, Texture2DArray newArray)
        {
            SetNeutralPoseTextureInMaterial(newArray);
        }


        public bool EnableBlock(OvrSkinningTypes.Handle layoutHandle, SkinningOutputFrame writeDest)
        {
            bool needsDrawUpdate = false;
            if (layoutHandle.IsValid())
            {
                int blockIndex = layoutHandle.GetValue();

                switch (writeDest)
                {
                    case SkinningOutputFrame.FrameZero:
                        _areAnyBlocksEnabledFrameZero = true;
                        _blockEnabledFrameZero[blockIndex] = 1.0f;
                        needsDrawUpdate = true;

                        break;
                    case SkinningOutputFrame.FrameOne:
                        _areAnyBlocksEnabledFrameOne = true;
                        _blockEnabledFrameOne[blockIndex] = 1.0f;
                        needsDrawUpdate = true;
                        break;
                }
            }
            return needsDrawUpdate;
        }

        protected OvrSkinningTypes.Handle AddBlockData(
            RectInt texelRectInOutput,
            int outputTexWidth,
            int outputTexHeight,
            TBlockData blockData)
        {
            OvrSkinningTypes.Handle layoutHandle = _meshLayout.TrackBlock(1);

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
                OvrSkinningQuads.ExpandMeshToFitQuad(_mesh);
            }

            OvrSkinningQuads.UpdateQuadInMesh(
                vertStartIndex,
                blockIndex,
                texelRectInOutput,
                outputTexWidth,
                outputTexHeight,
                _mesh);

            // Expand compute buffers and lists if needed
            int newNumBlocks = blockIndex + 1;
            if (newNumBlocks > _blockEnabledFrameZero.Length)
            {
                // Grab contents of block data buffer. Due to Unity API restrictions,
                // there is no copying between buffers, so the only way to enlarge a buffer
                // but not blow away contents is to read via GetData which causes a pipeline stall.
                // This isn't expected to happen very frequently (only when a block causes the mesh
                // to increase in size).
                //
                // TODO: if the data left in the front of the array is the same as before and we're
                // just adding data to the back of the array, use:
                // _blockDataBuffer.SetData(tempArray,oldNumBlocks,oldNumBlocks,newNumBlocks-oldNumBlocks);
                //
                // TODO* See how slow this is and potentially have a boolean dictating
                // whether or not to use more memory to circumvent the stall
                //
                TBlockData[] tempArray = _blockDataBuffer == null ? null : new TBlockData[_blockDataBuffer.count];
                _blockDataBuffer?.GetData(tempArray);

                // Enlarge buffer and copy back contents
                _blockDataBuffer?.Release();
                _blockDataBuffer = new ComputeBuffer(newNumBlocks, _blockDataStrideBytes);
                if (tempArray != null) { _blockDataBuffer.SetData(tempArray); }


                Array.Resize(ref _blockEnabledFrameZero, newNumBlocks);
                Array.Resize(ref _blockEnabledFrameOne, newNumBlocks);
                FlushBlockEnabled(_blockEnabledFrameZero);
                FlushBlockEnabled(_blockEnabledFrameOne);

                SetBuffersInMaterial();
            }

            // going to wait to set this on the main thread, to borrow the pipeline barrier unity will set for this buffer.
            _newBlockData = blockData;
            _newBlockIndex = blockIndex;
            _hasNewBlockData = true;

            return layoutHandle;
        }

        public virtual void RemoveBlock(OvrSkinningTypes.Handle handle)
        {
            _meshLayout.FreeBlock(handle);
        }

        public void Draw(SkinningOutputFrame writeDest)
        {
            switch (writeDest)
            {
                case SkinningOutputFrame.FrameZero:
                    DrawToFrame(ref _areAnyBlocksEnabledFrameZero, _blockEnabledFrameZero);
                    break;
                case SkinningOutputFrame.FrameOne:
                    DrawToFrame(ref _areAnyBlocksEnabledFrameOne, _blockEnabledFrameOne);
                    break;
            }
        }

        private void DrawToFrame(ref bool areAnyBlocksEnabled, float[] blockEnabledArray)
        {
            // Early exit
            if (!areAnyBlocksEnabled)
            {
                return;
            }

            // Copy from block enabled array to compute buffer
            Debug.Assert(blockEnabledArray.Length == 0 || blockEnabledArray.Length == 1);
            float blockEnabled = (blockEnabledArray.Length >= 1) ? blockEnabledArray[0] : 0.0f;
            _skinningMaterial.SetFloat(BLOCK_ENABLED_PROP, blockEnabled);

            // We are delaying setting the data for this block until here. Unity isn't
            // properly putting a pipeline barrier for our neutral pose texture upload.
            // but it does for this kind of buffer, so by waiting to here, the barrier
            // for this buffer will also apply to the neutral pose texture, and we won't
            // get a screen flash in vulkan.
            if (_hasNewBlockData)
            {
                // only one new block of data has been introduced, so set it here:
                var nativeWrapper = new NativeArray<TBlockData>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                {
                    nativeWrapper[0] = _newBlockData;
                    _blockDataBuffer.SetData(nativeWrapper, 0, _newBlockIndex, 1);
                }
                _hasNewBlockData = false;
            }

            // Don't care about matrices as the shader used should handle clip space
            // conversions without matrices (due to how quads set up)
            bool didSetPass = _skinningMaterial.SetPass(0);
            Debug.Assert(didSetPass);
            Graphics.DrawMeshNow(_mesh, Matrix4x4.identity);

            // Reset booleans and mark all blocks as disabled for next frame
            areAnyBlocksEnabled = false;
            ClearBlockEnabled(blockEnabledArray);
        }

        // Ensure virtual method for interface,
        bool IOvrGpuSkinnerDrawCall.NeedsDraw(SkinningOutputFrame dest)
        {
            switch (dest)
            {
                case SkinningOutputFrame.FrameZero:
                    return _areAnyBlocksEnabledFrameZero;
                case SkinningOutputFrame.FrameOne:
                    return _areAnyBlocksEnabledFrameOne;
            }

            return false;
        }

        void IOvrGpuSkinnerDrawCall.Draw(SkinningOutputFrame outputFrame)
        {
            this.Draw(outputFrame);
        }

        internal bool CanAdditionalQuad()
        {
            return _meshLayout.CanFit(1);
        }

        private void SetBuffersInMaterial()
        {
            _skinningMaterial.SetBuffer(BLOCK_DATA_PROP, _blockDataBuffer);
        }

        private void SetNeutralPoseTextureInMaterial(Texture2DArray texture)
        {
            _skinningMaterial.SetTexture(NEUTRAL_POSE_TEX_PROP, texture);
        }

        private void SetScaleBiasInMaterial(Vector2 scaleBias)
        {
            if (!Mathf.Approximately(scaleBias.x, 1.0f) || !Mathf.Approximately(scaleBias.y, 0.0f))
            {
                _skinningMaterial.EnableKeyword("OVR_OUTPUT_SCALE_BIAS");
            }
            else
            {
                _skinningMaterial.DisableKeyword("OVR_OUTPUT_SCALE_BIAS");
            }

            _skinningMaterial.SetVector(OUTPUT_SCALE_BIAS_PROP, scaleBias);
        }

        private void ClearBlockEnabled(float[] blockEnabledArray)
        {
            Array.Clear(blockEnabledArray, 0, blockEnabledArray.Length);
        }

        private void FlushBlockEnabled(float[] blockEnabledArray)
        {
            for (int i = 0; i < blockEnabledArray.Length; i++)
            {
                blockEnabledArray[i] = 1.0f;
            }
        }

        protected readonly Material _skinningMaterial;

        private OvrExpandableTextureArray _neutralPoseTex;

        private ComputeBuffer _blockDataBuffer = null;

        private bool _hasNewBlockData = false;
        private int _newBlockIndex;
        private TBlockData _newBlockData;

        private readonly int _blockDataStrideBytes;
        // The Unity API for ComputeBuffer only allows setting via
        // an array. The block enabled buffer will be changed completely every time
        // Draw() is called, so in order to not have to make a new temporary array
        // every Draw(), make a private field here
        private float[] _blockEnabledFrameZero;
        private float[] _blockEnabledFrameOne;

        private readonly Mesh _mesh;
        private readonly OvrFreeListBufferTracker _meshLayout;

        private bool _areAnyBlocksEnabledFrameZero = false;
        private bool _areAnyBlocksEnabledFrameOne = false;

        private const int BYTES_PER_FLOAT = 4;
        private const int NUM_VERTS_PER_QUAD = 4;
        private const int MAX_QUADS = ushort.MaxValue / NUM_VERTS_PER_QUAD;

        private static readonly int NEUTRAL_POSE_TEX_PROP = Shader.PropertyToID("u_NeutralPoseTex");
        private static readonly int BLOCK_ENABLED_PROP = Shader.PropertyToID("u_BlockEnabled");
        private static readonly int BLOCK_DATA_PROP = Shader.PropertyToID("u_BlockData");

        private static readonly int OUTPUT_SCALE_BIAS_PROP = Shader.PropertyToID("u_OutputScaleBias");
    }
}
