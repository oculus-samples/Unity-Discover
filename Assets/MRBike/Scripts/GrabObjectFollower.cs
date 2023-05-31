// Copyright (c) Meta Platforms, Inc. and affiliates.

using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MRBike
{
    /// <summary>
    /// Grabbed object will follow the constraints
    /// </summary>
    public class GrabObjectFollower : MonoBehaviour
    {
        [SerializeField] private Rigidbody m_rigidbody;
        [Header("Follow")]
        [SerializeField] private GameObject m_followTarget;
        [SerializeField] private float m_followSpeed = 20;
        [Header("Rotation")]
        [SerializeField] private float m_rotationSpeed = 50;
        [SerializeField] private bool m_rotationOnly = false;
        [SerializeField] private float m_rotationTarget = 360;
        [Header("Sound")]
        [SerializeField] private float m_volumeRamp = 2;
        [Header("Debug")]
        [SerializeField] private TMP_Text m_debugText;

        private bool m_isGrabbed = false;
        private float m_volume = 0;
        private float m_oldDistance = 0;
        private float m_totalRotation = 0;

        public UnityEvent OnRotationTargetHit;

        private void Start()
        {
            m_oldDistance = Vector3.Distance(m_followTarget.transform.position, transform.position);
        }

        public void Grab()
        {
            m_isGrabbed = true;
        }

        public void Release()
        {
            m_isGrabbed = false;
            m_rigidbody.angularVelocity = Vector3.zero;
            m_rigidbody.velocity = Vector3.zero;
        }

        private void FixedUpdate()
        {
            MoveTowardGrabbable();
        }

        private void MoveTowardGrabbable()
        {
            if (m_isGrabbed)
            {
                if (m_rotationOnly)
                {
                    m_totalRotation += Time.deltaTime * m_rotationSpeed;
                    SetDebugText(m_totalRotation.ToString("F2"));
                    transform.Rotate(Vector3.up, m_totalRotation / 30);

                    if (m_rotationTarget > 0)
                    {
                        if (m_totalRotation >= m_rotationTarget)
                        {
                            OnRotationTargetHit.Invoke();
                        }
                    }
                    else
                    {
                        if (m_totalRotation <= m_rotationTarget)
                        {
                            OnRotationTargetHit.Invoke();
                        }
                    }
                }
                else
                {
                    var thisPosition = transform.position;
                    var followPosition = m_followTarget.transform.position;
                    var dist = Vector3.Distance(followPosition, thisPosition);

                    m_rigidbody.velocity = (followPosition - thisPosition).normalized * (m_followSpeed * dist);

                    var newVolume = Mathf.Abs(dist - m_oldDistance) * m_volumeRamp;
                    m_volume = newVolume;

                    m_rigidbody.MovePosition(followPosition);
                    SetDebugText(m_volume.ToString("F2"));
                }
            }
        }

        private void SetDebugText(string text)
        {
            if (m_debugText != null)
            {
                m_debugText.text = text;
            }
        }
    }
}
