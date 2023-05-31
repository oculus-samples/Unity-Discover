// Copyright (c) Meta Platforms, Inc. and affiliates.

using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MRBike
{
    public class TransformTarget : MonoBehaviour
    {
        [SerializeField] private GameObject m_grabbedObject;

        [SerializeField] private float m_thresholdDistance = 0.1f;
        [SerializeField] private float m_thresholdAngle = 15;
        [SerializeField] private float m_offset = 0;
        [SerializeField] private bool m_removeGrabbableOnComplete = true;

        [SerializeField] private TMP_Text m_debugText;

        public UnityEvent OnComplete;

        public GameObject GrabbedObject
        {
            set => m_grabbedObject = value;
        }

        private void Update()
        {
            CheckForTargetPosition();
        }

        private void CheckForTargetPosition()
        {
            if (m_grabbedObject == null)
            {
                return;
            }

            var dist = Vector3.Distance(gameObject.transform.position, m_grabbedObject.transform.position) - m_offset;
            var angle = Vector3.Angle(m_grabbedObject.transform.up, gameObject.transform.up);

            if (m_debugText != null)
            {
                m_debugText.text = dist.ToString("F");
            }

            if (dist < m_thresholdDistance && angle < m_thresholdAngle)
            {
                SetOnTarget();
            }
        }

        public void SetOnTarget()
        {
            if (m_removeGrabbableOnComplete && m_grabbedObject)
            {
                m_grabbedObject.SetActive(false);
                if (m_grabbedObject.TryGetComponent<BikeVisibleObject>(out var bikeObj))
                {
                    bikeObj.Hide();
                }
            }

            gameObject.SetActive(false);
            if (m_debugText != null)
            {
                m_debugText.text = "Contact";
            }

            OnComplete?.Invoke();
        }
    }
}
