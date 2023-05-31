using System;

namespace Oculus.Avatar2
{
    public struct OvrAvatarControllerState
    {
        public CAPI.ovrAvatar2Button buttonMask;
        public CAPI.ovrAvatar2Touch touchMask;
        public float joystickX;
        public float joystickY;
        public float indexTrigger;
        public float handTrigger;
        public bool isActive;
        public bool isVisible;
    }

    public struct OvrAvatarInputControlState
    {
        public CAPI.ovrAvatar2ControllerType type;
        public OvrAvatarControllerState leftControllerState;
        public OvrAvatarControllerState rightControllerState;

        #region Native Conversions
        private CAPI.ovrAvatar2ControllerState ToNative (in OvrAvatarControllerState controller)
        {
            return new CAPI.ovrAvatar2ControllerState
            {
                buttonMask = controller.buttonMask,
                touchMask = controller.touchMask,
                joystickX = controller.joystickX,
                joystickY = controller.joystickY,
                indexTrigger = controller.indexTrigger,
                handTrigger = controller.handTrigger,
            };
        }

        internal CAPI.ovrAvatar2InputControlState ToNative()
        {
            return new CAPI.ovrAvatar2InputControlState
            {
                type = type,
                leftControllerState = ToNative(leftControllerState),
                rightControllerState = ToNative(rightControllerState),
            };
        }

        private void FromNative (in CAPI.ovrAvatar2ControllerState nativeController, ref OvrAvatarControllerState controller)
        {
            controller.buttonMask = nativeController.buttonMask;
            controller.touchMask = nativeController.touchMask;
            controller.joystickX = nativeController.joystickX;
            controller.joystickY = nativeController.joystickY;
            controller.indexTrigger = nativeController.indexTrigger;
            controller.handTrigger = nativeController.handTrigger;
        }

        internal void FromNative(ref CAPI.ovrAvatar2InputControlState native)
        {
            type = native.type;
            FromNative(native.leftControllerState, ref leftControllerState);
            FromNative(native.rightControllerState, ref rightControllerState);
        }
        #endregion
    }
}
