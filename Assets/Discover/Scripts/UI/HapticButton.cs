// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Haptics;
using Discover.Utils;
using Meta.XR.Samples;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Discover.UI
{
    [MetaCodeSample("Discover")]
    public class HapticButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        [SerializeField] private bool m_useHaptic = true;
        [SerializeField] private VibrationForce m_vibrationForce = VibrationForce.HARD;
        [SerializeField] private float m_vibrationDurationSec = 0.05f;
        [SerializeField] private VibrationForce m_vibrationForceOnEnter = VibrationForce.LIGHT;

        public UnityEvent<Handedness> OnClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            var handedness = ControllerUtils.GetHandFromPointerData(eventData);
            var controller = ControllerUtils.GetControllerFromHandedness(handedness);
            if (m_useHaptic)
            {
                HapticsManager.Instance.VibrateForDuration(m_vibrationForce, m_vibrationDurationSec, controller);
            }

            OnClick?.Invoke(handedness);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (m_useHaptic)
            {
                var controller = ControllerUtils.GetControllerFromPointerData(eventData);
                HapticsManager.Instance.VibrateForDuration(m_vibrationForceOnEnter, m_vibrationDurationSec,
                    controller);
            }
        }
    }
}
