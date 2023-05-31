// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using CommonUsages = UnityEngine.InputSystem.CommonUsages;

namespace Meta.Utilities.Input
{
    public class XRDeviceFpsSimulator : MonoBehaviour
    {
        private XRSimulatedHMDState m_hmdState;
        private XRSimulatedControllerState m_leftControllerState;
        private XRSimulatedControllerState m_rightControllerState;
        private XRSimulatedHMD m_hmdDevice;
        private XRSimulatedController m_leftControllerDevice;
        private XRSimulatedController m_rightControllerDevice;

        [SerializeField] private bool m_disableIfRealHmdConnected = true;

        [Header("Mouse Capture")]
        [SerializeField] private bool m_captureMouse = true;
        [SerializeField] private bool m_requireFocus = true;
        [SerializeField] private InputActionProperty m_releaseMouseCaptureAction;

        [Header("Input Configuration")]
        [SerializeField] private InputActionProperty m_rotationAction;
        [SerializeField] private InputActionProperty m_headMovementAction;
        [SerializeField] private InputActionProperty m_leftThumbstickAction;
        [SerializeField] private InputActionProperty m_rightThumbstickAction;
        [SerializeField] private InputActionProperty m_leftGrabAction;
        [SerializeField] private InputActionProperty m_rightGrabAction;
        [SerializeField] private InputActionProperty m_leftTriggerAction;
        [SerializeField] private InputActionProperty m_rightTriggerAction;
        [SerializeField] private InputActionProperty m_rightHandMovementAction;
        [SerializeField] private InputActionProperty m_rightHandMovementZAction;
        [SerializeField] private InputActionProperty m_leftHandMenuAction;

        private InputActionProperty[] AllActions => new[] {
            m_releaseMouseCaptureAction,
            m_rotationAction,
            m_headMovementAction,
            m_leftThumbstickAction,
            m_rightThumbstickAction,
            m_leftGrabAction,
            m_rightGrabAction,
            m_leftTriggerAction,
            m_rightTriggerAction,
            m_rightHandMovementAction,
            m_rightHandMovementZAction,
            m_leftHandMenuAction,
        };

        [Header("XR Simulation Configuration")]
        [SerializeField] private Vector3 m_centerEyeStartPosition = Vector3.up;
        [SerializeField] private Vector3 m_leftControllerOffset = new(-0.25f, -0.15f, 0.65f);
        [SerializeField] private Vector3 m_leftControllerRotation = new(-30, 0, -60);
        [SerializeField] private Vector3 m_rightControllerOffset = new(0.25f, -0.15f, 0.65f);
        [SerializeField] private Vector3 m_rightControllerRotation = new(-30, 0, 60);

        private Vector2 m_headRotator = Vector2.zero;

        private void OnEnable()
        {
            if (m_disableIfRealHmdConnected && XRSettings.loadedDeviceName?.Trim() is not ("MockHMD Display" or "" or null))
            {
                Debug.Log($"[XRDeviceFpsSimulator] Disabling in favor of {XRSettings.loadedDeviceName}", this);
                enabled = false;
                return;
            }

            m_hmdState.Reset();
            m_hmdState.centerEyePosition = m_centerEyeStartPosition;
            m_leftControllerState.Reset();
            m_rightControllerState.Reset();

            AddDevices();
            EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
            RemoveDevices();
        }

        private void EnableActions()
        {
            foreach (var action in AllActions)
                action.action.Enable();
        }

        private void DisableActions()
        {
            foreach (var action in AllActions)
                action.action.Disable();
        }

        private void AddDevices()
        {
            m_hmdDevice = InputSystem.AddDevice<XRSimulatedHMD>();
            if (m_hmdDevice == null)
            {
                Debug.LogError($"Failed to create {nameof(XRSimulatedHMD)}.");
            }

            m_leftControllerDevice = InputSystem.AddDevice<XRSimulatedController>($"{nameof(XRSimulatedController)} - {CommonUsages.LeftHand}");
            if (m_leftControllerDevice != null)
            {
                InputSystem.SetDeviceUsage(m_leftControllerDevice, CommonUsages.LeftHand);
            }
            else
            {
                Debug.LogError($"Failed to create {nameof(XRSimulatedController)} for {CommonUsages.LeftHand}.", this);
            }

            m_rightControllerDevice = InputSystem.AddDevice<XRSimulatedController>($"{nameof(XRSimulatedController)} - {CommonUsages.RightHand}");
            if (m_rightControllerDevice != null)
            {
                InputSystem.SetDeviceUsage(m_rightControllerDevice, CommonUsages.RightHand);
            }
            else
            {
                Debug.LogError($"Failed to create {nameof(XRSimulatedController)} for {CommonUsages.RightHand}.", this);
            }
        }

        private void RemoveDevices()
        {
            if (m_hmdDevice != null && m_hmdDevice.added)
                InputSystem.RemoveDevice(m_hmdDevice);

            if (m_leftControllerDevice != null && m_leftControllerDevice.added)
                InputSystem.RemoveDevice(m_leftControllerDevice);

            if (m_rightControllerDevice != null && m_rightControllerDevice.added)
                InputSystem.RemoveDevice(m_rightControllerDevice);
        }

