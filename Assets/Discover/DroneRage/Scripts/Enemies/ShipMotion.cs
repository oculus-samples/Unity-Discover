// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.DroneRage.Enemies
{
    public class ShipMotion : MonoBehaviour
    {


        [SerializeField]
        private Vector3 m_motionRadius = Vector3.one;

        [SerializeField]
        private Vector3 m_motionFrequency = Vector3.one;

        [SerializeField]
        private Vector2 m_bankAngles = Vector2.one * 5.0f;

        private Vector3 m_targetPosition;
        private Quaternion m_targetRotation;

        private void Start()
        {
            m_targetPosition = transform.localPosition;
            m_targetRotation = transform.localRotation;
        }

        private void Update()
        {
            var px = Mathf.PerlinNoise(Time.time * m_motionFrequency.x, 0.0f) * 2.0f - 1.0f;
            var py = Mathf.PerlinNoise(Time.time * m_motionFrequency.y, 10.0f) * 2.0f - 1.0f;
            var pz = Mathf.PerlinNoise(Time.time * m_motionFrequency.z, 20.0f) * 2.0f - 1.0f;

            var offset = Vector3.Scale(new Vector3(px, py, pz), m_motionRadius);
            transform.localPosition = m_targetPosition + transform.rotation * offset;

            transform.rotation = Quaternion.Euler(pz * m_bankAngles.y, 0.0f, px * m_bankAngles.x) * m_targetRotation;
        }

    }
}
