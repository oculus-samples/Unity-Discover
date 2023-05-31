using AOT;
using System;

namespace Oculus.Avatar2
{
    ///
    /// C# wrapper around OvrBody stand alone solver
    ///
    public sealed class OvrAvatarBodyTrackingContext : OvrAvatarBodyTrackingContextBase, IOvrAvatarNativeBodyTracking
    {
        private const string logScope = "BodyTrackingContext";

        private IntPtr _context;
        private IOvrAvatarHandTrackingDelegate _handTrackingDelegate;
        private IOvrAvatarInputTrackingDelegate _inputTrackingDelegate;
        private IOvrAvatarInputControlDelegate _inputControlDelegate;
        private readonly CAPI.ovrAvatar2TrackingDataContext? _callbacks;
        private readonly OvrAvatarTrackingHandsState _handState = new OvrAvatarTrackingHandsState();
        private OvrAvatarInputControlState _inputControlState = new OvrAvatarInputControlState();
        private OvrAvatarInputTrackingState _inputTrackingState = new OvrAvatarInputTrackingState();
        private readonly CAPI.ovrAvatar2TrackingDataContextNative _nativeContext;

        CAPI.ovrAvatar2TrackingDataContextNative IOvrAvatarNativeBodyTracking.NativeDataContext
        {
            get => _nativeContext;
        }

        public IntPtr BodyTrackingContextPtr => _context;

        public IOvrAvatarHandTrackingDelegate HandTrackingDelegate
        {
            get => _handTrackingDelegate;
            set
            {
                _handTrackingDelegate = value ?? OvrAvatarManager.Instance.DefaultHandTrackingDelegate;

                if (_handTrackingDelegate is IOvrAvatarNativeHandDelegate nativeHandDelegate)
                {
                    var nativeContext = nativeHandDelegate.NativeContext;
                    CAPI.ovrAvatar2Body_SetHandTrackingContextNative(_context, nativeContext)
                        .EnsureSuccess("ovrAvatar2Body_SetHandTrackingContextNative", logScope);
                }
                else
                {
                    // Set hand callbacks
                    var handContext = new CAPI.ovrAvatar2HandTrackingDataContext();
                    if (_handTrackingDelegate != null)
                    {
                        handContext.context = new IntPtr(id);
                        handContext.handTrackingCallback = HandTrackingCallback;
                    };

                    CAPI.ovrAvatar2Body_SetHandTrackingContext(_context, handContext)
                        .EnsureSuccess("ovrAvatar2Body_SetHandTrackingContext", logScope);
                }
            }
        }

        public IOvrAvatarInputTrackingDelegate InputTrackingDelegate
        {
            get => _inputTrackingDelegate;
            set
            {
                _inputTrackingDelegate = value;

                {
                    var inputContext = new CAPI.ovrAvatar2InputTrackingContext();
                    if (_inputTrackingDelegate != null)
                    {
                        inputContext.context = new IntPtr(id);
                        inputContext.inputTrackingCallback = InputTrackingCallback;
                    };

                    CAPI.ovrAvatar2Body_SetInputTrackingContext(_context, inputContext)
                        .EnsureSuccess("ovrAvatar2Body_SetInputTrackingContext", logScope);
                }
            }
        }

        public OvrAvatarInputTrackingState InputTrackingState { get => _inputTrackingState; }

        public IOvrAvatarInputControlDelegate InputControlDelegate
        {
            get => _inputControlDelegate;
            set
            {
                _inputControlDelegate = value;

                {
                    var inputContext = new CAPI.ovrAvatar2InputControlContext();
                    if (_inputControlDelegate != null)
                    {
                        inputContext.context = new IntPtr(id);
                        inputContext.inputControlCallback = InputControlCallback;
                    }

                    CAPI.ovrAvatar2Body_SetInputControlContext(_context, inputContext)
                        .EnsureSuccess("ovrAvatar2Body_SetInputControlContext", logScope);
                }
            }
        }

        public OvrAvatarInputControlState InputControlState { get => _inputControlState; }

        public static OvrAvatarBodyTrackingContext Create(bool runAsync)
        {
            OvrAvatarBodyTrackingContext context = null;
            try
            {
                context = new OvrAvatarBodyTrackingContext(runAsync);
            }
            catch (Exception)
            {
                context?.Dispose();
                context = null;
            }

            return context;
        }

        private OvrAvatarBodyTrackingContext(bool runAsync)
        {
            if (!CAPI.ovrAvatar2Body_CreateProvider(runAsync ? CAPI.ovrAvatar2BodyProviderCreateFlags.RunAsync : 0, out _context)
                    .EnsureSuccess("ovrAvatar2Body_CreateProvider", logScope))
            {
                // Not sure which exception type is best
                throw new Exception("Failed to create body tracking context");
            }

            HandTrackingDelegate = OvrAvatarManager.Instance.DefaultHandTrackingDelegate;

            _callbacks = CreateBodyDataContext();

            if (CAPI.ovrAvatar2Body_InitializeDataContextNative(_context, out var nativeContext)
                .EnsureSuccess("ovrAvatar2Body_InitializeDataContextNative", logScope))
            {
                _nativeContext = nativeContext;
            }
        }