        private T ReadAction<T>(InputActionProperty property) where T : struct =>
            IsInputEnabled ? property.action.ReadValue<T>() : default;
        private bool IsPressed(InputActionProperty property) =>
            IsInputEnabled && property.action.IsPressed();

        private void Update()
        {
            if (m_captureMouse)
            {
                var shouldCapture = !m_releaseMouseCaptureAction.action.IsPressed();
                Cursor.lockState = shouldCapture ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !shouldCapture;
            }

            m_leftControllerState.isTracked = true;
            m_rightControllerState.isTracked = true;
            m_hmdState.isTracked = true;
            m_leftControllerState.trackingState = (int)(InputTrackingState.Position | InputTrackingState.Rotation);
            m_rightControllerState.trackingState = (int)(InputTrackingState.Position | InputTrackingState.Rotation);
            m_hmdState.trackingState = (int)(InputTrackingState.Position | InputTrackingState.Rotation);

            var mouseDelta = ReadAction<Vector2>(m_rotationAction);
            m_headRotator += mouseDelta;
            m_hmdState.centerEyeRotation = Quaternion.Euler(m_headRotator.y, m_headRotator.x, 0);
            m_hmdState.deviceRotation = m_hmdState.centerEyeRotation;

            // a little wiggle keeps it updating
            m_hmdState.centerEyePosition += Mathf.Sin(Time.time) * 0.001f * Time.deltaTime * Vector3.up;
            var moveDelta = ReadAction<Vector3>(m_headMovementAction);
            m_hmdState.centerEyePosition += m_hmdState.deviceRotation * moveDelta;
            m_hmdState.devicePosition = m_hmdState.centerEyePosition;

            var handMovement = ReadAction<Vector2>(m_rightHandMovementAction);
            var handMovementZ = ReadAction<float>(m_rightHandMovementZAction);
            m_rightControllerOffset += handMovement.WithZ(handMovementZ);

            UpdateControllerState(ref m_leftControllerState, m_leftControllerOffset, Quaternion.Euler(m_leftControllerRotation));
            UpdateControllerState(ref m_rightControllerState, m_rightControllerOffset, Quaternion.Euler(m_rightControllerRotation));

            m_leftControllerState = m_leftControllerState.WithButton(ControllerButton.MenuButton, IsPressed(m_leftHandMenuAction));

            m_leftControllerState.trigger = ReadAction<float>(m_leftTriggerAction);
            m_leftControllerState.grip = ReadAction<float>(m_leftGrabAction);
            m_leftControllerState.primary2DAxis = ReadAction<Vector2>(m_leftThumbstickAction);
            UpdateButtons(ref m_leftControllerState);

            m_rightControllerState.trigger = ReadAction<float>(m_rightTriggerAction);
            m_rightControllerState.grip = ReadAction<float>(m_rightGrabAction);
            m_rightControllerState.primary2DAxis = ReadAction<Vector2>(m_rightThumbstickAction);
            UpdateButtons(ref m_rightControllerState);

            if (IsInputEnabled)
                UpdateDevices();
        }

        private static void UpdateButtons(ref XRSimulatedControllerState state)
        {
            state = state.WithButton(ControllerButton.GripButton, state.grip > 0.5f);
            state = state.WithButton(ControllerButton.TriggerButton, state.trigger > 0.5f);
        }

        private static bool IsFocused => Application.isFocused && !IsCursorVisible() && !Cursor.visible;
        private bool IsInputEnabled => !m_requireFocus || IsFocused;

#if UNITY_EDITOR_WIN
#pragma warning disable IDE0049
#pragma warning disable IDE1006

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT { public Int32 x; public Int32 y; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public Int32 cbSize; public Int32 flags; public IntPtr hCursor; public POINT ptScreenPos;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetCursorInfo")]
        public static extern bool GetCursorInfo(ref CURSORINFO pci);

#pragma warning restore IDE0049
#pragma warning restore IDE1006
#endif

        private static bool IsCursorVisible()
        {
#if UNITY_EDITOR_WIN
            var cursor = new CURSORINFO();
            cursor.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(cursor);

            if (GetCursorInfo(ref cursor))
            {
                return cursor.flags == 1;
            }
#endif

            return Cursor.visible;
        }

        private void UpdateControllerState(ref XRSimulatedControllerState controller, Vector3 offset, Quaternion rot)
        {
            controller.devicePosition = m_hmdState.devicePosition + m_hmdState.deviceRotation * offset;
            controller.deviceRotation = m_hmdState.deviceRotation * rot;
        }

        private void UpdateDevices()
        {
            if (m_hmdDevice != null)
            {
                InputState.Change(m_hmdDevice, m_hmdState);
            }

            if (m_leftControllerDevice != null)
            {
                InputState.Change(m_leftControllerDevice, m_leftControllerState);
            }

            if (m_rightControllerDevice != null)
            {
                InputState.Change(m_rightControllerDevice, m_rightControllerState);
            }
        }
    }
}
