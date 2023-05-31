// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.DroneRage.PowerUps
{
    public class LerpedLookAt : MonoBehaviour
    {
        [SerializeField] private float m_damp = 0.5f;
        [SerializeField] private Camera m_mainCam;

        private void Start()
        {
            if (!m_mainCam)
            {
                m_mainCam = Camera.main ? Camera.main : FindObjectOfType<Camera>();
            }
        }

        private void Update()
        {
            if (m_mainCam)
            {
                var lookAtRot = Quaternion.LookRotation(m_mainCam.transform.position - transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, lookAtRot, m_damp * Time.deltaTime);
            }
        }
    }
}
