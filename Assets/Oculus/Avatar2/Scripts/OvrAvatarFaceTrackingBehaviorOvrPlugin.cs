using Oculus.Avatar2;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;

namespace Oculus.Avatar2
{
    /// <summary>
    /// FaceTracking behavior that enables face tracking through OVRPlugin.
    /// </summary>
    public class OvrAvatarFaceTrackingBehaviorOvrPlugin : OvrAvatarFacePoseBehavior
    {
        private OvrAvatarFacePoseProviderBase _facePoseProvider;

        public override OvrAvatarFacePoseProviderBase FacePoseProvider
        {
            get
            {
                InitializeFacePoseProvider();

                return _facePoseProvider;
            }
        }

        private void InitializeFacePoseProvider()
        {
            if (_facePoseProvider == null && OvrAvatarManager.Instance != null)
            {
                OvrAvatarManager.Instance.RequestFaceTrackingPermission();
                if (OvrAvatarManager.Instance.OvrPluginFacePoseProvider != null)
                {
                    OvrAvatarLog.LogInfo("Face tracking service available");
                    _facePoseProvider = OvrAvatarManager.Instance.OvrPluginFacePoseProvider;
                }
                else
                {
                    OvrAvatarLog.LogWarning("Face tracking service unavailable");
                }
            }
        }
    }
}
