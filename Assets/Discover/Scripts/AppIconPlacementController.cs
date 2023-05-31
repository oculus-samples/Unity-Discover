// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Configs;
using Discover.Haptics;
using Discover.Utils;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Discover
{
    public class AppIconPlacementController : MonoBehaviour
    {
        public delegate void AppPlacedAction(AppManifest appManifest, /*Guid surfaceID,*/ Vector3 position, Quaternion rotation);
        public delegate void AppPlacementCanceledAction();

        [SerializeField] private AppPlacementVisual m_appPlacementVisualPrefab;

        [SerializeReference] private AppInteractionController m_appInteractionController;

        [Tooltip("distance for the icon when no surface found")]
        [SerializeField] private float m_appFloatDistance = 1.0f;

        [Header("Rotation")]
        [SerializeField] private float m_rotationControllerThreshold = 0.6f;
        [SerializeField] private float m_rotSpeed = 1.0f;
        [SerializeField] private float m_handRotationMultiplier = 200;
        [SerializeField] private float m_handRotationThreshold = 0.0005f;

        private AppPlacementVisual m_appPlacementVisual;
        private Transform m_mainCameraTransform;

        private bool m_isPlacing;
        private Handedness m_handedness;
        private AppManifest m_selectedAppManifest;
        private GameObject m_indicator;

        // interaction logic
        private RayInteractor m_rayInteractor;
        private Quaternion m_surfaceRotation;
        private bool m_isOnValidPlacement;

        // rotation
        private float m_iconRotation = 0;

        // hands
        private bool m_pinchAnchored = true;
        private Vector3 m_rotationStartPosition = Vector3.zero; // the "hand" position on the frame when placing the app
        private Vector3 m_averagePinchPosition = Vector3.zero; // this is only for the arrow visuals on the app/puck

        public AppPlacedAction OnAppPlaced;
        public AppPlacementCanceledAction OnAppPlacementCanceled;

        private void Awake()
        {
            m_appPlacementVisual = Instantiate(m_appPlacementVisualPrefab);
            m_appPlacementVisual.gameObject.SetActive(false);
            m_mainCameraTransform = Camera.main!.transform;
        }

        private void Update()
        {
            if (m_isPlacing)
            {
                HandleAppMovement();
                HandleRotation();
                HandlePlacementAction();
            }
        }

        public void StartPlacement(AppManifest appManifest, Handedness handedness)
        {
            m_isPlacing = true;
            m_isOnValidPlacement = false;
            m_handedness = handedness;
            m_selectedAppManifest = appManifest;

            m_iconRotation = appManifest.IconStartRotation;
            m_pinchAnchored = false;

            if (appManifest.DropIndicator != null)
            {
                m_indicator = Instantiate(appManifest.DropIndicator, m_appPlacementVisual.transform);
                m_indicator.SetActive(false);
            }

            m_appPlacementVisual.gameObject.SetActive(true);
        }

        public void CancelPlacement()
        {
            if (m_isPlacing)
            {
                StopPlacement();
            }
        }

        private void StopPlacement()
        {
            if (!m_isPlacing)
            {
                return;
            }

            m_isPlacing = false;
            m_appPlacementVisual.gameObject.SetActive(false);
            Destroy(m_indicator);
            m_indicator = null;
        }

        private void HandleAppMovement()
        {
            if (m_pinchAnchored)
            {
                return;
            }

            m_rayInteractor = m_appInteractionController.GetRay(m_handedness);

            if (m_rayInteractor == null)
                return;

            var collisionInfo = m_rayInteractor.CollisionInfo;
            m_isOnValidPlacement = false;
            string invalidMessage = null;

            if (collisionInfo != null)
            {
                // create a pleasing default rotation every frame:
                // by default, align app.forward (Z) with camera.forward
                var appZ = m_mainCameraTransform.transform.position - collisionInfo.Value.Point;
                appZ.y = 0;
                // if on wall, align Z up
                var wallPlacement = Mathf.Abs(Vector3.Dot(collisionInfo.Value.Normal, Vector3.up)) < 0.01f;
                if (wallPlacement)
                {
                    appZ = Vector3.up;
                }

                var hitObj =
                    m_rayInteractor.Interactable?.gameObject;

                bool onValidSurfaceType;
                if (hitObj && HasSceneElement(hitObj.transform, out var sceneElement))
                {
                    // if collision has a DESK label, align Y perpendicular with closest edge to player
                    var validQuadPlacement = sceneElement.ContainsLabel(OVRSceneManager.Classification.Desk) ||
                                             sceneElement.ContainsLabel(OVRSceneManager.Classification.Couch) ||
                                             sceneElement.ContainsLabel(OVRSceneManager.Classification.Other);
                    validQuadPlacement &= !wallPlacement; // without this, rotation is NaN on the sides of non-wall objects
                    if (validQuadPlacement)
                    {
                        var anchorTransform = sceneElement.transform;
                        var toPlane = m_mainCameraTransform.transform.position - anchorTransform.position;
                        var planeYup = Vector3.Dot(anchorTransform.up, toPlane) > 0.0f ?
                            anchorTransform.up : -anchorTransform.up;
                        var planeXup = Vector3.Dot(anchorTransform.right, toPlane) > 0.0f ?
                            anchorTransform.right : -anchorTransform.right;
                        var planeFwd = anchorTransform.forward;

                        var anchorScale = anchorTransform.localScale;
                        var anchorPosition = anchorTransform.position;
                        var nearestCorner = anchorPosition +
                                            planeXup * anchorScale.x * 0.5f +
                                            planeYup * anchorScale.y * 0.5f;
                        Vector3.OrthoNormalize(ref planeFwd, ref toPlane);
                        nearestCorner -= anchorPosition;
                        appZ = Vector3.Angle(toPlane, planeYup) > Vector3.Angle(nearestCorner, planeYup) ?
                            planeXup : planeYup;
                    }

                    onValidSurfaceType = m_selectedAppManifest.IconSurfaceType == AppManifest.SurfaceType.ANY ||
                                       sceneElement.ContainsLabel(m_selectedAppManifest.IconSurfaceType.ToString());
                    if (!onValidSurfaceType)
                    {
                        invalidMessage = $"Place on {m_selectedAppManifest.IconSurfaceType}";
                    }
                }
                else
                {
                    onValidSurfaceType = m_selectedAppManifest.IconSurfaceType == AppManifest.SurfaceType.ANY;
                }

                var appY = collisionInfo.Value.Normal;
                Vector3.OrthoNormalize(ref appZ, ref appY);
                m_surfaceRotation = Quaternion.LookRotation(appZ, appY);
                m_appPlacementVisual.transform.position = collisionInfo.Value.Point;

                // Check that the normal aligns with the type of surface
                var correctSurfaceOrientation = m_selectedAppManifest.IconSurfaceOrientation == SurfaceOrientation.HORIZONTAL
                    ? CheckHorizontal(collisionInfo.Value.Normal)
                    : CheckVertical(collisionInfo.Value.Normal);

                if (onValidSurfaceType && !correctSurfaceOrientation)
                {
                    invalidMessage = $"Place on {m_selectedAppManifest.IconSurfaceOrientation} surface";
                }

                if (correctSurfaceOrientation && onValidSurfaceType)
                {
                    if (m_indicator != null)
                    {
                        m_indicator.SetActive(true);
                    }
                    m_isOnValidPlacement = true;
                }
                else
                {
                    if (m_indicator != null)
                    {
                        m_indicator.SetActive(false);
                    }
                    m_isOnValidPlacement = false;
                }
            }
            else
            {
                // invalid placement
                if (m_indicator != null)
                {
                    m_indicator.SetActive(false);
                }

                m_appPlacementVisual.transform.position = m_rayInteractor.Origin + m_rayInteractor.Forward * m_appFloatDistance;
                m_isOnValidPlacement = false;
            }

            m_appPlacementVisual.SetValidPlacement(m_isOnValidPlacement, invalidMessage);
        }

        private void HandleRotation()
        {
            m_appPlacementVisual.transform.rotation = m_surfaceRotation;
            var clockwiseStrength = 0.0f;
            var counterClockwiseStrength = 0.0f;

            // only rotate when on valid placement
            if (m_isOnValidPlacement)
            {
                if (m_pinchAnchored)
                {
                    var previousPos = m_averagePinchPosition;
                    m_averagePinchPosition = Vector3.Lerp(m_averagePinchPosition, m_rayInteractor.Origin, 0.1f);
                    var currentDragDirection = m_rayInteractor.Origin - previousPos;
                    var indicatorPos = m_indicator.transform.position;
                    var flatFwd =
                        Vector3.Scale(indicatorPos - previousPos, new Vector3(1, 0, 1)).normalized;
                    var cross = Vector3.Cross(flatFwd, Vector3.up);
                    var rotationAmount = Vector3.Dot(cross, currentDragDirection);

                    if (Mathf.Abs(rotationAmount) > m_handRotationThreshold)
                    {
                        m_iconRotation += rotationAmount * m_handRotationMultiplier;
                        clockwiseStrength = Mathf.Sign(rotationAmount) * 0.5f + 0.5f;
                        counterClockwiseStrength = 1 - clockwiseStrength;
                    }
                }
                else
                {
                    // if using controllers, use thumbstick
                    var vec = m_handedness == Handedness.Right
                        ? OVRInput.Get(OVRInput.RawAxis2D.RThumbstick)
                        : OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
                    if (Mathf.Abs(vec.x) > m_rotationControllerThreshold)
                    {
                        m_iconRotation += m_rotSpeed * Mathf.Sign(vec.x);
                        clockwiseStrength = Mathf.Sign(vec.x) * 0.5f + 0.5f; // remap a sign value
                        counterClockwiseStrength = 1 - clockwiseStrength;
                    }
                }
            }

            m_appPlacementVisual.transform.Rotate(Vector3.up, m_iconRotation, Space.Self);
            m_appPlacementVisual.UpdateRotationArrows(clockwiseStrength, counterClockwiseStrength);
        }

        private void HandlePlacementAction()
        {
            if (m_isOnValidPlacement)
            {
                if (m_appInteractionController.OnPinchStart(m_handedness))
                {
                    m_pinchAnchored = true;
                    m_rotationStartPosition = m_rayInteractor.Origin;
                    m_averagePinchPosition = m_rotationStartPosition;
                }
                var triggerButton = m_handedness == Handedness.Right
                    ? OVRInput.RawButton.RIndexTrigger
                    : OVRInput.RawButton.LIndexTrigger;
                if (OVRInput.GetUp(triggerButton) ||
                    (m_pinchAnchored && m_appInteractionController.OnPinchRelease(m_handedness)))
                {
                    var controller = ControllerUtils.GetControllerFromHandedness(m_handedness);
                    HapticsManager.Instance?.VibrateForDuration(VibrationForce.HARD, 0.05f, controller);
                    FinishAppPlacement();
                    return;
                }
            }

            // Press B or Y to exit 
            var cancelButton = m_handedness == Handedness.Right ? OVRInput.RawButton.B : OVRInput.RawButton.Y;
            if (OVRInput.GetDown(cancelButton))
            {
                HandleCancelPlacement();
            }
        }

        private void HandleCancelPlacement()
        {
            StopPlacement();
            OnAppPlacementCanceled?.Invoke();
        }

        private void FinishAppPlacement()
        {
            StopPlacement();

            var visualTransform = m_appPlacementVisual.transform;
            OnAppPlaced?.Invoke(m_selectedAppManifest, visualTransform.position,
                visualTransform.rotation);
        }

        private bool HasSceneElement(Transform objTransform, out SceneElement sceneElement)
        {
            sceneElement = objTransform.GetComponentInParent<SceneElement>();

            return sceneElement != null;
        }

        private static bool CheckHorizontal(Vector3 surfaceNormal)
        {
            return Vector3.Dot(surfaceNormal, Vector3.up) >= 0.99f;
        }

        private static bool CheckVertical(Vector3 surfaceNormal)
        {
            // this 0.02 is to account for slight variation of vertical angle
            return Mathf.Abs(Vector3.Dot(surfaceNormal, Vector3.up)) <= 0.02f;
        }
    }
}