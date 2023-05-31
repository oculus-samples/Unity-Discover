using System;
using System.Collections.Generic;
using Oculus.Avatar2;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Oculus.Skinning.GpuSkinning
{
    internal class OvrGpuSkinnerMorphTargetsOnlyDrawCall : OvrGpuSkinnerBaseDrawCall<OvrGpuSkinnerMorphTargetsOnlyDrawCall.PerBlockData>
    {
        public OvrGpuSkinnerMorphTargetsOnlyDrawCall(
            Shader skinningShader,
            Vector2 scaleBias,
            OvrGpuMorphTargetsCombiner combiner,
            OvrExpandableTextureArray neutralPoseTexture,
            OvrExpandableTextureArray indirectionTexture) :
            base(
                skinningShader,
                scaleBias,
                OvrMorphTargetsData.ShaderKeywordsForMorphTargets(),
                neutralPoseTexture,
                PerBlockData.STRIDE_BYTES)
        {
            _targetsData = new OvrMorphTargetsData(combiner, indirectionTexture, _skinningMaterial);
        }

        public override void Destroy()
        {
            base.Destroy();
            _targetsData.Destroy();
        }

        public OvrSkinningTypes.Handle AddBlock(
            RectInt texelRectInOutput,
            int outputTexWidth,
            int outputTexHeight,
            CAPI.ovrTextureLayoutResult layoutInNeutralPoseTex,
            CAPI.ovrTextureLayoutResult layoutInIndirectionTex)
        {
            PerBlockData blockData = new PerBlockData(
                layoutInNeutralPoseTex,
                NeutralPoseTexWidth,
                NeutralPoseTexHeight,
                layoutInIndirectionTex,
                _targetsData.IndirectionTexWidth,
                _targetsData.IndirectionTexHeight);

            return AddBlockData(texelRectInOutput, outputTexWidth, outputTexHeight, blockData);
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]

        internal struct PerBlockData
        {
            public PerBlockData(
                CAPI.ovrTextureLayoutResult layoutInNeutralPoseTex,
                int neutralPoseTexWidth,
                int neutralPoseTexHeight,
                CAPI.ovrTextureLayoutResult layoutInIndirectionTex,
                int indirectionTexWidth,
                int indirectionTexHeight)
            {
                Vector2 invTexDim = new Vector2(1.0f / neutralPoseTexWidth, 1.0f / neutralPoseTexHeight);

                _neutralPoseTexUvRect = new Vector4(
                    layoutInNeutralPoseTex.x * invTexDim.x,
                    layoutInNeutralPoseTex.y * invTexDim.y,
                    layoutInNeutralPoseTex.w * invTexDim.x,
                    layoutInNeutralPoseTex.h * invTexDim.y);

                _indicesAndSlices = new Vector4(
                    0.0f,
                    layoutInNeutralPoseTex.texSlice,
                    0.0f,
                    layoutInIndirectionTex.texSlice);

                invTexDim = new Vector2(1.0f / indirectionTexWidth, 1.0f / indirectionTexHeight);
                _indirectionTexUvRect = new Vector4(
                    layoutInIndirectionTex.x * invTexDim.x,
                    layoutInIndirectionTex.y * invTexDim.y,
                    layoutInIndirectionTex.w * invTexDim.x,
                    layoutInIndirectionTex.h * invTexDim.y);
            }

            [FieldOffset(VECTOR4_SIZE_BYTES * 0)] private Vector4 _neutralPoseTexUvRect;
            [FieldOffset(VECTOR4_SIZE_BYTES * 1)] private Vector4 _indicesAndSlices;
            [FieldOffset(VECTOR4_SIZE_BYTES * 2)] private Vector4 _indirectionTexUvRect;

            public static int STRIDE_BYTES = VECTOR4_SIZE_BYTES * 3;
        }

        private readonly OvrMorphTargetsData _targetsData;
    }
}
