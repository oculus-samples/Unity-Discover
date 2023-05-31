using Oculus.Avatar2;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Oculus.Skinning.GpuSkinning
{
    internal sealed class OvrGpuSkinnerJointsOnlyDrawCall : OvrGpuSkinnerBaseDrawCall<OvrGpuSkinnerJointsOnlyDrawCall.PerBlockData>, IOvrGpuJointSkinnerDrawCall
    {
        public OvrGpuSkinnerJointsOnlyDrawCall(
            Shader skinningShader,
            Vector2 scaleBias,
            OvrExpandableTextureArray neutralPoseTexture,
            OvrExpandableTextureArray jointsTexture,
            OvrSkinningTypes.SkinningQuality quality) :
            base(
                skinningShader,
                scaleBias,
                OvrJointsData.ShaderKeywordsForJoints(quality),
                neutralPoseTexture,
                PerBlockData.STRIDE_BYTES)
        {
            _quality = quality;
            _jointsData = new OvrJointsData(jointsTexture, _skinningMaterial);
            _meshHandleToJointsHandle = new Dictionary<OvrSkinningTypes.Handle, OvrSkinningTypes.Handle>();
        }

        public override void Destroy()
        {
            _jointsData.Destroy();
            base.Destroy();
        }

        public OvrSkinningTypes.Handle AddBlock(
            RectInt texelRectInOutput,
            int outputTexWidth,
            int outputTexHeight,
            CAPI.ovrTextureLayoutResult layoutInNeutralPoseTex,
            CAPI.ovrTextureLayoutResult layoutInJointsTex,
            int numJoints)
        {
            OvrSkinningTypes.Handle jointsHandle = _jointsData.AddJoints(numJoints);

            if (!jointsHandle.IsValid())
            {
                return jointsHandle;
            }

            OvrFreeListBufferTracker.LayoutResult jointsLayout = _jointsData.GetLayoutForJoints(jointsHandle);

            PerBlockData blockData = new PerBlockData(
                jointsLayout.startIndex,
                layoutInNeutralPoseTex,
                NeutralPoseTexWidth,
                NeutralPoseTexHeight,
                layoutInJointsTex,
                _jointsData.JointsTexWidth,
                _jointsData.JointsTexHeight);

            OvrSkinningTypes.Handle meshHandle = AddBlockData(texelRectInOutput, outputTexWidth, outputTexHeight, blockData);

            _meshHandleToJointsHandle[meshHandle] = jointsHandle;

            return meshHandle;
        }

        public override void RemoveBlock(OvrSkinningTypes.Handle handle)
        {
            base.RemoveBlock(handle);

            if (_meshHandleToJointsHandle.TryGetValue(handle, out OvrSkinningTypes.Handle jointsHandle))
            {
                _jointsData.RemoveJoints(jointsHandle);
            }
        }

        public bool CanFitAdditionalJoints(int numJoints)
        {
            return _jointsData.CanFitAdditionalJoints(numJoints);
        }

        public IntPtr GetJointTransformMatricesArray(OvrSkinningTypes.Handle handle)
        {
            return _jointsData.GetJointTransformMatricesArray(handle);
        }

        [StructLayout(LayoutKind.Explicit, Size = VECTOR4_SIZE_BYTES * 3)]
        internal struct PerBlockData
        {
            public PerBlockData(
                int jointsStartIndex,
                CAPI.ovrTextureLayoutResult layoutInNeutralPoseTex,
                int neutralPoseTexWidth,
                int neutralPoseTexHeight,
                CAPI.ovrTextureLayoutResult layoutInJointsTex,
                int jointsTexWidth,
                int jointsTexHeight)
            {
                Vector2 invTexDim = new Vector2(1.0f / neutralPoseTexWidth, 1.0f / neutralPoseTexHeight);

                _neutralPoseTexUvRect = new Vector4(
                    layoutInNeutralPoseTex.x * invTexDim.x,
                    layoutInNeutralPoseTex.y * invTexDim.y,
                    layoutInNeutralPoseTex.w * invTexDim.x,
                    layoutInNeutralPoseTex.h * invTexDim.y);

                _indicesAndSlices = new Vector4(
                    jointsStartIndex,
                    layoutInNeutralPoseTex.texSlice,
                    layoutInJointsTex.texSlice,
                    0.0f);

                invTexDim = new Vector2(1.0f / jointsTexWidth, 1.0f / jointsTexHeight);

                _jointsTexUvRect = new Vector4(
                    layoutInJointsTex.x * invTexDim.x,
                    layoutInJointsTex.y * invTexDim.y,
                    layoutInJointsTex.w * invTexDim.x,
                    layoutInJointsTex.h * invTexDim.y);
            }

            [FieldOffset(VECTOR4_SIZE_BYTES * 0)] private Vector4 _neutralPoseTexUvRect;
            [FieldOffset(VECTOR4_SIZE_BYTES * 1)] private Vector4 _indicesAndSlices;
            [FieldOffset(VECTOR4_SIZE_BYTES * 2)] private Vector4 _jointsTexUvRect;

            public const int STRIDE_BYTES = VECTOR4_SIZE_BYTES * 3;
        }

        private readonly OvrJointsData _jointsData;
        private readonly Dictionary<OvrSkinningTypes.Handle, OvrSkinningTypes.Handle> _meshHandleToJointsHandle;

        private OvrSkinningTypes.SkinningQuality _quality = OvrSkinningTypes.SkinningQuality.Invalid;
        OvrSkinningTypes.SkinningQuality IOvrGpuJointSkinnerDrawCall.Quality
        {
            get => _quality;
            set
            {
                if (_quality != value)
                {
                    TransitionQualityKeywords(_quality, value);
                    _quality = value;
                }
            }
        }
    }
}
