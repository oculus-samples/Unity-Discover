using System;

namespace Oculus.Avatar2
{
    public struct OvrAvatarInputTrackingState
    {
        public bool headsetActive;
        public bool leftControllerActive;
        public bool rightControllerActive;
        public bool leftControllerVisible;
        public bool rightControllerVisible;
        public CAPI.ovrAvatar2Transform headset;
        public CAPI.ovrAvatar2Transform leftController;
        public CAPI.ovrAvatar2Transform rightController;

        #region Native Conversions
        internal CAPI.ovrAvatar2InputTrackingState ToNative()
        {
            return new CAPI.ovrAvatar2InputTrackingState
            {
                headsetActive = headsetActive,
                leftControllerActive = leftControllerActive,
                rightControllerActive = rightControllerActive,
                leftControllerVisible = leftControllerVisible,
                rightControllerVisible = rightControllerVisible,
                headset = headset,
                leftController = leftController,
                rightController = rightController,
            };
        }

        internal void FromNative(ref CAPI.ovrAvatar2InputTrackingState native)
        {
            headsetActive = native.headsetActive;
            leftControllerActive = native.leftControllerActive;
            rightControllerActive = native.rightControllerActive;
            leftControllerVisible = native.leftControllerVisible;
            rightControllerVisible = native.rightControllerVisible;
            headset = native.headset;
            leftController = native.leftController;
            rightController = native.rightController;
        }
        #endregion
    }
}
