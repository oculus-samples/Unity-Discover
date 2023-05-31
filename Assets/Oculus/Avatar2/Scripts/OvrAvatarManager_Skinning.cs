using System;

using UnityEngine;

using UnitySkinningQuality = UnityEngine.SkinWeights;

/// @file OvrAvatarManager_Skinning.cs

namespace Oculus.Avatar2
{
    public partial class OvrAvatarManager
    {
        ///
        /// Skinning implementation types.
        /// Used by @ref OvrAvatarManager to designate skinning implementation.
        [Flags]
        [System.Serializable]
        public enum SkinnerSupport
        {
            /// NO RENDER - No rendering data is built or stored, sim only (headless server)
            NONE = 0,
            /// Mesh data is loaded into standard `Unity.Mesh` fields
            UNITY = 1 << 0,
            /// Animation mesh data is stored in AvatarSDK internal buffers, it is not available to Unity systems
            OVR_CPU = 1 << 1,
            /// Mesh data is primarily stored in textures and compute buffers, it is not available to Unity systems
            OVR_GPU = 1 << 2,
            /// DEBUG ONLY - All modes are supported, wastes lots of memory
            ALL = ~0
        }

        [Header("Skinning Settings")]
        [Tooltip("Skinning implementations which assets will be loaded for, use the smallest set possible.\nThere are significant memory and load time costs to enabling more than one.")]
        [SerializeField]
        [EnumMask]
        private SkinnerSupport _skinnersSupported = SkinnerSupport.OVR_GPU;

        [Header("Unity Skinning")]
        [SerializeField]
        private SkinQuality[] _skinQualityPerLOD = Array.Empty<SkinQuality>();

        public bool UnitySMRSupported => (_skinnersSupported & SkinnerSupport.UNITY) == SkinnerSupport.UNITY;


        private const int GpuSkinningRequiredFeatureLevel = 45;
        private const int ComputeSkinningRequiredFeatureLevel = 45;

        // OVR_CPU skinner currently unimplemented
        public bool OvrCPUSkinnerSupported => false;

        public bool OvrGPUSkinnerSupported =>
            gpuSkinningShaderLevelSupported && (_skinnersSupported & SkinnerSupport.OVR_GPU) == SkinnerSupport.OVR_GPU;

        public bool OvrComputeSkinnerSupported => false; // No support right now

        public bool UnitySkinnerSupported =>
            (_skinnersSupported & SkinnerSupport.UNITY) == SkinnerSupport.UNITY;

        // Set via `Initialize`
        private int _shaderLevelSupport = -1;
        internal bool gpuSkinningShaderLevelSupported
        {
            get
            {
                Debug.Assert(_shaderLevelSupport >= 0);
                return _shaderLevelSupport >= GpuSkinningRequiredFeatureLevel;
            }
        }

        internal bool computeSkinningShaderLevelSupported
        {
            get
            {
                Debug.Assert(_shaderLevelSupport >= 0);
                return _shaderLevelSupport >= ComputeSkinningRequiredFeatureLevel;
            }
        }

        public SkinQuality GetUnitySkinQualityForLODIndex(uint lodIndex)
        {
            return lodIndex < _skinQualityPerLOD.Length ?
                (SkinQuality)Mathf.Min((int)_skinQualityPerLOD[lodIndex], (int)HighestUnitySkinningQuality)
                : HighestUnitySkinningQuality;
        }

        // Helper to query Unity skinWeights/boneWeights configuration as SkinningQuality enum
        public SkinQuality HighestUnitySkinningQuality
        {
            get
            {
                switch (QualitySettings.skinWeights)
                {
                    case UnitySkinningQuality.OneBone:
                        return SkinQuality.Bone1;
                    case UnitySkinningQuality.TwoBones:
                        return SkinQuality.Bone2;
                    case UnitySkinningQuality.FourBones:
                        return SkinQuality.Bone4;
                }
                return SkinQuality.Auto;
            }
        }

        private void ValidateSupportedSkinners()
        {
            if (!gpuSkinningShaderLevelSupported && (_skinnersSupported & SkinnerSupport.OVR_GPU) == SkinnerSupport.OVR_GPU)
            {
                // gpu skinning not actually supported so remove from supported list.
                _skinnersSupported &= ~SkinnerSupport.OVR_GPU;
                if (_skinnersSupported == SkinnerSupport.NONE)
                {
                    _skinnersSupported = SkinnerSupport.UNITY;
                }
            }
        }
    }
}
