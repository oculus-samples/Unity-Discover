// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meta.Utilities.Input
{
    public class XRInputControlActions : ScriptableObject
    {
        [System.Serializable]
        public struct Controller
        {
            public InputActionProperty ButtonOne;
            public InputActionProperty ButtonTwo;
            public InputActionProperty ButtonThree;
            public InputActionProperty ButtonPrimaryThumbstick;

            public InputActionProperty TouchOne;
            public InputActionProperty TouchTwo;
            public InputActionProperty TouchPrimaryThumbstick;
            public InputActionProperty TouchPrimaryThumbRest;

            public InputActionProperty AxisIndexTrigger;
            public InputActionProperty AxisHandTrigger;

            public InputActionProperty[] AllActions => new[] {
                ButtonOne,
                ButtonTwo,
                ButtonThree,
                ButtonPrimaryThumbstick,
                TouchOne,
                TouchTwo,
                TouchPrimaryThumbstick,
                TouchPrimaryThumbRest,
                AxisIndexTrigger,
                AxisHandTrigger,
            };

            public InputActionProperty[] PrimaryThumbButtonTouches => new[] {
                TouchOne,
                TouchTwo,
                TouchPrimaryThumbRest,
                TouchPrimaryThumbstick
            };

            public float AnyPrimaryThumbButtonTouching =>
                PrimaryThumbButtonTouches.Max(a => a.action.ReadValue<float>());
        }

        public Controller LeftController;
        public Controller RightController;

        public IEnumerable<InputActionProperty> AllActions =>
            new[] { LeftController, RightController }.SelectMany(c => c.AllActions);

        public void EnableActions()
        {
            foreach (var action in AllActions)
                action.action.Enable();
        }
    }
}
