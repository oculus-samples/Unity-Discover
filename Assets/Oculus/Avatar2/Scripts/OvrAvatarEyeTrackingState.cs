using System;
using UnityEngine;

namespace Oculus.Avatar2
{
    /// <summary>
    /// Data needed to drive eye tracking of an avatar.
    /// </summary>
    public sealed class OvrAvatarEyesPose
    {
        public CAPI.ovrAvatar2EyePose leftEye;
        public CAPI.ovrAvatar2EyePose rightEye;

        #region Native Conversions
        internal CAPI.ovrAvatar2EyesPose ToNative()
        {
            var native = new CAPI.ovrAvatar2EyesPose();
            native.leftEye = leftEye;
            native.rightEye = rightEye;
            return native;
        }

        internal void FromNative(in CAPI.ovrAvatar2EyesPose native)
        {
            leftEye = native.leftEye;
            rightEye = native.rightEye;
        }
        #endregion
    }
}
