// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Discover.Haptics;
using Discover.Utils;
using Meta.XR.Samples;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Discover.Icons
{
    [MetaCodeSample("Discover")]
    public class IconController : MonoBehaviour
    {
        private enum ClickState
        {
            NONE,
            CLICK,
            MOVE_DELAY,
            MOVE
        }
        private const float CLICK_WINDOW_TIME_SEC = 0.5f;
        private const float MOVE_WINDOW_TIME_SEC = 2f;
        private static readonly int s_highlightedProperty = Shader.PropertyToID("_Highlighted");
        private static readonly int s_proximityProperty = Shader.PropertyToID("_Proximity");

        [SerializeField] private string m_appName;

        [Interface(typeof(IPointable))]
        [SerializeField] private MonoBehaviour m_pointable;
        private IPointable m_pointableInt;

        [SerializeField] private MeshRenderer m_highlightMesh;
        [SerializeField] private MeshRenderer m_guardianMesh;

        [SerializeField] private AudioSource m_rolloverSfx;
        [SerializeField] private MoveIconController m_moveIconController;

        private Coroutine m_hoverCoroutine;

        private ClickState m_clickState;
        private float m_clickTimer;
        private OVRInput.Controller m_currentController;

        public string AppName => m_appName;

        private void Awake()
        {
            m_pointableInt = m_pointable as IPointable;
            ResetHoverFX();
            m_moveIconController.Hide();
        }

        private void OnDestroy()
        {
            if (m_appName != null)
            {
                IconsManager.Instance.DeregisterIcon(m_appName);
            }
        }

        public void SetApp(string appName)
        {
            m_appName = appName;
            IconsManager.Instance.RegisterIcon(appName, gameObject);
        }

        private void OnPointerClick(PointerEvent pointerEvent)
        {
            if (m_currentController != OVRInput.Controller.None)
            {
                return;
            }

            m_currentController = GetControllerFromPointerEvent(pointerEvent);

            m_clickState = ClickState.CLICK;
            m_clickTimer = 0;
        }

        private void OnPointerRelease(PointerEvent pointerEvent)
        {
            var controller = GetControllerFromPointerEvent(pointerEvent);
            if (controller != m_currentController)
            {
                return;
            }
            if (m_clickState == ClickState.CLICK)
            {
                HapticsManager.Instance?.VibrateForDuration(VibrationForce.HARD, 0.05f, controller);
                ResetHoverFX();
                StartApp();
            }
            else if (m_clickState == ClickState.MOVE)
            {
                HapticsManager.Instance?.VibrateForDuration(VibrationForce.HARD, 0.05f, controller);
                var handedness = (controller is OVRInput.Controller.LHand or OVRInput.Controller.LTouch) ?
                    Handedness.Left :
                    Handedness.Right;
                ResetHoverFX();
                AppsManager.Instance.StartMoveApp(this, handedness);
            }
            m_moveIconController.Hide();
            m_clickState = ClickState.NONE;
            m_currentController = OVRInput.Controller.None;
        }

        private void OnPointerEnter(PointerEvent pointerEvent)
        {
            var controller = GetControllerFromPointerEvent(pointerEvent);
            if (m_currentController != OVRInput.Controller.None && controller != m_currentController)
            {
                return;
            }

            HapticsManager.Instance?.VibrateForDuration(VibrationForce.LIGHT, 0.05f, controller);
            PlayHoverFX(true);
        }

        private void OnPointerExit(PointerEvent pointerEvent)
        {
            var controller = GetControllerFromPointerEvent(pointerEvent);
            if (m_currentController != OVRInput.Controller.None && controller != m_currentController)
            {
                return;
            }

            PlayHoverFX(false);
        }

        private void Update()
        {
            if (m_clickState == ClickState.CLICK)
            {
                m_clickTimer += Time.deltaTime;

                if (m_clickTimer > CLICK_WINDOW_TIME_SEC && AvatarColocationManager.Instance.CanPlaceOrMoveIcons)
                {
                    m_clickState = ClickState.MOVE_DELAY;
                    m_moveIconController.Show();
                    m_moveIconController.SetFill(0);
                    m_clickTimer = 0;
                }
            }
            else if (m_clickState == ClickState.MOVE_DELAY)
            {
                m_clickTimer += Time.deltaTime;
                HapticsManager.Instance?.VibrateForDuration(VibrationForce.LIGHT, 0.05f, m_currentController);
                m_moveIconController.SetFill(m_clickTimer / MOVE_WINDOW_TIME_SEC);
                if (m_clickTimer > MOVE_WINDOW_TIME_SEC)
                {
                    m_clickState = ClickState.MOVE;
                    HapticsManager.Instance?.VibrateForDuration(VibrationForce.HARD, 0.05f, m_currentController);
                    m_moveIconController.SetFill(1);
                    m_clickTimer = 0;
                }
            }
        }

        private void StartApp()
        {
            Debug.Log($"Try Launch App {m_appName}");
            NetworkApplicationManager.Instance.LaunchApplication(m_appName, transform);
        }

        private void OnEnable()
        {
            m_pointableInt.WhenPointerEventRaised += HandPointerEvent;
            ResetHoverFX();
        }

        private void OnDisable()
        {
            m_pointableInt.WhenPointerEventRaised -= HandPointerEvent;
            ResetHoverFX();
        }

        private void HandPointerEvent(PointerEvent pointerEvent)
        {
            switch (pointerEvent.Type)
            {
                case PointerEventType.Hover:
                    OnPointerEnter(pointerEvent);
                    break;
                case PointerEventType.Unhover:
                    OnPointerExit(pointerEvent);
                    break;
                case PointerEventType.Select:
                    OnPointerClick(pointerEvent);
                    break;
                case PointerEventType.Unselect:
                    OnPointerRelease(pointerEvent);
                    break;
                case PointerEventType.Move:
                    break;
                case PointerEventType.Cancel:
                    break;
                default:
                    break;
            }
        }

        private void PlayHoverFX(bool doEnable)
        {
            if (m_highlightMesh)
            {
                if (m_hoverCoroutine != null)
                {
                    StopCoroutine(m_hoverCoroutine);
                }
                m_hoverCoroutine = StartCoroutine(HoverEffectLerp(doEnable));
            }

            if (m_rolloverSfx && doEnable)
            {
                m_rolloverSfx.Play();
            }
        }

        private void ResetHoverFX()
        {
            if (m_highlightMesh != null)
            {
                m_highlightMesh.material.SetFloat(s_highlightedProperty, 0);
            }

            if (m_guardianMesh != null)
            {
                m_guardianMesh.material.SetFloat(s_proximityProperty, 0);
            }
        }

        private IEnumerator HoverEffectLerp(bool doEnable)
        {
            var currentHighlightValue = m_highlightMesh.material.GetFloat(s_highlightedProperty);
            float targetHighlightValue = doEnable ? 1 : 0;
            var timer = 0.0f;
            var duration = 0.25f;
            while (timer <= duration)
            {
                timer += Time.deltaTime;
                var targetValue = Mathf.Lerp(currentHighlightValue, targetHighlightValue, Mathf.Clamp01(timer / duration));
                if (m_highlightMesh != null)
                {
                    m_highlightMesh.material.SetFloat(s_highlightedProperty, targetValue);
                }

                if (m_guardianMesh != null)
                {
                    m_guardianMesh.material.SetFloat(s_proximityProperty, targetValue * 0.5f);
                }

                yield return null;
            }
        }

        private OVRInput.Controller GetControllerFromPointerEvent(PointerEvent pointerEvent)
        {
            var controller = OVRInput.Controller.None;
            if (pointerEvent.Data is RayInteractor ri)
            {
                if (ri != null)
                {
                    if (ri.TryGetComponent<ControllerRef>(out var controllerRef))
                    {
                        var handedness = controllerRef.Handedness;
                        controller = ControllerUtils.GetControllerFromHandedness(handedness);
                    }
                    else if (ri.TryGetComponent<HandRef>(out var handRef))
                    {
                        var handedness = handRef.Handedness;
                        controller = handedness == Handedness.Left
                            ? OVRInput.Controller.LHand
                            : OVRInput.Controller.RHand;
                    }
                }
            }

            if (controller == OVRInput.Controller.None)
            {
                controller = ControllerUtils.GetControllerFromPointerEvent(pointerEvent);
            }

            return controller;
        }
    }
}
