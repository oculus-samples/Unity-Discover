// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_AVATARS

using Oculus.Avatar2;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Meta.Utilities.Input
{
    public class XRInputManager : OvrAvatarInputManager
    {
        private const string LOG_SCOPE = "xrInput";

        [SerializeField]
        [Tooltip("Optional. If added, it will use input directly from OVRCameraRig instead of doing its own calculations.")]
        private OVRCameraRig m_ovrCameraRig;

        [SerializeField] private XRInputControlActions m_controlActions;

        // Only used in editor, produces warnings when packaging
#pragma warning disable CS0414 // is assigned but its value is never used
        [SerializeField]
        private bool m_debugDrawTrackingLocations;
#pragma warning restore CS0414 // is assigned but its value is never used

        protected void Awake()
        {
            // Debug Drawing
#if UNITY_EDITOR
#if UNITY_2019_3_OR_NEWER
            SceneView.duringSceneGui += OnSceneGUI;
#else
        SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
#endif

            m_controlActions.EnableActions();
        }

        private void Start()
        {
            if (BodyTracking != null)
            {
                BodyTracking.InputTrackingDelegate = new XRInputTrackingDelegate(m_ovrCameraRig, true);
                BodyTracking.InputControlDelegate = new XRInputControlDelegate(m_controlActions);
            }
        }

        protected override void OnDestroyCalled()
        {
#if UNITY_EDITOR
#if UNITY_2019_3_OR_NEWER
            SceneView.duringSceneGui -= OnSceneGUI;
#else
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
#endif
#endif

            base.OnDestroyCalled();
        }

        public XRInputControlActions.Controller GetActions(bool forLeftHand)
        {
            return forLeftHand ? m_controlActions.LeftController : m_controlActions.RightController;
        }

        public Transform GetAnchor(bool forLeftHand)
        {
            return forLeftHand ? m_ovrCameraRig.leftHandAnchor : m_ovrCameraRig.rightHandAnchor;
        }

#if UNITY_EDITOR

        #region Debug Drawing

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_debugDrawTrackingLocations) DrawTrackingLocations();
        }

        private void DrawTrackingLocations()
        {
            var inputTrackingState = BodyTracking.InputTrackingState;

            var radius = 0.2f;
            Quaternion orientation;

            float OuterRadius() => radius + 0.25f;

            Vector3 Forward() => orientation * Vector3.forward;

            Handles.color = Color.blue;
            _ = Handles.RadiusHandle(Quaternion.identity, inputTrackingState.headset.position, radius);

            orientation = inputTrackingState.headset.orientation;
            Handles.DrawLine(inputTrackingState.headset.position + Forward() * radius,
                inputTrackingState.headset.position + Forward() * OuterRadius());

            radius = 0.1f;
            Handles.color = Color.yellow;
            _ = Handles.RadiusHandle(Quaternion.identity, inputTrackingState.leftController.position, radius);

            orientation = inputTrackingState.leftController.orientation;
            Handles.DrawLine(inputTrackingState.leftController.position + Forward() * radius,
                inputTrackingState.leftController.position + Forward() * OuterRadius());

            Handles.color = Color.yellow;
            _ = Handles.RadiusHandle(Quaternion.identity, inputTrackingState.rightController.position, radius);

            orientation = inputTrackingState.rightController.orientation;
            Handles.DrawLine(inputTrackingState.rightController.position + Forward() * radius,
                inputTrackingState.rightController.position + Forward() * OuterRadius());
        }

        #endregion

#endif // UNITY_EDITOR
    }
}

#endif
