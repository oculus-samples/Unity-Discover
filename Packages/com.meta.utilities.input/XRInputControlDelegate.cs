// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_AVATARS

#if USING_XR_MANAGEMENT && (USING_XR_SDK_OCULUS || USING_XR_SDK_OPENXR) && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

using Oculus.Avatar2;

#if USING_XR_SDK
using Button = OVRInput.Button;
using Touch = OVRInput.Touch;
#endif

namespace Meta.Utilities.Input
{
    public class XRInputControlDelegate : OvrAvatarInputControlDelegate
    {
        private XRInputControlActions m_controlActions;

        public XRInputControlDelegate(XRInputControlActions controlActions) => m_controlActions = controlActions;

        public override bool GetInputControlState(out OvrAvatarInputControlState inputControlState)
        {
            if (OVRInput.GetConnectedControllers() != OVRInput.Controller.None)
            {
                // Based of Avatar Samples SampleInputControlDelegate
                inputControlState = new OvrAvatarInputControlState();
                inputControlState.type = GetControllerType();

#if USING_XR_SDK
                UpdateControllerInput(ref inputControlState.leftControllerState, OVRInput.Controller.LTouch);
                UpdateControllerInput(ref inputControlState.rightControllerState, OVRInput.Controller.RTouch);
#endif
            }

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
        
        // Based of Avatar Samples SampleInputControlDelegate
#if USING_XR_SDK
    private void UpdateControllerInput(ref OvrAvatarControllerState controllerState, OVRInput.Controller controller)
    {
        controllerState.buttonMask = 0;
        controllerState.touchMask = 0;

        // Button Press
        if (OVRInput.Get(Button.One, controller))
        {
            controllerState.buttonMask |= CAPI.ovrAvatar2Button.One;
        }
        if (OVRInput.Get(Button.Two, controller))
        {
            controllerState.buttonMask |= CAPI.ovrAvatar2Button.Two;
        }
        if (OVRInput.Get(Button.Three, controller))
        {
            controllerState.buttonMask |= CAPI.ovrAvatar2Button.Three;
        }
        if (OVRInput.Get(Button.PrimaryThumbstick, controller))
        {
            controllerState.buttonMask |= CAPI.ovrAvatar2Button.Joystick;
        }

        // Button Touch
        if (OVRInput.Get(Touch.One, controller))
        {
            controllerState.touchMask |= CAPI.ovrAvatar2Touch.One;
        }
        if (OVRInput.Get(Touch.Two, controller))
        {
            controllerState.touchMask |= CAPI.ovrAvatar2Touch.Two;
        }
        if (OVRInput.Get(Touch.PrimaryThumbstick, controller))
        {
            controllerState.touchMask |= CAPI.ovrAvatar2Touch.Joystick;
        }
        if (OVRInput.Get(Touch.PrimaryThumbRest, controller))
        {
            controllerState.touchMask |= CAPI.ovrAvatar2Touch.ThumbRest;
        }

        // Trigger
        controllerState.indexTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
        if (OVRInput.Get(Touch.PrimaryIndexTrigger, controller))
        {
            controllerState.touchMask |= CAPI.ovrAvatar2Touch.Index;
        }
        else if (controllerState.indexTrigger <= 0f)
        {
            // TODO: Not sure if this is the correct way to do this
            controllerState.touchMask |= CAPI.ovrAvatar2Touch.Pointing;
        }

        // Grip
        controllerState.handTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller);

        // Set ThumbUp if no other thumb-touch is set.
        // TODO: Not sure if this is the correct way to do this
        if ((controllerState.touchMask & (CAPI.ovrAvatar2Touch.One | CAPI.ovrAvatar2Touch.Two |
                                          CAPI.ovrAvatar2Touch.Joystick | CAPI.ovrAvatar2Touch.ThumbRest)) == 0)
        {
            controllerState.touchMask |= CAPI.ovrAvatar2Touch.ThumbUp;
        }
    }
#endif
    }
}

#endif
