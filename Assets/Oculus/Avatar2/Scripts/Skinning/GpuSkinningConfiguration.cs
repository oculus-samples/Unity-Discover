// #define OVR_AVATAR_CLAMP_SKINNING_QUALITY

using System;

using Oculus.Skinning;
using Oculus.Skinning.GpuSkinning;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

#if UNITY_2019_3_OR_NEWER
using UnitySkinningQuality = UnityEngine.SkinWeights;
#else
using UnitySkinningQuality = UnityEngine.BlendWeights;
#endif

using SkinningConfig = Oculus.Avatar2.OvrAvatarEntity.SkinningConfig;

/// @file GpuSkinningConfiguration.cs

namespace Oculus.Avatar2
{
    // TODO: Convert to ScriptableObject
    ///
    /// Contains configuration options for GPU skinning.
    /// You can specify the skinning quality (number of bones per vertex),
    /// the numeric precision (float, half) and what type of skinning
    /// implementation to use.
    /// @see OvrAvatarSkinnedRenderable
    ///
    public class GpuSkinningConfiguration : OvrSingletonBehaviour<GpuSkinningConfiguration>
    {
        private const string logScope = "GpuSkinningConfiguration";

        // Avoid skinning more avatars than MaxSkinnedAvatarsPerFrame per frame
        public const uint MaxSkinnedAvatarsPerFrame = OvrAvatarGpuSkinningController.MaxSkinnedAvatarsPerFrame;

        internal enum TexturePrecision { Float = 0, Half = 1, Unorm16 = 2, Snorm10 = 3, Byte = 4, Nibble = 5 }

        /// Maximum allowed skinning quality (bones per vertex).
        // Helper to query Unity skinWeights/boneWeights configuration as SkinningQuality enum
        public OvrSkinningTypes.SkinningQuality MaxAllowedSkinningQuality
        {
            get
            {
#if OVR_AVATAR_CLAMP_SKINNING_QUALITY
                // It'd be nice to cache this, but it can be changed by script at runtime
#if UNITY_2019_3_OR_NEWER
                switch (QualitySettings.skinWeights)
#else // 2018
                switch (QualitySettings.blendWeights)
#endif
                {
                    case UnitySkinningQuality.OneBone:
                        return OvrSkinningTypes.SkinningQuality.Bone1;
                    case UnitySkinningQuality.TwoBones:
                        return OvrSkinningTypes.SkinningQuality.Bone2;
                    case UnitySkinningQuality.FourBones:
                        return OvrSkinningTypes.SkinningQuality.Bone4;
                }
                return OvrSkinningTypes.SkinningQuality.Invalid;
#else
                return OvrSkinningTypes.SkinningQuality.Bone4;
#endif
            }
        }

        /// Default skinning implementation to use.
        [SerializeField]
        [Tooltip("SkinningConfig used when OvrAvatarEntity is set to `DEFAULT`")]
        private SkinningConfig DefaultSkinningType = SkinningConfig.DEFAULT;

        /// Fallback skinning type (when skinning implementation is not specified).
        // Used if Entity does not specify and DefaultSkinningType == SkinningConfig.DEFAULT
        private SkinningConfig FallbackSkinningType = SkinningConfig.OVR_UNITY_GPU_FULL;

        /// Skinning quality (bones per vertex) used for each level of detail.
        [SerializeField]
        [Tooltip("SkinningQuality (number of bone influences per vert) used for each LOD")]
        private OvrSkinningTypes.SkinningQuality[] QualityPerLOD = Array.Empty<OvrSkinningTypes.SkinningQuality>();

        /// Precision of source morph textures.
        [Header("Texture Settings (Advanced)")]
        [Tooltip("Morph texture should work well as snorm10")]
        [SerializeField]
        internal TexturePrecision SourceMorphFormat = TexturePrecision.Float;

        /// Precision of combined morph textures.
        [Tooltip("Morph texture should work well as half")]
        [SerializeField]
        internal TexturePrecision CombinedMorphFormat = TexturePrecision.Float;

        /// Precision of skinning output textures.
        [Tooltip("Skinner Output likely needs to be float")]
        [SerializeField]
        internal TexturePrecision SkinnerOutputFormat = TexturePrecision.Float;

        /// Scale used to calculate scale and bias used when using unorm formats.
        [Tooltip("Scale used to calculate scale and bias used when using unorm formats")]
        [SerializeField]
        internal float SkinnerUnormScale = 4.0f;

        /// Format of neutral pose texture.
        [SerializeField]
        readonly internal GraphicsFormat NeutralPoseFormat = GraphicsFormat.R32G32B32A32_SFloat;

        /// Format of joints texture.
        [SerializeField]
        readonly internal GraphicsFormat JointsFormat = GraphicsFormat.R32G32B32A32_SFloat;

        /// Format of indirection texture.
        [SerializeField]
        readonly internal GraphicsFormat IndirectionFormat = GraphicsFormat.R32G32B32A32_SFloat;

        [Header("Performance Optimizations (Advanced)")]
        [Tooltip("Enables Support for Application Space Warp. Applications still need to enable ASW.")]
        [SerializeField]
        internal bool SupportApplicationSpaceWarp;

        [Header("Experimental Settings (Advanced, Unsupported)")]

        /// Enable motion smoothing for GPU skinning.
        [Tooltip("Smooths Motion Between Animation Updates but Introduces Latency. Ignored for Unity skinning types.")]
        [SerializeField]
        internal bool MotionSmoothing = false;

        [Header("Shader Settings (Advanced)")]

