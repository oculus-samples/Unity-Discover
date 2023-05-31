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
     * is set to SkinningConfig.OVR_UNITY_GPU_FULL and motion smoothing
     * is enabled in the GPU skinning configuration.
     *
     * @see OvrAvatarSkinnedRenderable
     * @see OvrAvatarGpuSkinnedRenderable
     * @see OvrGpuSkinningConfiguration.MotionSmoothing
     */
    public class OvrAvatarGpuInterpolatedSkinnedRenderable : OvrAvatarGpuSkinnedRenderableBase
    {
        // Number of animation frames required to be considered "completely valid"
        private const int NUM_ANIM_FRAMES_NEEDED_FOR_CURRENT_RENDER = 2;

        public IInterpolationValueProvider InterpolationValueProvider { get; internal set; }

        private CAPI.ovrAvatar2Transform _skinningOriginFrameZero;
        private CAPI.ovrAvatar2Transform _skinningOriginFrameOne;

        // 2 "output depth texels" per "atlas packer" slice to interpolate between
        // and enable bilinear filtering to have hardware to the interpolation
        // between depth texels for us
        protected override string LogScope => "OvrAvatarGpuInterpolatedSkinnedRenderable";
        protected override FilterMode SkinnerOutputFilterMode => FilterMode.Bilinear;
        protected override int SkinnerOutputDepthTexelsPerSlice => 2;
        protected override bool InterpolateAttributes => true;

        private int _numValidAnimationFrames;

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
                // Reset valid frame counter on re-enabling animation
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

            // Guard against insufficient animation frames available
            // by "slamming" value to be 1.0 ("the newest value").
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

            InterpolateSkinningOrigin(lerpValue);
            SetAnimationInterpolationValueInMaterial(lerpValue);
        }

        internal override bool IsAnimationDataCompletelyValid => _numValidAnimationFrames >= NUM_ANIM_FRAMES_NEEDED_FOR_CURRENT_RENDER;

        private void SetAnimationInterpolationValueInMaterial(float lerpValue)
        {
            // Update the depth texel value to interpolate between skinning output slices
            rendererComponent.GetPropertyBlock(MatBlock);

            MatBlock.SetFloat(U_ATTRIBUTE_TEXEL_SLICE_PROP_ID, SkinnerLayoutSlice + lerpValue);
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
