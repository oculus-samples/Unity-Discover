// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Networking;
using UnityEngine;

namespace Discover.DroneRage.Enemies
{
    public class EnemyProximitySensor : MonoBehaviour
    {

        [SerializeField]
        private Enemy m_enemy;


        [SerializeField]
        private SphereCollider m_sphereCollider;
        public float Radius => (m_sphereCollider == null) ? 0f : m_sphereCollider.radius;

        private void OnTriggerStay(Collider c)
        {
            m_enemy.OnProximityStay(c);
        }

        private void Start()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                Destroy(this);
            }
        }
    }
}
