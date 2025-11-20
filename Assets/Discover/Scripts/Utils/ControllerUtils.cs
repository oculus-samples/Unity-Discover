// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Discover.Utils
{
    [MetaCodeSample("Discover")]
    public static class ControllerUtils
    {
        public static float DotProductBetweenControllerAndPosition(OVRInput.Controller controller, Vector3 position)
        {
            var touchPos = OVRInput.GetLocalControllerPosition(controller);
            var touchRot = OVRInput.GetLocalControllerRotation(controller);

            var dot = Vector3.Dot((position - touchPos).normalized, touchRot * Vector3.forward);
            return dot;
        }

        public static float DotProductBetweenControllerAndEvent(
          OVRInput.Controller controller,
          PointerEventData eventData
        )
        {
            var buttonWorldPos = eventData.pointerCurrentRaycast.worldPosition;
            return DotProductBetweenControllerAndPosition(controller, buttonWorldPos);
        }

        public static Handedness GetHandFromPointerData(PointerEventData eventData)
        {
            var dotLeft = DotProductBetweenControllerAndEvent(OVRInput.Controller.LHand, eventData);
            var dotRight = DotProductBetweenControllerAndEvent(OVRInput.Controller.RHand, eventData);

            return dotLeft > dotRight ? Handedness.Left : Handedness.Right;
        }

        public static OVRInput.Controller GetControllerFromPointerData(PointerEventData eventData)
        {
            var dotLeft = DotProductBetweenControllerAndEvent(OVRInput.Controller.LTouch, eventData);
            var dotRight = DotProductBetweenControllerAndEvent(OVRInput.Controller.RTouch, eventData);

            return dotLeft > dotRight ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        }

        public static float DotProductBetweenControllerAndPose(OVRInput.Controller controller, Pose pose)
        {
            var buttonWorldPos = pose.position;
            return DotProductBetweenControllerAndPosition(controller, buttonWorldPos);
        }

        public static Handedness GetHandFromPointerEvent(PointerEvent pointerEvent)
        {
            var dotLeft = DotProductBetweenControllerAndPose(OVRInput.Controller.LTouch, pointerEvent.Pose);
            var dotRight = DotProductBetweenControllerAndPose(OVRInput.Controller.RTouch, pointerEvent.Pose);

            return dotLeft > dotRight ? Handedness.Left : Handedness.Right;
        }

        public static OVRInput.Controller GetControllerFromPointerEvent(PointerEvent pointerEvent)
        {
            var dotLeft = DotProductBetweenControllerAndPose(OVRInput.Controller.LTouch, pointerEvent.Pose);
            var dotRight = DotProductBetweenControllerAndPose(OVRInput.Controller.RTouch, pointerEvent.Pose);

            return dotLeft > dotRight ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        }

        public static OVRInput.Controller GetControllerFromHandedness(Handedness handedness)
        {
            return handedness == Handedness.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        }
    }
}
