//#define OVR_AVATAR_AUTO_DISABLE_SKINNING_MATRICES_WITH_UNITYSMR

using Oculus.Skinning;
using Oculus.Skinning.GpuSkinning;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Avatar2
{
    public partial class OvrAvatarEntity : MonoBehaviour
    {
        // TODO: Move more rendering logic here

        public bool isStaticMesh => SkinningType == SkinningConfig.NONE;
        private bool UseGpuSkinning => SkinningType == SkinningConfig.OVR_UNITY_GPU_FULL;
        private bool UseGpuMorphTargets => SkinningType == SkinningConfig.OVR_UNITY_GPU_FULL;

        private bool UseMotionSmoothingRenderer => MotionSmoothingSettings == MotionSmoothingOptions.USE_CONFIG_SETTING ? GpuSkinningConfiguration.Instance.MotionSmoothing : MotionSmoothingSettings == MotionSmoothingOptions.FORCE_ON;

        private Transform _probeAnchor = null;

        /////////////////////////////////////////////////
        //:: Private Functions

        #region Config

        public enum SkinningConfig
        {
            DEFAULT,
            NONE,

            UNITY,

            OVR_UNITY_GPU_FULL,

            // TODO: Turn into flags, need to genericize EntityFeatureDrawer

            // OVR_UNITY_GPU_JOINTS_ONLY,
            // OVR_UNITY_GPU_MORPH_TARGETS,

            // OVR_UNITY_CPU,

            // OVR_NATIVE_CPU,
            // OVR_NATIVE_GPU
        }

        [SerializeField]
        private SkinningConfig SkinningType = SkinningConfig.DEFAULT;

        private enum MotionSmoothingOptions
        {
            USE_CONFIG_SETTING,
            FORCE_ON,
            FORCE_OFF,
        }

        [Tooltip("Enable/disable motion smoothing for an individual OvrAvatarEntity. By default uses the setting specified in the GpuSkinningConfiguration.")]
        [SerializeField]
        private MotionSmoothingOptions MotionSmoothingSettings = MotionSmoothingOptions.USE_CONFIG_SETTING;

        [SerializeField]
        private bool _hidden = false;

        private bool UseAppSwRenderer => GpuSkinningConfiguration.Instance.SupportApplicationSpaceWarp;

        // TODO: This should be keyed by primitiveId instead of instance?
        private readonly Dictionary<OvrAvatarPrimitive, OvrAvatarSkinnedRenderable> _skinnedRenderables =
            new Dictionary<OvrAvatarPrimitive, OvrAvatarSkinnedRenderable>();

        public bool Hidden
        {
            get => _hidden;
            set
            {
                _hidden = value;
                SetActiveView(GetActiveView());
            }
        }

        // Called by CreateRenderable, setup skinning type based on primitive and configuration
        private OvrAvatarRenderable AddRenderableComponent(GameObject primitiveObject, OvrAvatarPrimitive primitive)
        {
            bool hasSkinningData = primitive.joints.Length > 0;
            if (!hasSkinningData)
            {
                SkinningType = SkinningConfig.NONE;
            }

            var renderable = AddRenderableComponent(primitiveObject);

            if (!(_probeAnchor is null))
            {
                renderable.rendererComponent.probeAnchor = _probeAnchor;
            }

            renderable.ApplyMeshPrimitive(primitive);

            return renderable;
        }

        private OvrAvatarRenderable AddRenderableComponent(GameObject primitiveObject)
        {
            switch (SkinningType)
            {
                case SkinningConfig.DEFAULT:
                case SkinningConfig.UNITY:
                    return primitiveObject.AddComponent<OvrAvatarUnitySkinnedRenderable>();

                case SkinningConfig.OVR_UNITY_GPU_FULL:
                    if (!UseMotionSmoothingRenderer)
                    {
                        if (!UseAppSwRenderer)
                        {
                            return primitiveObject.AddComponent<OvrAvatarGpuSkinnedRenderable>();
                        }

                        return primitiveObject.AddComponent<OvrAvatarGpuSkinnedMvRenderable>();
                    }
                    else
                    {
                        if (!UseAppSwRenderer)
                        {
                            var renderable = primitiveObject.AddComponent<OvrAvatarGpuInterpolatedSkinnedRenderable>();
                            renderable.InterpolationValueProvider = _interpolationValueProvider;

                            return renderable;
                        }
                        else
                        {
                            var renderable = primitiveObject.AddComponent<OvrAvatarGpuInterpolatedSkinnedMvRenderable>();
                            renderable.InterpolationValueProvider = _interpolationValueProvider;

                            return renderable;
                        }
                    }
                case SkinningConfig.NONE:
                    return primitiveObject.AddComponent<OvrAvatarRenderable>();

                default:
                    throw new ArgumentException($"Invalid SkinningType: {(int)SkinningType}");

            }
        }


        private void ValidateSkinningType()
        {
            GpuSkinningConfiguration.HandleDefaultConfig(ref SkinningType);

            if (SkinningType == SkinningConfig.OVR_UNITY_GPU_FULL && !OvrAvatarManager.Instance.OvrGPUSkinnerSupported)
            {
#if UNITY_ANDROID && !UNITY_2019_3_OR_NEWER
                OvrAvatarLog.LogError("OvrGpuSkinning is unavailable on Android before Unity 2019.3", logScope, this);
#else
                if (!OvrAvatarManager.Instance.gpuSkinningShaderLevelSupported)
                {
                    OvrAvatarLog.LogInfo("OvrGpuSkinning unsupported on this hardware, attempting fallback to UnitySMR", logScope, this);
                }
                else
                {
                    OvrAvatarLog.LogWarning("OvrGpuSkinning unsupported, attempting fallback to UnitySMR", logScope, this);
                }
#endif   // !UNITY_ANDROID || UNITY_2019_3_OR_NEWER

                SkinningType = SkinningConfig.UNITY;
            }

            if (SkinningType == SkinningConfig.UNITY && !OvrAvatarManager.Instance.UnitySMRSupported)
            {
                if (!OvrAvatarManager.Instance.OvrGPUSkinnerSupported)
                {
                    OvrAvatarLog.LogError("UnitySMR unsupported with no fallback", logScope, this);

                    SkinningType = SkinningConfig.NONE;
                }
                else
                {
                    OvrAvatarLog.LogWarning("UnitySMR unsupported, falling back to OvrGPU", logScope, this);

                    SkinningType = SkinningConfig.OVR_UNITY_GPU_FULL;
                }
            }

            // Intentional SkinningConfig.NONE config should log no warnings/errors
        }

        #endregion

        public void SetProbeAnchor(Transform anchor)
        {
            // TODO: Confirm this catches renderables added later
            _probeAnchor = anchor;

            foreach (var meshNodeKVP in _meshNodes)
            {
                foreach (var primRenderData in meshNodeKVP.Value)
                {
                    var renderable = primRenderData.renderable;
                    if (!renderable) { continue; }
                    var rend = renderable.rendererComponent;
                    rend.probeAnchor = anchor;
                }
            }
        }

        /**
         * Configures the material for this renderable
         * with the last known material state.
         */
        private void InitializeRenderable(OvrAvatarRenderable renderable)
        {
            ConfigureRenderableMaterial(renderable);
            OnRenderableCreated(renderable);
        }
    }
}
