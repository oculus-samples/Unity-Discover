// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_AVATARS

using Oculus.Avatar2;

namespace Meta.Utilities.Input
{
    public class XRInputControlDelegate : SampleInputControlDelegate
    {
        private XRInputControlActions m_controlActions;

        public XRInputControlDelegate(XRInputControlActions controlActions) => m_controlActions = controlActions;

        public override bool GetInputControlState(out OvrAvatarInputControlState inputControlState)
        {
            if (OVRInput.GetConnectedControllers() != OVRInput.Controller.None)
                return base.GetInputControlState(out inputControlState);

            inputControlState = default;
            UpdateControllerInput(ref inputControlState.leftControllerState, ref m_controlActions.LeftController);
            UpdateControllerInput(ref inputControlState.rightControllerState, ref m_controlActions.RightController);
            return true;
        }

        private void UpdateControllerInput(ref OvrAvatarControllerState controllerState, ref XRInputControlActions.Controller controller)
        {
            controllerState.buttonMask = 0;
            controllerState.touchMask = 0;

            // Button Press
            if (controller.ButtonOne.action.ReadValue<float>() > 0.5f)
            {
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.One;
            }
            if (controller.ButtonTwo.action.ReadValue<float>() > 0.5f)
            {
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.Two;
            }
            if (controller.ButtonThree.action.ReadValue<float>() > 0.5f)
            {
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.Three;
            }

            // Button Touch
            if (controller.TouchOne.action.ReadValue<float>() > 0.5f)
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.One;
            }
            if (controller.TouchTwo.action.ReadValue<float>() > 0.5f)
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.Two;
            }
            if (controller.TouchPrimaryThumbstick.action.ReadValue<float>() > 0.5f)
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.Joystick;
            }

            // Trigger
            controllerState.indexTrigger = controller.AxisIndexTrigger.action.ReadValue<float>();

            // Grip
            controllerState.handTrigger = controller.AxisHandTrigger.action.ReadValue<float>();
        }
    }
}

#endif
