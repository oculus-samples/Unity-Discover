// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Discover.Haptics;
using Discover.Utils;
using Meta.XR.Samples;
using Oculus.Interaction.Input;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Oculus.Interaction;

namespace Discover.UI
{
    [MetaCodeSample("Discover")]
    public class TileButton : MonoBehaviour, IPointableElement
    {
        public UnityEvent<Handedness> OnClick;
        public event Action<PointerEvent> WhenPointerEventRaised = delegate { };

        [SerializeField]
        private Sprite m_sourceImage;
        public Sprite SourceImage
        {
            get => m_sourceImage;
            set
            {
                m_sourceImage = value;
                m_imageComponent.sprite = m_sourceImage;
            }
        }

        [SerializeField]
        private string m_title;
        public string Title
        {
            get => m_title;
            set
            {
                m_title = value;
                m_textComponent.text = m_title;
            }
        }

        [SerializeField]
        private Image m_imageComponent;
        [SerializeField]
        private TextMeshProUGUI m_textComponent;

        [SerializeField]
        private VibrationForce m_hapticsHoverForce = VibrationForce.LIGHT;
        [SerializeField]
        private VibrationForce m_hapticsPressForce = VibrationForce.HARD;
        [SerializeField]
        private float m_hapticsDuration = 0.05f;

        private void Awake()
        {
            FindDependencies();

            Assert.IsNotNull(m_textComponent, $"{nameof(m_textComponent)} cannot be null.");
            Assert.IsNotNull(m_imageComponent, $"{nameof(m_imageComponent)} cannot be null.");

            SourceImage = m_sourceImage;
            Title = m_title;
        }

        public void OnPointerClick(PointerEvent eventData)
        {
            Debug.Log($"tile button with {m_title} pressed {eventData.Identifier}");
            var handedness = ControllerUtils.GetHandFromPointerEvent(eventData);
            var controller = ControllerUtils.GetControllerFromHandedness(handedness);
            HapticsManager.Instance.VibrateForDuration(m_hapticsPressForce, m_hapticsDuration, controller);
            Click(handedness);
        }

        public void OnPointerEnter(PointerEvent eventData)
        {
            var controller = ControllerUtils.GetControllerFromPointerEvent(eventData);
            HapticsManager.Instance.VibrateForDuration(m_hapticsHoverForce, m_hapticsDuration, controller);
        }

        public void OnPointerExit(PointerEvent eventData) { }

        private void Click(Handedness handedness)
        {
            OnClick?.Invoke(handedness);
        }

        private void FindDependencies()
        {
            if (m_textComponent == null)
            {
                m_textComponent = GetComponentInChildren<TextMeshProUGUI>();
            }
            if (m_imageComponent == null)
            {
                var imageTransform = transform.FindChildRecursive("Image");
                if (imageTransform != null)
                {
                    m_imageComponent = imageTransform.gameObject.GetComponentInChildren<Image>();
                }
            }
        }

        public void ProcessPointerEvent(PointerEvent pointerEvent)
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
                    break;
                case PointerEventType.Unselect:
                    OnPointerClick(pointerEvent);
                    break;
                case PointerEventType.Move:
                    break;
                case PointerEventType.Cancel:
                    break;
                default:
                    break;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            FindDependencies();
        }
#endif
    }
}
