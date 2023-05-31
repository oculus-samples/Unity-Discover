// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.Assertions;

namespace Discover.DroneRage.Weapons
{
    public class BasicDamageable : MonoBehaviour, IDamageable
    {


        [SerializeField]
        private GameObject m_impactPrefab;


        [SerializeField]
        private BasicDamageablePhoton m_basicDamageablePhoton;

        private void Start()
        {
            Assert.IsNotNull(m_impactPrefab, $"{nameof(m_impactPrefab)} cannot be null.");
            Assert.IsNotNull(m_basicDamageablePhoton, $"{nameof(m_basicDamageablePhoton)} cannot be null.");
            m_basicDamageablePhoton.ImpactPrefab = m_impactPrefab;
        }

        public virtual void Heal(float healing, IDamageable.DamageCallback callback = null)
        {
        }

        public virtual void TakeDamage(float damage, Vector3 position, Vector3 normal, IDamageable.DamageCallback callback = null)
        {
            m_basicDamageablePhoton.OnHit(position, normal);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_basicDamageablePhoton == null)
            {
                m_basicDamageablePhoton = GetComponentInParent<BasicDamageablePhoton>();
            }
        }
#endif
    }
}
