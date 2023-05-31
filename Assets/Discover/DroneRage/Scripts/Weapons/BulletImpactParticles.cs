// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Discover.DroneRage.Audio;
using UnityEngine;
using static Discover.DroneRage.Bootstrapper.DroneRageAppContainerUtils;

namespace Discover.DroneRage.Weapons
{
    public class BulletImpactParticles : MonoBehaviour
    {
        private class CircularPool
        {
            public BulletImpactParticles[] Pool;
            private int m_index;

            public CircularPool(BulletImpactParticles primitive, int size)
            {
                Pool = new BulletImpactParticles[size];
                m_index = 0;

                for (var i = 0; i < size; ++i)
                {
                    var impact = GetAppContainer().Instantiate(primitive, Vector3.zero, Quaternion.identity);
                    impact.gameObject.SetActive(false);
                    Pool[i] = impact;
                }
            }

            public void Destroy()
            {
                foreach (var impact in Pool)
                {
                    Object.Destroy(impact);
                }
                Pool = null;
            }

            public BulletImpactParticles GetNext()
            {
                if (m_index >= Pool.Length)
                {
                    m_index = 0;
                }

                return Pool[m_index++];
            }
        }
        private static Dictionary<BulletImpactParticles, CircularPool> s_pools = new();


        [SerializeField]
        private ParticleSystem m_particleSystem;

        [SerializeField]
        private AudioTriggerExtended m_audioTrigger;

        private Transform m_weakParentLink;
        private Transform m_weakParent;


        [SerializeField]
        private int m_maxBulletImpactParticles = 64;

        private static void InitPool(BulletImpactParticles primitive)
        {
            if (s_pools.ContainsKey(primitive))
            {
                return;
            }

            s_pools[primitive] = new CircularPool(primitive, primitive.m_maxBulletImpactParticles);
        }

        public static void DestroyPools()
        {
            foreach (var p in s_pools.Values)
            {
                p.Destroy();
            }
            s_pools.Clear();
        }

        public static BulletImpactParticles Create(BulletImpactParticles primitive,
                                                   Transform parent)
        {
            InitPool(primitive);

            var impact = s_pools[primitive].GetNext();
            impact.gameObject.SetActive(true);
            impact.m_weakParentLink = parent;
            impact.UpdateWeakParent();

            impact.m_particleSystem.Stop();
            impact.m_particleSystem.Play();

            if (impact.m_audioTrigger != null)
            {
                impact.m_audioTrigger.PlayAudio();
            }

            return impact;
        }

        private void UpdateWeakParent()
        {
            if (m_weakParentLink == null)
            {
                return;
            }

            m_weakParent.position = m_weakParentLink.position;
            m_weakParent.rotation = m_weakParentLink.rotation;
            m_weakParent.localScale = m_weakParentLink.localScale;
        }

        private void Awake()
        {
            m_weakParent = new GameObject(name + "WeakParent").transform;
            transform.SetParent(m_weakParent);
        }

        private void OnDestroy()
        {
            Destroy(m_weakParent.gameObject);
        }

        private void Update()
        {
            if (!m_particleSystem.IsAlive() ||
                m_weakParentLink == null)
            {
                m_particleSystem.Stop();
                gameObject.SetActive(false);
                return;
            }

            UpdateWeakParent();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!m_particleSystem)
            {
                m_particleSystem = GetComponent<ParticleSystem>();
            }
        }
#endif
    }
}
