using AOT;
using System;

namespace Oculus.Avatar2
{
    public abstract class OvrAvatarInputTrackingContextBase : OvrAvatarCallbackContextBase
    {
        private OvrAvatarInputTrackingState _inputTrackingState = new OvrAvatarInputTrackingState();
        internal CAPI.ovrAvatar2InputTrackingContext Context { get; }

        protected OvrAvatarInputTrackingContextBase()
        {
            var context = new CAPI.ovrAvatar2InputTrackingContext
            {
                context = new IntPtr(id),
                inputTrackingCallback = InputTrackingCallback,
            };
            Context = context;
        }

        protected abstract bool GetInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState);

        [MonoPInvokeCallback(typeof(CAPI.InputTrackingCallback))]
        private static bool InputTrackingCallback(out CAPI.ovrAvatar2InputTrackingState inputTrackingState, IntPtr userContext)
        {
            try
            {
                var context = GetInstance<OvrAvatarInputTrackingContextBase>(userContext);
                if (context != null)
                {
                    if (context.GetInputTrackingState(out context._inputTrackingState))
                    {
                        inputTrackingState = context._inputTrackingState.ToNative();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            inputTrackingState = default;
            return false;
        }
    }
}