        public void SetTransformOffset(CAPI.ovrAvatar2BodyMarkerTypes type, ref CAPI.ovrAvatar2Transform offset)
        {
            CAPI.ovrAvatar2Body_SetOffset(_context, type, offset)
                .EnsureSuccess("ovrAvatar2Body_SetOffset", logScope);
        }

        private CAPI.ovrAvatar2TrackingDataContext? CreateBodyDataContext()
        {
            if (CAPI.ovrAvatar2Body_InitializeDataContext(_context, out var trackingContext)
                .EnsureSuccess("ovrAvatar2Body_InitializeDataContext", logScope))
            {
                return trackingContext;
            }
            else
            {
                return null;
            }
        }

        private void ReleaseUnmanagedResources()
        {
            if (_context == IntPtr.Zero) return;
            // Release unmanaged resources here
            CAPI.ovrAvatar2Body_DestroyProvider(_context)
                .EnsureSuccess("ovrAvatar2Body_DestroyProvider", logScope);

            _context = IntPtr.Zero;
        }

        protected override void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            base.Dispose(disposing);
        }

        [MonoPInvokeCallback(typeof(CAPI.HandStateCallback))]
        private static bool HandTrackingCallback(out CAPI.ovrAvatar2HandTrackingState handsState, IntPtr context)
        {
            try
            {
                var bodyContext = GetInstance<OvrAvatarBodyTrackingContext>(context);
                if (bodyContext?._handTrackingDelegate != null &&
                    bodyContext._handTrackingDelegate.GetHandData(bodyContext._handState))
                {
                    handsState = bodyContext._handState.ToNative();
                    return true;
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            handsState = default;
            return false;
        }


        [MonoPInvokeCallback(typeof(CAPI.InputTrackingCallback))]
        private static bool InputTrackingCallback(out CAPI.ovrAvatar2InputTrackingState trackingState, IntPtr userContext)
        {
            try
            {
                var bodyContext = GetInstance<OvrAvatarBodyTrackingContext>(userContext);
                if (bodyContext?._inputTrackingDelegate != null &&
                    bodyContext._inputTrackingDelegate.GetInputTrackingState(out bodyContext._inputTrackingState))
                {
                    trackingState = bodyContext._inputTrackingState.ToNative();
                    return true;
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            trackingState = default;
            return false;
        }

        [MonoPInvokeCallback(typeof(CAPI.InputControlCallback))]
        private static bool InputControlCallback(out CAPI.ovrAvatar2InputControlState controlState, IntPtr userContext)
        {
            try
            {
                var bodyContext = GetInstance<OvrAvatarBodyTrackingContext>(userContext);
                if (bodyContext?._inputControlDelegate != null &&
                    bodyContext._inputControlDelegate.GetInputControlState(out bodyContext._inputControlState))
                {
                    controlState = bodyContext._inputControlState.ToNative();
                    return true;
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            controlState = default;
            return false;
        }

        // Provides a Body State by calling into the native Body Tracking implementation
        protected override bool GetBodyState(OvrAvatarTrackingBodyState bodyState)
        {
            if (_callbacks.HasValue)
            {
                var cb = _callbacks.Value;
                if (cb.bodyStateCallback(out var nativeBodyState, cb.context))
                {
                    bodyState.FromNative(ref nativeBodyState);
                    return true;
                }
            }
            return false;
        }

        // Provides a Tracking Skeleton by calling into the native Body Tracking implementation
        protected override bool GetBodySkeleton(ref OvrAvatarTrackingSkeleton skeleton)
        {
            if (_callbacks.HasValue)
            {
                var cb = _callbacks.Value;
                if (cb.bodySkeletonCallback != null)
                {
                    var native = skeleton.GetNative();
                    var result = cb.bodySkeletonCallback(ref native, cb.context);
                    skeleton.CopyFromNative(ref native);
                    return result;
                }

            }

            return false;
        }

        // Provides a Body Pose by calling into the native Body Tracking implementation
        protected override bool GetBodyPose(ref OvrAvatarTrackingPose pose)
        {
            if (_callbacks.HasValue)
            {
                var cb = _callbacks.Value;
                if (cb.bodyPoseCallback != null)
                {
                    var native = pose.GetNative();
                    var result = cb.bodyPoseCallback(ref native, cb.context);
                    pose.CopyFromNative(ref native);
                    return result;
                }
            }

            return false;
        }
    }
}
