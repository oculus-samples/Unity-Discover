using Oculus.Avatar2;

namespace Oculus.Skinning.GpuSkinning
{
    /**
     * Component that encapsulates the meshes of a skinned avatar.
     * This component implements skinning using the Avatar SDK
     * and uses the GPU. It performs skinning on every avatar
     * at each frame. It is used when the skinning configuration
     * is set to SkinningConfig.OVR_UNITY_GPU_COMPUTE and motion smoothing
     * is *not* enabled in the GPU skinning configuration.
     *
     * @see OvrAvatarSkinnedRenderable
     * @see OvrAvatarComputeSkinnedRenderable
     * @see OvrGpuSkinningConfiguration.MotionSmoothing
     */
    public class OvrAvatarComputeSkinnedRenderable : OvrAvatarComputeSkinnedRenderableBase
    {
        protected override string LogScope => nameof(OvrAvatarComputeSkinnedRenderable);

        // Only need a single output frame
        internal override OvrComputeMeshAnimator.MaxOutputFrames MeshAnimatorOutputFrames => OvrComputeMeshAnimator.MaxOutputFrames.ONE;

        // Only 1 "animation frame" is required for this renderable, so a boolean is sufficient
        // to serve as "is valid
        private bool _isAnimationDataCompletelyValid;

        protected override void OnAnimationEnabledChanged(bool isNowEnabled)
        {
            if (isNowEnabled)
            {
                _isAnimationDataCompletelyValid = false;
            }
        }

        internal override void AnimationFrameUpdate()
        {
            // ASSUMPTION: This call will always be followed by calls to update morphs and/or skinning.
            // With that assumption, new data will be written by the morph target combiner and/or skinner, so there
            // will be valid data at end of frame.
            _isAnimationDataCompletelyValid = true;
            OnAnimationDataCompleted();

            OvrAvatarManager.Instance.GpuSkinningController.AddActivateComputeAnimator(MeshAnimator);
        }

        internal override void RenderFrameUpdate()
        {
            // Intentionally empty
        }

        internal override bool IsAnimationDataCompletelyValid => _isAnimationDataCompletelyValid;
    }
}
