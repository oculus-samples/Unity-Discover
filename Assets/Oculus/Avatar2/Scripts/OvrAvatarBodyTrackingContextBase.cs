using AOT;
using System;

/**
 * @file OvrAvatarBodyTrackingContextBase.cs
 */
namespace Oculus.Avatar2
{
    /**
     * Base class for C# code to supply tracking data for avatar entities.
     * Body tracking implementations derive from this class to transfer
     * data between Unity body tracking state and data gathered from sensors
     * from a C++ implementation.
     * @see OvrAvatarEntity.SetBodyTracking
     * @see OvrPluginTracking
     * @see CAPI.ovrAvatar2TrackingDataContext
     */
    public abstract class OvrAvatarBodyTrackingContextBase : OvrAvatarCallbackContextBase
    {
        private readonly OvrAvatarTrackingBodyState _bodyState = new OvrAvatarTrackingBodyState();
        internal CAPI.ovrAvatar2TrackingDataContext DataContext { get; }

        /**
         * Initializes the implementation-specific callbacks
         * invoked by native code to collect the current
         * tracking state, skeleton and pose.
         * @see BodyStateCallback
         * @see BodySkeletonCallback
         * @see BodyPoseCallback
         */
        protected OvrAvatarBodyTrackingContextBase()
        {
            var dataContext = new CAPI.ovrAvatar2TrackingDataContext
            {
                context = new IntPtr(id),
                bodyStateCallback = BodyStateCallback,
                bodySkeletonCallback = BodySkeletonCallback,
                bodyPoseCallback = BodyPoseCallback
            };
            DataContext = dataContext;
        }

        /**
         * Callback from Avatar SDK for the application to provide the current
         * Body Tracking State.
         *
         * The tracking state includes the position, orientation and scale
         * for the headset and controllers, buttons pressed, the type of
         * Hand tracking desired and whether the avatar is sitting or standing.
         * Body tracking implementations must override this function to provide
         * the current Body Tracking state to AvatarSDK each frame.
         * @param bodyState  where to store the generated tracking state.
         * @see OvrAvatarTrackingBodyState
         * @see OvrAvatarTrackingPose
         * @see OvrAvatarTrackingSkeleton
         * @see GetBodyPose
         */
        protected abstract bool GetBodyState(OvrAvatarTrackingBodyState bodyState);

        /**
         * Callback from Avatar SDK for the application to provide a
         * description of the skeleton used for Body Tracking, including
         * a reference pose.
         *
         * The tracking skeleton may be different than the avatar skeleton (it
         * might have a different number of bones) and the pose will be
         * retargeted within the Avatar SDK animation system.
         * Body tracking implementations must override this function to
         * describe the tracking skeleton to AvatarSDK. It will be called
         * whenever the skeleton changes (see "skeletonVersion" in
         * OvrAvatarTrackingBodyState).
         * @param skeleton  where to store the generated skeleton.
         * @see OvrAvatarTrackingPose
         * @see OvrAvatarTrackingSkeleton
         * @see GetBodyPose
         */
        protected abstract bool GetBodySkeleton(ref OvrAvatarTrackingSkeleton skeleton);

        /**
         * Callback from Avatar SDK for the application to provide a Body Pose.
         *
         * Body tracking implementations must override this function to fill
         * out the OvrAvatarTrackingPose struct to provide pose data each frame.
         * @param pose  Where to set the generated pose.
         * @see OvrAvatarTrackingPose
         * @see OvrAvatarTrackingSkeleton
         * @see GetBodySkeleton
         */
        protected abstract bool GetBodyPose(ref OvrAvatarTrackingPose pose);

        #region Static Methods

        [MonoPInvokeCallback(typeof(CAPI.BodyStateCallback))]
        private static bool BodyStateCallback(out CAPI.ovrAvatar2TrackingBodyState bodyState, IntPtr userContext)
        {
            try
            {
                var context = GetInstance<OvrAvatarBodyTrackingContextBase>(userContext);
                if (context != null)
                {
                    if (context.GetBodyState(context._bodyState))
                    {
                        bodyState = context._bodyState.ToNative();
                        return true;
                    }
                    bodyState = new CAPI.ovrAvatar2TrackingBodyState();
                    return false;
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            bodyState = new CAPI.ovrAvatar2TrackingBodyState();
            return false;
        }

        [MonoPInvokeCallback(typeof(CAPI.BodySkeletonCallback))]
        private static bool BodySkeletonCallback(ref CAPI.ovrAvatar2TrackingBodySkeleton nativeSkeleton, IntPtr userContext)
        {
            try
            {
                var context = GetInstance<OvrAvatarBodyTrackingContextBase>(userContext);
                if (context == null) return false;
                var skeleton = new OvrAvatarTrackingSkeleton(ref nativeSkeleton);
                var result = context.GetBodySkeleton(ref skeleton);
                skeleton.CopyToNative(ref nativeSkeleton);
                return result;
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            return false;
        }

        [MonoPInvokeCallback(typeof(CAPI.BodyPoseCallback))]
        private static bool BodyPoseCallback(ref CAPI.ovrAvatar2TrackingBodyPose nativePose, IntPtr userContext)
        {
            try
            {
                var context = GetInstance<OvrAvatarBodyTrackingContextBase>(userContext);
                if (context == null) return false;
                var pose = new OvrAvatarTrackingPose(ref nativePose);
                var result = context.GetBodyPose(ref pose);
                pose.CopyToNative(ref nativePose);
                return result;
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            return false;
        }

        #endregion
    }
}
