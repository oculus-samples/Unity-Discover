using System;
using AOT;

namespace Oculus.Avatar2
{
    /// <summary>
    /// Base class for C# code to supply eye pose data for avatar entities
    /// </summary>
    public abstract class OvrAvatarEyePoseProviderBase : OvrAvatarCallbackContextBase
    {
        private readonly OvrAvatarEyesPose _eyePose = new OvrAvatarEyesPose();

        internal CAPI.ovrAvatar2EyePoseProvider Context { get; }

        protected OvrAvatarEyePoseProviderBase()
        {
            var context = new CAPI.ovrAvatar2EyePoseProvider
            {
                provider = new IntPtr(id),
                eyePoseCallback = EyePoseCallback
            };
            Context = context;
        }

        protected abstract bool GetEyePose(OvrAvatarEyesPose eyePose);

        [MonoPInvokeCallback(typeof(CAPI.EyePoseCallback))]
        private static bool EyePoseCallback(out CAPI.ovrAvatar2EyesPose eyePose, IntPtr userContext)
        {
            try
            {
                var provider = GetInstance<OvrAvatarEyePoseProviderBase>(userContext);
                if (provider != null)
                {
                    if (provider.GetEyePose(provider._eyePose))
                    {
                        eyePose = provider._eyePose.ToNative();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            eyePose = default;
            return false;
        }
    }
}
