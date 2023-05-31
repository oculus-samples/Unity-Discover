// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.DroneRage.Player
{
    public class SelfDestroy : MonoBehaviour
    {
        [SerializeField] private float m_delay = 1;
        private float m_timer = 0;

        private void Update()
        {
            m_timer += Time.deltaTime;
            if (m_timer > m_delay)
            {
                Destroy(gameObject);
            }
        }
    }
}
