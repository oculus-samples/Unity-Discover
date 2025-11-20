// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;

namespace Discover
{
    [MetaCodeSample("Discover")]
    public class FollowGaze : MonoBehaviour
    {
        [SerializeField] private Transform m_uiElement;

        [SerializeField] private bool m_lockToView;

        [SerializeField] private float m_followSpeed = 0.25f;

        [SerializeField] private float m_menuHeight;

        [SerializeField] private float m_gazeOffsets;

        private Transform m_cameraTransform;

        private void Awake()
        {
            if (m_uiElement == null)
            {
                m_uiElement = transform;
            }
        }

        private void Update()
        {
            UpdatePosition();
        }

        private void OnEnable()
        {
            // Jump to the target position
            if (Camera.main != null)
            {
                m_cameraTransform = Camera.main.transform;
                var playerPos = m_cameraTransform.position;
                var targetDirection =
                    Vector3.ProjectOnPlane(m_cameraTransform.forward, Vector3.up).normalized;
                var targetPosition = playerPos + targetDirection;
                targetPosition.y = playerPos.y + m_menuHeight;
                m_uiElement.position = targetPosition;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_uiElement == null)
            {
                m_uiElement = transform;
            }
        }
#endif

        private void UpdatePosition()
        {
            var playerPos = m_cameraTransform.position;
            var uiElementPos = m_uiElement.position;

            var toUIElement = uiElementPos - playerPos;
            if (toUIElement != Vector3.zero)
            {
                m_uiElement.rotation = Quaternion.LookRotation(toUIElement);
            }

            var targetDirection = Vector3.ProjectOnPlane(m_cameraTransform.forward, Vector3.up).normalized;

            var targetPosition = playerPos + targetDirection;

            if (!m_lockToView)
            {
                targetPosition = Vector3.Slerp(uiElementPos, targetPosition, m_followSpeed * Time.deltaTime);

                var toTarget = (targetPosition - playerPos).normalized;
                targetPosition = playerPos + m_gazeOffsets * toTarget;
            }

            targetPosition.y = playerPos.y + m_menuHeight;

            m_uiElement.position = targetPosition;
        }
    }
}
