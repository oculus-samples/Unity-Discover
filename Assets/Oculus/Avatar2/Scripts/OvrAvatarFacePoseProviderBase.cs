using System;
using AOT;

namespace Oculus.Avatar2
{
    /// <summary>
    /// Base class for C# code to supply face pose data for avatar entities
    /// </summary>
    public abstract class OvrAvatarFacePoseProviderBase : OvrAvatarCallbackContextBase
    {
        private readonly OvrAvatarFacePose _facePose = new OvrAvatarFacePose();
        internal CAPI.ovrAvatar2FacePoseProvider Provider { get; }

        protected OvrAvatarFacePoseProviderBase()
        {
            var provider = new CAPI.ovrAvatar2FacePoseProvider
            {
                provider = new IntPtr(id),
                facePoseCallback = FacePoseCallback
            };
            Provider = provider;
        }

        protected abstract bool GetFacePose(OvrAvatarFacePose facePose);

        [MonoPInvokeCallback(typeof(CAPI.FacePoseCallback))]
        private static bool FacePoseCallback(out CAPI.ovrAvatar2FacePose facePose, IntPtr userContext)
        {
            try
            {
                var provider = GetInstance<OvrAvatarFacePoseProviderBase>(userContext);
                if (provider != null)
                {
                    if (provider.GetFacePose(provider._facePose))
                    {
                        facePose = provider._facePose.ToNative();
                        return true;
                    }

                    facePose = OvrAvatarFacePose.GenerateEmptyNativePose();
                    return false;
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            facePose = default;
            return false;
        }
    }
}
