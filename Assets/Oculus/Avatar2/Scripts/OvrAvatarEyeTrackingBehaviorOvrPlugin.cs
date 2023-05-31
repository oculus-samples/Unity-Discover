using Oculus.Avatar2;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Avatar2
{
    /// <summary>
    /// EyePose behavior that enables eye tracking through OVRPlugin.
    /// </summary>
    public class OvrAvatarEyeTrackingBehaviorOvrPlugin : OvrAvatarEyePoseBehavior
    {
        private OvrAvatarEyePoseProviderBase _eyePoseProvider;

        public override OvrAvatarEyePoseProviderBase EyePoseProvider
        {
            get
            {
                InitializeEyePoseProvider();

                return _eyePoseProvider;
            }
        }

        private void InitializeEyePoseProvider()
        {
            if (_eyePoseProvider == null && OvrAvatarManager.Instance != null)
            {
                OvrAvatarManager.Instance.RequestEyeTrackingPermission();
                if (OvrAvatarManager.Instance.OvrPluginEyePoseProvider != null)
                {
                    OvrAvatarLog.LogInfo("Eye tracking service available");
                    _eyePoseProvider = OvrAvatarManager.Instance.OvrPluginEyePoseProvider;
                }
                else
                {
                    OvrAvatarLog.LogWarning("Eye tracking service unavailable");
                }
            }
        }
    }
}
