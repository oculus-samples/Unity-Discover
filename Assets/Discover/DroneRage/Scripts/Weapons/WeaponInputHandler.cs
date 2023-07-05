// Copyright (c) Meta Platforms, Inc. and affiliates.

using Cysharp.Threading.Tasks;
using Discover.DroneRage.UI.EndScreen;
using Discover.Menus;
using Meta.Utilities;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Discover.DroneRage.Weapons
{
    public class WeaponInputHandler : MonoBehaviour
    {

        [SerializeField, AutoSetFromChildren]
        private Weapon m_weapon;

        public Weapon ControlledWeapon
        {
            get => m_weapon;
            set
            {
                m_weapon = value;
                if (m_isTriggerHeld)
                {
                    m_weapon.StartFiring();
                }
            }
        }

        [SerializeField] private float m_triggerStrengthThreshold = 0.5f;

        private bool m_isTriggerHeld = false;
        private HandGrabInteractor m_currentGrabInteractor;
        private IActiveState m_currentActiveState;

        private HandGrabInteractor m_controllerGrabInteractor;
        private IActiveState m_controllerActiveState;
        private HandGrabInteractor m_handGrabInteractor;
        private IActiveState m_handActiveState;

        private bool m_isCurrentInteractorActive;

        private bool m_usingHands;

        private void Start()
        {
            if (TryGetComponent<WeaponVisuals>(out var weaponVisuals))
            {
                // Allow the local player's guns to create more shells
                weaponVisuals.MaxShellCasings = 25;
            }

            MainMenuController.Instance.OnMenuButtonPressed += OnMenuShowing;
            EndScreenController.WhenInstantiated(c => c.OnStateChanged += OnMenuShowing);
        }

        private void OnDestroy()
        {
            MainMenuController.Instance.OnMenuButtonPressed -= OnMenuShowing;
            if (EndScreenController.Instance != null)
                EndScreenController.Instance.OnStateChanged -= OnMenuShowing;
            CleanupInteractor();
        }

        public async void Setup(HandGrabInteractor controllerGrab, HandGrabInteractor handGrab, Handedness hand)
        {
            m_controllerGrabInteractor = controllerGrab;
            m_controllerActiveState = controllerGrab.Hand as IActiveState;
            m_handGrabInteractor = handGrab;
            m_handActiveState = handGrab.Hand as IActiveState;

            if (TryGetComponent<WeaponVisuals>(out var weaponVisuals))
            {
                weaponVisuals.HapticsTargetController = hand == Handedness.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            }

            // wait a frame for everything to fully instantiate
            await UniTask.DelayFrame(1);

            UpdateInteractor(OVRPlugin.GetHandTrackingEnabled());
        }

        private void OnMenuShowing(bool show)
        {
            if (show)
            {
                CleanupInteractor();
            }
            else
            {
                UpdateInteractor(OVRPlugin.GetHandTrackingEnabled());
            }
        }

        private void CleanupInteractor()
        {
            if (m_currentGrabInteractor != null)
            {
                m_currentGrabInteractor.ForceRelease();
            }
        }
        private void UpdateInteractor(bool useHands)
        {
            CleanupInteractor();
            m_usingHands = useHands;
            m_currentGrabInteractor = m_usingHands ? m_handGrabInteractor : m_controllerGrabInteractor;
            m_currentActiveState = m_usingHands ? m_handActiveState : m_controllerActiveState;
            var interactable = m_weapon.HandGrabInteractable;
            if (interactable != null && m_currentGrabInteractor != null && m_currentActiveState.Active)
            {
                Debug.Log($"Selecting {interactable} with {m_currentGrabInteractor}", this);
                m_currentGrabInteractor.ForceSelect(interactable);
            }

            m_isCurrentInteractorActive = m_currentActiveState?.Active ?? false;
        }

        private void Update()
        {
            var useHands = OVRPlugin.GetHandTrackingEnabled();
            var activeStateChanged = m_currentActiveState != null &&
                                     m_isCurrentInteractorActive != m_currentActiveState.Active;
            if (useHands != m_usingHands || activeStateChanged)
            {
                UpdateInteractor(useHands);
            }

            var isTriggerHeldThisFrame = false;
            if (m_weapon.HandGrabInteractable != null && m_currentGrabInteractor != null &&
                m_currentGrabInteractor.HasSelectedInteractable)
            {
                var triggerStrength = m_currentGrabInteractor.HandGrabApi.GetFingerPalmStrength(HandFinger.Index);
                isTriggerHeldThisFrame = triggerStrength > m_triggerStrengthThreshold;
            }

            if (isTriggerHeldThisFrame == m_isTriggerHeld)
            {
                return;
            }

            if (m_weapon)
            {
                if (isTriggerHeldThisFrame)
                {
                    m_weapon.StartFiring();
                }
                else
                {
                    m_weapon.StopFiring();
                }
            }

            m_isTriggerHeld = isTriggerHeldThisFrame;
        }
    }
}