        // Assigned via editor
        // TODO: Remove suppresion of unused variable warning once auto-recompile is landed
#pragma warning disable CS0649
        [SerializeField]
        private Shader _CombineMorphTargetsShader;
        [SerializeField]
        private Shader _SkinToTextureShader;
#pragma warning restore CS0649 //  is never assigned to

        /// Shader to use for combining morph targets.
        public Shader CombineMorphTargetsShader => _CombineMorphTargetsShader;

        // Shader to use for skinning.
        public Shader SkinToTextureShader => _SkinToTextureShader;

        protected override void Initialize()
        {
            ValidateTexturePrecision(ref SourceMorphFormat, FormatUsage.Linear);
            ValidateTexturePrecision(ref CombinedMorphFormat, FormatUsage.Blend);
            ValidateTexturePrecision(ref SkinnerOutputFormat, FormatUsage.Render);
        }

        internal static void HandleDefaultConfig(ref SkinningConfig config)
        {
            Debug.Assert(hasInstance);
            if (config == SkinningConfig.DEFAULT)
            {
                var defaultConfig = Instance.DefaultSkinningType;
                config = defaultConfig != SkinningConfig.DEFAULT ? defaultConfig : Instance.FallbackSkinningType;
            }
        }

        internal OvrSkinningTypes.SkinningQuality GetQualityForLOD(uint lodIndex)
        {
            Debug.Assert(lodIndex < QualityPerLOD.Length);
            var configValue = QualityPerLOD[lodIndex];
#if OVR_AVATAR_CLAMP_SKINNING_QUALITY
            configValue = (OvrSkinningTypes.SkinningQuality)Math.Min((int)configValue, (int)MaxAllowedSkinningQuality);
#endif
            return configValue;
        }

        internal void ValidateFallbackSkinner(bool unitySkinnerSupported, bool gpuSkinnerSupported)
        {
            if (!gpuSkinnerSupported && FallbackSkinningType == SkinningConfig.OVR_UNITY_GPU_FULL)
            {
                FallbackSkinningType = unitySkinnerSupported ? SkinningConfig.UNITY : SkinningConfig.NONE;
            }
        }

        private void ValidateTexturePrecision(ref TexturePrecision precision, FormatUsage usage)
        {
            var configPrecision = precision;

            // Float is "lowest common denominator" - if the system doesn't support that it can't gpu skin
            // TODO: Trigger fallback to UnitySMR if float isn't supported? Should be caught by ShaderModel check
            while (precision > TexturePrecision.Float)
            {
                var graphicsFormat = precision.GetGraphicsFormat();
                if (SystemInfo.IsFormatSupported(graphicsFormat, usage))
                {
                    // precision is supported - use it
                    break;
                }

                // precision is not supported - drop down to next simpler/more-compatible format
                precision--;
            }

            if (precision != configPrecision)
            {
                OvrAvatarLog.LogWarning(
                    $"Configured precision {configPrecision} unsupported for usage {usage}"
                    + $" - falling back to {precision} for compatibility"
                    , logScope, this);
            }
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            EnforceValidFormatMorph(ref SourceMorphFormat);
            EnforceValidFormat(ref CombinedMorphFormat);
            EnforceValidFormat(ref SkinnerOutputFormat);

            ValidateQualityPerLOD();

            CheckDefaultShader(ref _CombineMorphTargetsShader, "Avatar/CombineMorphTargets");
            CheckDefaultShader(ref _SkinToTextureShader, "Avatar/SkinToTexture");
        }

        private void EnforceValidFormat(ref TexturePrecision precision)
        {
            if (precision == TexturePrecision.Byte || precision == TexturePrecision.Nibble || precision == TexturePrecision.Snorm10)
            {
                RevertFormat(ref precision, TexturePrecision.Half);
            }
        }

        private void EnforceValidFormatMorph(ref TexturePrecision precision)
        {
            if (precision == TexturePrecision.Byte || precision == TexturePrecision.Nibble || precision == TexturePrecision.Unorm16)
            {
                RevertFormat(ref precision, TexturePrecision.Snorm10);
            }
        }

        private void RevertFormat<T>(ref T textureFormat, T correctFormat) where T : Enum
        {
            OvrAvatarLog.LogError($"Unsupported format {textureFormat}, reverted to {correctFormat}");
            Debug.LogWarning("Bad GpuSkinningConfig setting", this);
            textureFormat = correctFormat;
        }

        private void ValidateQualityPerLOD()
        {
            int oldLength = QualityPerLOD?.Length ?? 0;
            for (int idx = 0; idx < oldLength; idx++)
            {
                ref var lod = ref QualityPerLOD[idx];
                // TODO: Does `Invalid` have any useful meaning as a configuration here?
                lod = (OvrSkinningTypes.SkinningQuality)
                    Mathf.Clamp((int)lod, (int)OvrSkinningTypes.SkinningQuality.Bone1, (int)OvrSkinningTypes.SkinningQuality.Bone4);
            }
            if (oldLength != CAPI.ovrAvatar2EntityLODFlagsCount)
            {
                Array.Resize(ref QualityPerLOD, (int)CAPI.ovrAvatar2EntityLODFlagsCount);

                var fillValue = oldLength > 0 && oldLength <= QualityPerLOD.Length ? QualityPerLOD[oldLength - 1] : OvrSkinningTypes.SkinningQuality.Bone4;
                for (int newIdx = oldLength; newIdx < CAPI.ovrAvatar2EntityLODFlagsCount; newIdx++)
                {
                    QualityPerLOD[newIdx] = fillValue;
                }
            }
        }

        private void CheckDefaultShader(ref Shader shaderProperty, string defaultShaderName)
        {
            if (shaderProperty == null)
            {
                shaderProperty = Shader.Find(defaultShaderName);
            }
        }
#endif
    }
} // namespace Oculus.Avatar
