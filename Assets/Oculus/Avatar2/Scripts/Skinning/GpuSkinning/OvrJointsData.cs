// Check for differences in update vs current state and ignore if they match
//#define OVR_GPUSKINNING_DIFFERENCE_CHECK

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

using Oculus.Avatar2;

namespace Oculus.Skinning.GpuSkinning
{
    internal class OvrJointsData
    {
        public int JointsTexWidth => _jointsTex.Width;
        public int JointsTexHeight => _jointsTex.Height;

        public static string[] ShaderKeywordsForJoints(OvrSkinningTypes.SkinningQuality quality)
        {
            var qualityIndex = (uint)quality;
            var keywords = qualityIndex < _KeywordLookup.Length ? _KeywordLookup[qualityIndex] : null;
            if (keywords == null)
            {
                throw new ArgumentOutOfRangeException(nameof(quality), quality, "Invalid SkinningQuality value");
            }
            return keywords;
        }

        public OvrJointsData(OvrExpandableTextureArray jointsTexture, Material skinningMaterial)
        {
            _jointsTex = jointsTexture;
            _skinningMaterial = skinningMaterial;

            _jointsBufferLayout = new OvrFreeListBufferTracker(OvrComputeBufferPool.MaxJoints);

            _jointsTex.ArrayResized += JointsTexArrayResized;

            SetJointsTextureInMaterial(jointsTexture.GetTexArray());
            SetBuffersInMaterial();
        }

        public void Destroy()
        {
            _jointsTex.ArrayResized -= JointsTexArrayResized;
        }

        private void JointsTexArrayResized(object sender, Texture2DArray newArray)
        {
            SetJointsTextureInMaterial(newArray);
        }

        public OvrSkinningTypes.Handle AddJoints(int numJoints)
        {
            OvrSkinningTypes.Handle layoutHandle = _jointsBufferLayout.TrackBlock(numJoints);
            if (!layoutHandle.IsValid())
            {
                return layoutHandle;
            }

            SetBuffersInMaterial();

            return layoutHandle;
        }

        public OvrFreeListBufferTracker.LayoutResult GetLayoutForJoints(OvrSkinningTypes.Handle handle)
        {
            return _jointsBufferLayout.GetLayoutInBufferForBlock(handle);
        }

        public void RemoveJoints(OvrSkinningTypes.Handle handle)
        {
            _jointsBufferLayout.FreeBlock(handle);
        }

        public bool CanFitAdditionalJoints(int numJoints)
        {
            return _jointsBufferLayout.CanFit(numJoints);
        }

        public IntPtr GetJointTransformMatricesArray(OvrSkinningTypes.Handle handle)
        {
            var layout = _jointsBufferLayout.GetLayoutInBufferForBlock(handle);

            if (!layout.IsValid)
            {
                return IntPtr.Zero;
            }

            var jointEntry = OvrAvatarManager.Instance.GpuSkinningController.GetNextEntryJoints();
            _skinningMaterial.SetInt(JOINT_OFFSET_PROP, jointEntry.JointOffset);

            return jointEntry.Data;
        }

        private void SetBuffersInMaterial()
        {
            _skinningMaterial.SetBuffer(JOINT_MATRICES_PROP, OvrAvatarManager.Instance.GpuSkinningController.GetJointBuffer());
        }

        private void SetJointsTextureInMaterial(Texture2DArray texture)
        {
            _skinningMaterial.SetTexture(JOINTS_TEX_PROP, texture);
        }


        private OvrExpandableTextureArray _jointsTex;
        private Material _skinningMaterial;

        private readonly OvrFreeListBufferTracker _jointsBufferLayout;

        private const string OVR_FOUR_BONES_KEYWORD = "OVR_SKINNING_QUALITY_4_BONES";
        private const string OVR_TWO_BONES_KEYWORD = "OVR_SKINNING_QUALITY_2_BONES";
        private const string OVR_ONE_BONE_KEYWORD = "OVR_SKINNING_QUALITY_1_BONE";

        private static readonly int JOINTS_TEX_PROP = Shader.PropertyToID("u_JointsTex");
        private static readonly int JOINT_MATRICES_PROP = Shader.PropertyToID("u_JointMatrices");
        private static readonly int JOINT_OFFSET_PROP = Shader.PropertyToID("u_JointOffset");

        private static readonly string[][] _KeywordLookup = {
                /* 0, INVALID */ null,
                /* 1, Bone1 */ new[] { OVR_ONE_BONE_KEYWORD },
                /* 2, Bone2 */ new[] { OVR_TWO_BONES_KEYWORD },
                /* 3, Unsupported */ null,
                /* 4, Bone4 */ new[] { OVR_FOUR_BONES_KEYWORD },
        };
    }
}
