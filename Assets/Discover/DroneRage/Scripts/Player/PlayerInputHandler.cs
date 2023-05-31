// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.DroneRage.Player
{
    public class PlayerInputHandler : MonoBehaviour
    {

        [SerializeField]
        private Transform m_targetTransform;

        public void SetTargetTransform(Transform targetTransform)
        {
            m_targetTransform = targetTransform;
            UpdatePosition();
        }

        private void Update()
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (m_targetTransform == null)
            {
                return;
            }

            transform.position = m_targetTransform.position;
            transform.rotation = m_targetTransform.rotation;
        }
    }
}
