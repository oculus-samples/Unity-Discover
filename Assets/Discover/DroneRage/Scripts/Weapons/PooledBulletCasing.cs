// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace Discover.DroneRage.Weapons
{
    public class PooledBulletCasing : MonoBehaviour
    {


        [SerializeField]
        private float m_sleepReleaseTime = 3.0f;

        [SerializeField]
        private float m_maxLifetime = 10.0f;


        [SerializeField]
        private Rigidbody m_rigidbody;
        public Rigidbody Rigidbody => m_rigidbody;

        private float m_aliveTime;
        private float m_asleepTime;

        public IObjectPool<PooledBulletCasing> Pool { get; set; }

        private void Start()
        {
            Assert.IsNotNull(m_rigidbody, $"{nameof(m_rigidbody)} cannot be null.");
            Assert.IsNotNull(Pool, $"{nameof(Pool)} cannot be null.");
        }

        public void Init()
        {
            m_aliveTime = 0.0f;
            m_asleepTime = 0.0f;
            m_rigidbody.velocity = Vector3.zero;
            m_rigidbody.angularVelocity = Vector3.zero;
        }

        private void Update()
        {
            if (Pool == null)
            {
                enabled = false;
                return;
            }

            m_aliveTime += Time.deltaTime;
            if (m_aliveTime > m_maxLifetime)
            {
                Pool.Release(this);
                return;
            }

            if (Rigidbody.IsSleeping())
            {
                m_asleepTime += Time.deltaTime;
                if (m_asleepTime > m_sleepReleaseTime)
                {
                    Pool.Release(this);
                    return;
                }
            }
            else
            {
                m_asleepTime = 0.0f;
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (m_rigidbody == null)
            {
                m_rigidbody = GetComponent<Rigidbody>();
            }
#endif
        }
    }
}
