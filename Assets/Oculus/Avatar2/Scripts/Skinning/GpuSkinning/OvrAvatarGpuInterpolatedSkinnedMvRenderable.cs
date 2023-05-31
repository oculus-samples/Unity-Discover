using Oculus.Avatar2;
using UnityEngine;

/// @file OvrAvatarGpuInterpolatedSkinningRenderable

namespace Oculus.Skinning.GpuSkinning
{
    /**
     * Component that encapsulates the meshes of a skinned avatar.
     * This component implements skinning using the Avatar SDK
     * and uses the GPU. It performs skinning on every avatar
     * but not at every frame. Instead, it interpolates between
     * frames, reducing the performance overhead of skinning
     * when there are lots of avatars. It is used when the skinning configuration
     * is set to SkinningConfig.OVR_UNITY_GPU_FULL, motion smoothing
     * is enabled, and "App Space Warp" is enabled in the GPU skinning configuration.
     *
     * @see OvrAvatarSkinnedRenderable
     * @see OvrAvatarGpuSkinnedRenderable
     * @see OvrGpuSkinningConfiguration.MotionSmoothing
     */
    public class OvrAvatarGpuInterpolatedSkinnedMvRenderable : OvrAvatarGpuSkinnedRenderableBase
    {
        // Number of animation frames required to be considered "completely valid"
        private const int NUM_ANIM_FRAMES_NEEDED_FOR_CURRENT_RENDER = 2;

        public IInterpolationValueProvider InterpolationValueProvider { get; internal set; }

        private CAPI.ovrAvatar2Transform _skinningOriginFrameZero;
        private CAPI.ovrAvatar2Transform _skinningOriginFrameOne;

        private int _renderFrameF0;
        private float _renderFrameLerpVal;
        private int _prevRenderFrameF0;
        private float _prevRenderFrameLerpVal;

        // 2 "output depth texels" per "atlas packer" slice to interpolate between
        // and enable bilinear filtering to have hardware to the interpolation
        // between depth texels for us
        protected override string LogScope => "OvrAvatarGpuInterpolatedSkinnedMvRenderable";
        protected override FilterMode SkinnerOutputFilterMode => FilterMode.Bilinear;

        // 4 slices per "block" 2 for animation frames needed for the "current render frame"
        // and 2 slices needed for animation frames for the "previous render frame"
        protected override int SkinnerOutputDepthTexelsPerSlice => 4;
        protected override bool InterpolateAttributes => true;

        private int _numValidAnimationFrames;
        private bool _hasValidPreviousRenderFrame;

        protected override void Awake()
        {
            base.Awake();
            CopyMaterial(); // Probably not necessary due to material property block usage, but just to be safe
        }

        protected virtual void OnEnable()
        {
            // No animation data yet since object just enabled (becoming visible)
            _renderFrameLerpVal = 0.0f;
            _prevRenderFrameLerpVal = 0.0f;
            _renderFrameF0 = 0;
            _prevRenderFrameF0 = 0;
            _hasValidPreviousRenderFrame = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            InterpolationValueProvider = null;

            base.Dispose(isDisposing);
        }

        public override void UpdateSkinningOrigin(in CAPI.ovrAvatar2Transform skinningOrigin)
        {
            // Replace base implementation
            switch (SkinnerWriteDestination)
            {
                case SkinningOutputFrame.FrameZero:
                    _skinningOriginFrameZero = skinningOrigin;
                    break;
                case SkinningOutputFrame.FrameOne:
                    _skinningOriginFrameOne = skinningOrigin;
                    break;
            }
        }

        protected override void OnAnimationEnabledChanged(bool isNowEnabled)
        {
            if (isNowEnabled)
            {
                // Reset valid frame counter
                _numValidAnimationFrames = 0;
                SkinnerWriteDestination = SkinningOutputFrame.FrameOne;
            }
        }

