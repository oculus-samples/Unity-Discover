using Oculus.Avatar2;
using UnityEngine;
using static Oculus.Skinning.GpuSkinning.OvrComputeMeshAnimator;

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
     * is set to SkinningConfig.OVR_COMPUTE, "motion smoothing" and "support application spacewarp"
     * is enabled in the GPU skinning configuration.
     *
     * @see OvrAvatarSkinnedRenderable
     * @see OvrAvatarComputeSkinnedRenderable
     * @see OvrGpuSkinningConfiguration.MotionSmoothing
     */
    public class OvrAvatarComputeInterpolatedSkinnedMvRenderable : OvrAvatarComputeSkinnedRenderableBase
    {
        // Number of animation frames required to be considered "completely valid"
        private const int NUM_ANIM_FRAMES_NEEDED_FOR_CURRENT_RENDER = 2;

        protected override string LogScope => nameof(OvrAvatarComputeInterpolatedSkinnedMvRenderable);

        internal override MaxOutputFrames MeshAnimatorOutputFrames => MaxOutputFrames.THREE;
        protected override bool InterpolateAttributes => true;

        public IInterpolationValueProvider InterpolationValueProvider { get; set; }

        private CAPI.ovrAvatar2Transform _prevSkinningOrigin;
        private CAPI.ovrAvatar2Transform _currentSkinningOrigin;

        private int _numValidAnimationFrames;
        private bool _hasValidPreviousRenderFrame;

        private SkinningOutputFrame _writeDestination = SkinningOutputFrame.FrameZero;
        private SkinningOutputFrame _prevAnimFrameWriteDest = SkinningOutputFrame.FrameZero;

        private SkinningOutputFrame _renderFrameWriteDest = SkinningOutputFrame.FrameZero;
        private SkinningOutputFrame _prevRenderFrameWriteDest = SkinningOutputFrame.FrameZero;

        private float _renderFrameLerpVal;
        private float _prevRenderFrameLerpVal;

        protected override void Dispose(bool isDisposing)
        {
            InterpolationValueProvider = null;

            base.Dispose(isDisposing);
        }

        public override void UpdateSkinningOrigin(in CAPI.ovrAvatar2Transform skinningOrigin)
        {
            // Should be called every "animation frame"

            _prevSkinningOrigin = IsAnimationDataCompletelyValid ? skinningOrigin : _currentSkinningOrigin;

            _currentSkinningOrigin = skinningOrigin;
        }

        protected override void OnAnimationEnabledChanged(bool isNowEnabled)
        {
            if (isNowEnabled)
            {
                // Reset valid frame counter on re-enabling animation
                _numValidAnimationFrames = 0;
                _writeDestination = SkinningOutputFrame.FrameOne;
                _prevAnimFrameWriteDest = _writeDestination;
            }
        }

        protected virtual void OnEnable()
        {
            // No animation data yet since object just enabled (becoming visible)
            _renderFrameLerpVal = 0.0f;
            _prevRenderFrameLerpVal = 0.0f;
            _hasValidPreviousRenderFrame = false;

            _renderFrameWriteDest = _writeDestination;
            _prevRenderFrameWriteDest = _writeDestination;
        }

        internal override void AnimationFrameUpdate()
        {
            // Replaces logic in base class

            // ASSUMPTION: This call will always follow calls to update morphs and/or skinning.
            // With that assumption, new data will be written by the morph target combiner and/or skinner, so there
            // will be valid data at end of frame.
            bool wasAnimDataCompletedValid = IsAnimationDataCompletelyValid;

            if (_numValidAnimationFrames < NUM_ANIM_FRAMES_NEEDED_FOR_CURRENT_RENDER)
            {
                _numValidAnimationFrames++;
            }

            if (!wasAnimDataCompletedValid && IsAnimationDataCompletelyValid)
            {
                OnAnimationDataCompleted();
            }

            // Update "current" and "previous" animation frame related data
            _prevAnimFrameWriteDest = _writeDestination;
            _writeDestination = GetNextOutputFrame(_writeDestination, MeshAnimatorOutputFrames);

            MeshAnimator?.SetWriteDestinationInDynamicBuffer(_writeDestination);
            OvrAvatarManager.Instance.GpuSkinningController.AddActivateComputeAnimator(MeshAnimator);
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

            // Update "current" and "previous" render frame related data
            // Slam "previous" values to be current values if there was no previous render frame
            if (!_hasValidPreviousRenderFrame)
            {
                _prevRenderFrameWriteDest = _writeDestination;
                _prevRenderFrameLerpVal = lerpValue;
                _hasValidPreviousRenderFrame = true;
            }
            else
            {
                _prevRenderFrameWriteDest = _renderFrameWriteDest;
                _prevRenderFrameLerpVal = _renderFrameLerpVal;
            }

            _renderFrameWriteDest = _writeDestination;
            _renderFrameLerpVal = lerpValue;

            InterpolateSkinningOrigin(lerpValue);
            SetAnimationInterpolationValuesInMaterial(lerpValue);
        }

        private void SetAnimationInterpolationValuesInMaterial(float lerpValue)
        {
            // Update the interpolation value
            rendererComponent.GetPropertyBlock(MatBlock);

            MatBlock.SetInt(PropIds.AttributeOutputLatestAnimFrameEntryOffset, (int)_writeDestination);
            MatBlock.SetInt(PropIds.AttributeOutputPrevAnimFrameEntryOffset, (int)_prevAnimFrameWriteDest);
            MatBlock.SetFloat(PropIds.AttributeLerpValuePropId, lerpValue);

            MatBlock.SetInt(PropIds.AttributeOutputPrevRenderFrameLatestAnimFrameOffset, (int)_renderFrameWriteDest);
            MatBlock.SetInt(PropIds.AttributeOutputPrevRenderFramePrevAnimFrameOffset, (int)_prevRenderFrameWriteDest);
            MatBlock.SetFloat(PropIds.AttributePrevRenderFrameLerpValuePropId, _prevRenderFrameLerpVal);

            rendererComponent.SetPropertyBlock(MatBlock);
        }

        private void InterpolateSkinningOrigin(float lerpValue)
        {
            // Update the "skinning origin" via lerp/slerp.
            // NOTE: This feels dirty as we are converting from `OvrAvatar2Vector3f/Quat` to Unity
            // versions just to do the lerp/slerp. Unnecessary conversions
            transform.localPosition = Vector3.Lerp(
                _prevSkinningOrigin.position,
                _currentSkinningOrigin.position,
                lerpValue);
            transform.localRotation = Quaternion.Slerp(
                _prevSkinningOrigin.orientation,
                _currentSkinningOrigin.orientation,
                lerpValue);
            transform.localScale = Vector3.Lerp(
                _prevSkinningOrigin.scale,
                _currentSkinningOrigin.scale,
                lerpValue);
        }

        internal override bool IsAnimationDataCompletelyValid => _numValidAnimationFrames >= NUM_ANIM_FRAMES_NEEDED_FOR_CURRENT_RENDER;
    }
}
