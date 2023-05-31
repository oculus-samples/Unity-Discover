using Oculus.Avatar2;

namespace Oculus.Skinning.GpuSkinning
{
    /**
     * Component that encapsulates the meshes of a skinned avatar.
     * This component implements skinning using the Avatar SDK
     * and uses the GPU. It performs skinning on every avatar
     * but not at every frame. Instead, it interpolates between
     * frames, reducing the performance overhead of skinning
     * when there are lots of avatars. It is used when the skinning configuration
     * is set to SkinningConfig.OVR_UNITY_GPU_COMPUTE, , motion smoothing
     * is *not* enabled, and "App Space Warp" is enabled in the GPU skinning configuration.
     *
     * @see OvrAvatarSkinnedRenderable
     * @see OvrAvatarComputeSkinnedRenderable
     * @see OvrGpuSkinningConfiguration.SupportApplicationSpacewarp
     */
    public class OvrAvatarComputeSkinnedMvRenderable : OvrAvatarComputeSkinnedRenderableBase
    {
        protected override string LogScope => nameof(OvrAvatarComputeSkinnedMvRenderable);

        // Only need 2 single output frames (one for current frame, one for previous frame)
        internal override OvrComputeMeshAnimator.MaxOutputFrames MeshAnimatorOutputFrames => OvrComputeMeshAnimator.MaxOutputFrames.TWO;

        // Only 1 "animation frame" is required for this renderable's animation frames to be valid (not interpolating)
        private bool _isAnimationFrameDataValid;
        private bool _hasValidPreviousRenderFrame;

        private SkinningOutputFrame _writeDestination = SkinningOutputFrame.FrameZero;
        private SkinningOutputFrame _prevRenderWriteDest = SkinningOutputFrame.FrameZero;

        protected override void OnAnimationEnabledChanged(bool isNowEnabled)
        {
            if (isNowEnabled)
            {
                _isAnimationFrameDataValid = false;
                _writeDestination = SkinningOutputFrame.FrameOne;
            }
        }

        protected virtual void OnEnable()
        {
            // Reset the previous render frame slice and render frame count
            _hasValidPreviousRenderFrame = false;
            _prevRenderWriteDest = _writeDestination;
        }

        internal override void AnimationFrameUpdate()
        {
            // ASSUMPTION: This call will always be followed by calls to update morphs and/or skinning.
            // With that assumption, new data will be written by the morph target combiner and/or skinner, so there
            // will be valid data at end of frame.
            _isAnimationFrameDataValid = true;

            _writeDestination = GetNextOutputFrame(_writeDestination, MeshAnimatorOutputFrames);
            MeshAnimator?.SetWriteDestinationInDynamicBuffer(_writeDestination);
            OvrAvatarManager.Instance.GpuSkinningController.AddActivateComputeAnimator(MeshAnimator);
        }

        internal override void RenderFrameUpdate()
        {
            // Need at least 1 "previous render frame"
            if (!_hasValidPreviousRenderFrame)
            {
                // Not enough render frames, just make the motion vectors "previous frame" the same
                // as the current one
                _prevRenderWriteDest = _writeDestination;
                _hasValidPreviousRenderFrame = true;
            }

            SetRenderFrameOutputSlices();

            // Update "previous frame" value for next frame
            _prevRenderWriteDest = _writeDestination;
        }

        internal override bool IsAnimationDataCompletelyValid => _isAnimationFrameDataValid;

        private void SetRenderFrameOutputSlices()
        {
            rendererComponent.GetPropertyBlock(MatBlock);

            MatBlock.SetInt(PropIds.AttributeOutputLatestAnimFrameEntryOffset, (int)_writeDestination);
            MatBlock.SetInt(PropIds.AttributeOutputPrevRenderFrameLatestAnimFrameOffset, (int)_prevRenderWriteDest);

            rendererComponent.SetPropertyBlock(MatBlock);
        }
    }
}
