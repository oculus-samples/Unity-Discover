// Copyright (c) Meta Platforms, Inc. and affiliates.


using Meta.Utilities;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using Oculus.Interaction.Input.Visuals;
using UnityEngine;

namespace Discover
{
    public class AppInteractionController : Singleton<AppInteractionController>
    {
        [field: SerializeField, AutoSet] public OVRCameraRig CameraRig { get; private set; }

        [Header("Ray Interactors")]
        [SerializeField] private RayInteractor m_rightControllerInteractor;
        [SerializeField] private RayInteractor m_leftControllerInteractor;
        [SerializeField] private RayInteractor m_rightHandInteractor;
        [SerializeField] private RayInteractor m_leftHandInteractor;
        
        [Header("Poke Interactors")]
        [SerializeField] private PokeInteractor[] m_pokeInteractors;

        [Header("Hands")]
        [SerializeField] private OVRSkeleton m_rightHandSkeleton;
        [SerializeField] private Hand m_rightHand;
        [SerializeField] private OVRSkeleton m_leftHandSkeleton;
        [SerializeField] private Hand m_leftHand;

        [Header("Grab Interactors")]
        [SerializeField] private HandGrabInteractor m_rightControllerGrabInteractor;
        [SerializeField] private HandGrabInteractor m_leftControllerGrabInteractor;
        [SerializeField] private HandGrabInteractor m_rightHandGrabInteractor;
        [SerializeField] private HandGrabInteractor m_leftHandGrabInteractor;

        private ControllerVisual[] m_controllerMeshes;

        private RayInteractor m_lastUsedHandInteractor;
        private bool m_interactorDisabledForApp;

        private bool m_pinchingLeft = false;
        private bool m_pinchingRight = false;
        private bool m_pressedLeft = false;
        private bool m_pressedRight = false;
        private bool m_releasedLeft = false;
        private bool m_releasedRight = false;

        public bool OnPinchStart()
        { // pinch start
            return m_pressedLeft || m_pressedRight;
        }
        public bool OnPinchRelease()
        { // pinch release
            return m_releasedLeft || m_releasedRight;
        }

        public bool OnPinchStart(Handedness handedness)
        { // pinch start
            return handedness == Handedness.Left ? m_pressedLeft : m_pressedRight;
        }

        public bool OnPinchRelease(Handedness handedness)
        { // pinch release
            return handedness == Handedness.Left ? m_releasedLeft : m_releasedRight;
        }

        public HandGrabInteractor GetControllerGrabInteractor(Handedness handedness)
        {
            return handedness == Handedness.Left ? m_leftControllerGrabInteractor : m_rightControllerGrabInteractor;
        }

        public HandGrabInteractor GetHandGrabInteractor(Handedness handedness)
        {
            return handedness == Handedness.Left ? m_leftHandGrabInteractor : m_rightHandGrabInteractor;
        }

        protected override void InternalAwake()
        {
            if (!m_rightControllerInteractor
              || !m_leftControllerInteractor
              || !m_rightHandInteractor
              || !m_leftHandInteractor
              || !m_rightHandSkeleton
              || !m_leftHandSkeleton
              || !m_rightHand
              || !m_leftHand)
            {
                Debug.Log($"Missing interactor references, functionality not guaranteed");
            }

            // crawl the scene for controller visuals, to avoid making a .scene change with a hard link
            m_controllerMeshes = FindObjectsByType(typeof(ControllerVisual), FindObjectsSortMode.None) as ControllerVisual[];
        }

        private void Update()
        {
            if (m_leftHand && m_rightHand)
            {
                DoGestureCalculation(Handedness.Left, ref m_pressedLeft, ref m_releasedLeft, ref m_pinchingLeft);
                DoGestureCalculation(Handedness.Right, ref m_pressedRight, ref m_releasedRight, ref m_pinchingRight);
            }
        }

        private void DoGestureCalculation(Handedness handedness, ref bool pressed, ref bool released, ref bool pinching)
        {
            var lefty = handedness == Handedness.Left;
            var handPinching = lefty ? m_leftHand.GetIndexFingerIsPinching() : m_rightHand.GetIndexFingerIsPinching();

            pressed = false;
            released = false;

            if (pinching)
            {
                if (!handPinching)
                {
                    pinching = false;
                    released = true;
                }
            }
            else
            {
                if (handPinching)
                {
                    pressed = true;
                    pinching = true;

                    m_lastUsedHandInteractor = lefty ? m_leftHandInteractor : m_rightHandInteractor;
                }
            }
        }

        public RayInteractor GetRay(Handedness handedness)
        {
            return OVRPlugin.GetControllerIsInHand(OVRPlugin.Step.Render, handedness == Handedness.Left ? OVRPlugin.Node.ControllerLeft : OVRPlugin.Node.ControllerRight)
                ? handedness == Handedness.Left ? m_leftControllerInteractor : m_rightControllerInteractor
                : handedness == Handedness.Left ? m_leftHandInteractor : m_rightHandInteractor;
        }

        public RayInteractor GetLastUsedHandRay()
        {
            return m_lastUsedHandInteractor;
        }

        public void DisableSystemInteractorForApp()
        {
            m_interactorDisabledForApp = true;
            EnableSystemInteractors(false);
        }

        public void EnableSystemInteractorForApp()
        {
            m_interactorDisabledForApp = false;
            EnableSystemInteractors(true);
        }

        public void ToggleSystemInteractorForMenu(bool menuOpen)
        {
            EnableSystemInteractors(menuOpen || !m_interactorDisabledForApp);
        }

        // when entering an app, we want to disable the UI interactors' VISUALS only
        private void EnableSystemInteractors(bool doEnable)
        {
            Debug.Log($"Setting system interactors enabled={doEnable}.");

            if (m_rightControllerInteractor)
                EnableRayInteractorVisuals(m_rightControllerInteractor, doEnable);
            if (m_leftControllerInteractor)
                EnableRayInteractorVisuals(m_leftControllerInteractor, doEnable);
            if (m_rightHandInteractor)
                EnableRayInteractorVisuals(m_rightHandInteractor, doEnable);
            if (m_leftHandInteractor)
                EnableRayInteractorVisuals(m_leftHandInteractor, doEnable);

            foreach (var pokeInteractor in m_pokeInteractors)
            {
                if (pokeInteractor != null)
                {
                    EnablePokeInteractorVisuals(pokeInteractor, doEnable);
                }
            }

            foreach (var controller in m_controllerMeshes)
            {
                controller.ForceOffVisibility = !doEnable;
            }
        }

        private void EnableRayInteractorVisuals(RayInteractor ray, bool doEnable)
        {
            // we'll allow a string search here because this is a system prefab and unlikely to change
            var visuals = ray.transform.Find("Visuals");
            // We disable the child of the Visuals root
            var count = visuals.childCount;
            for (var i = 0; i < count; ++i)
            {
                visuals.GetChild(i).gameObject.SetActive(doEnable);
            }
        }
        
        private void EnablePokeInteractorVisuals(PokeInteractor poke, bool doEnable)
        {
            // we'll allow a string search here because this is a system prefab and unlikely to change
            var visual = poke.transform.Find("PokeLocation");
            if (visual != null)
            {
                visual.gameObject.SetActive(doEnable);
            }
        }
    }
}
