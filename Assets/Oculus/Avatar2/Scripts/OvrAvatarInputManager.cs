using UnityEngine;

/// @file OvrAvatarInputManager.cs

namespace Oculus.Avatar2
{
    /**
     * Base class for setting tracking input on an avatar entity.
     * Allows you to select which components to use for tracking in
     * the Unity Editor.
     * @see OvrAvatarBodyTrackingBehavior
     */
    public abstract class OvrAvatarInputManager : OvrAvatarBodyTrackingBehavior
    {
        private OvrAvatarBodyTrackingContext _bodyTracking = null;

        /**
         * The current body tracking implementation.
         * @see OvrAvatarBodyTrackingContext
         */
        public OvrAvatarBodyTrackingContext BodyTracking
        {
            private set => _bodyTracking = value;
            get
            {
                InitializeTracking();
                return _bodyTracking;
            }
        }

        [SerializeField, Tooltip("This adds one frame of latency to the body solver. In exchange saves some main thread CPU time")]
        private bool _useAsyncBodySolver = false;

        private OvrAvatarBodyTrackingContextBase _trackingContext;

        /**
         * The current body tracking implementation.
         * Gets the body tracking information from sensors and applies it to the skeleton.
         * Accessing the body tracking context also initializes body tracking.
         * @see OvrAvatarBodyTrackingContextBase
         */
        public override OvrAvatarBodyTrackingContextBase TrackingContext
        {
            get
            {
                InitializeTracking();

                return _trackingContext;
            }
        }

        protected void InitializeTracking()
        {
            if (_trackingContext == null && OvrAvatarManager.initialized)
            {
                if (_bodyTracking == null)
                {
                    _bodyTracking = OvrAvatarBodyTrackingContext.Create(_useAsyncBodySolver);
                }
                _trackingContext = _bodyTracking;
            }
        }

        protected void OnDestroy()
        {
            OnDestroyCalled();

            BodyTracking?.Dispose();
            BodyTracking = null;
        }

        /**
         * Called from *OnDestroy* to provide a way
         * for subclasses to intercept destruction and
         * do their own cleanup.
         * The default implementation does nothing.
         */
        protected virtual void OnDestroyCalled()
        {
        }

        /**
         * Returns the position and orientation of the headset
         * and controllers in T-Pose.
         * Useful for initial calibration or testing.
         * @see CAPI.ovrAvatar2InputTransforms
         */
        protected unsafe OvrAvatarInputTrackingState GetTPose()
        {
            var transforms = new OvrAvatarInputTrackingState
            {
                headsetActive = true,
                leftControllerActive = true,
                rightControllerActive = true,
                leftControllerVisible = true,
                rightControllerVisible = true,
                headset =
                {
                    position = new CAPI.ovrAvatar2Vector3f() {x = 0, y = 1.6169891f, z = 0},
                    orientation = new CAPI.ovrAvatar2Quatf() {x = 0, y = 0, z = 0, w = -1f},
                    scale = new CAPI.ovrAvatar2Vector3f() {x = 1f, y = 1f, z = 1f}
                },
                leftController =
                {
                    position = new CAPI.ovrAvatar2Vector3f() {x = -0.79f, y = 1.37f, z = 0},
                    orientation =
                        new CAPI.ovrAvatar2Quatf() {x = 0, y = -0.70710678118f, z = 0, w = -0.70710678118f},
                    scale = new CAPI.ovrAvatar2Vector3f() {x = 1f, y = 1f, z = 1f}
                },
                rightController =
                {
                    position = new CAPI.ovrAvatar2Vector3f() {x = 0.79f, y = 1.37f, z = 0},
                    orientation = new CAPI.ovrAvatar2Quatf() {x = 0, y = 0.70710678118f, z = 0, w = -0.70710678118f},
                    scale = new CAPI.ovrAvatar2Vector3f() {x = 1f, y = 1f, z = 1f}
                }
            };

            return transforms;
        }
    }
}