        internal override void AnimationFrameUpdate()
        {
            // Replaces logic in base class

            // ASSUMPTION: This call will always follow calls to update morphs and/or skinning.
            // With that assumption, new data will be written by the morph target combiner and/or skinner, so there
            // will be valid data at end of frame.
            SwapWriteDestination();

            // Copy "previous 2 animation frame's" worth of data to slices 3 and 4
            // and manipulate some "render frame" internal fields to reflect that
            var outputTexture = GetOutputTexture();
            Debug.Assert(outputTexture);
            const int mipLevel = 0;

            int srcSlice = (int)SkinnerLayoutSlice + 0;
            int dstSlice = (int)SkinnerLayoutSlice + 2;
            Graphics.CopyTexture(
                outputTexture,
                srcSlice,
                mipLevel,
                SkinnerLayout.x,
                SkinnerLayout.y,
                SkinnerLayout.width,
                SkinnerLayout.height,
                outputTexture,
                dstSlice,
                mipLevel,
                SkinnerLayout.x,
                SkinnerLayout.y);

            srcSlice = (int)SkinnerLayoutSlice + 1;
            dstSlice = (int)SkinnerLayoutSlice + 3;
            Graphics.CopyTexture(
                outputTexture,
                srcSlice,
                mipLevel,
                SkinnerLayout.x,
                SkinnerLayout.y,
                SkinnerLayout.width,
                SkinnerLayout.height,
                outputTexture,
                dstSlice,
                mipLevel,
                SkinnerLayout.x,
                SkinnerLayout.y);

            _renderFrameF0 = 2;
            bool wasAnimDataCompletedValid = IsAnimationDataCompletelyValid;
            if (_numValidAnimationFrames < NUM_ANIM_FRAMES_NEEDED_FOR_CURRENT_RENDER)
            {
              _numValidAnimationFrames++;
            }
            if (!wasAnimDataCompletedValid && IsAnimationDataCompletelyValid)
            {
              OnAnimationDataCompleted();
            }
        }

        internal override void RenderFrameUpdate()
        {
            Debug.Assert(InterpolationValueProvider != null);

            float lerpValue = InterpolationValueProvider.GetRenderInterpolationValue();

            // Guard against frame interpolation value set, but insufficient animation frames available
            // by "slamming" value to 1.0 (take the latest animation data available)
            // Should hopefully not happen frequently/at all if caller manages state well (maybe on first enabling)
            if (_numValidAnimationFrames < NUM_ANIM_FRAMES_NEEDED_FOR_CURRENT_RENDER)
            {
                lerpValue = 1.0f;
            }

            if (ShouldInverseInterpolationValue)
            {
                // Convert from the 0 -> 1 interpolation value to one that "ping pongs" between
                // the slices here so that an additional GPU copy isn't needed to
                // transfer from "slice 1" to "slice 0"
                lerpValue = 1.0f - lerpValue;
            }

            _prevRenderFrameF0 = _renderFrameF0;
            _prevRenderFrameLerpVal = _renderFrameLerpVal;

            _renderFrameF0 = 0;
            _renderFrameLerpVal = lerpValue;

            if (!_hasValidPreviousRenderFrame)
            {
                // Slam "previous" values to be current values
                _prevRenderFrameF0 = _renderFrameF0;
                _prevRenderFrameLerpVal = _renderFrameLerpVal;
                _hasValidPreviousRenderFrame = true;
            }

            InterpolateSkinningOrigin(lerpValue);
            SetAnimationInterpolationValueInMaterial(lerpValue);
        }

        // Since animation frames are updated slower or at same rate as render frames, having
        // more than 2 animation frames implies having more than 2 render frames, so validity
        // can just be based on animation frames
        internal override bool IsAnimationDataCompletelyValid => _numValidAnimationFrames >= NUM_ANIM_FRAMES_NEEDED_FOR_CURRENT_RENDER;

        private void SetAnimationInterpolationValueInMaterial(float lerpValue)
        {
            // Update the depth texel value to interpolate between skinning output slices
            rendererComponent.GetPropertyBlock(MatBlock);

            MatBlock.SetFloat(U_ATTRIBUTE_TEXEL_SLICE_PROP_ID, SkinnerLayoutSlice + lerpValue);
            MatBlock.SetFloat(U_PREV_POSITION_TEXEL_SLICE_PROP_ID, SkinnerLayoutSlice + _prevRenderFrameF0 + _prevRenderFrameLerpVal);
            rendererComponent.SetPropertyBlock(MatBlock);
        }

        private void InterpolateSkinningOrigin(float lerpValue)
        {
            // Update the "skinning origin" via lerp/slerp.
            // NOTE: This feels dirty as we are converting from `OvrAvatar2Vector3f/Quat` to Unity
            // versions just to do the lerp/slerp. Unnecessary conversions
            transform.localPosition = Vector3.Lerp(
                _skinningOriginFrameZero.position,
                _skinningOriginFrameOne.position,
                lerpValue);
            transform.localRotation = Quaternion.Slerp(
                _skinningOriginFrameZero.orientation,
                _skinningOriginFrameOne.orientation,
                lerpValue);
            transform.localScale = Vector3.Lerp(
                _skinningOriginFrameZero.scale,
                _skinningOriginFrameOne.scale,
                lerpValue);
        }

        private bool ShouldInverseInterpolationValue => SkinnerWriteDestination == SkinningOutputFrame.FrameZero;
    }
}
