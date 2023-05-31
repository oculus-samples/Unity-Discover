// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.DroneRage.Audio;
using Discover.DroneRage.Weapons;
using Discover.Utilities.Extensions;
using UnityEngine;
using UnityEngine.Assertions;
using static Discover.DroneRage.Bootstrapper.DroneRageAppContainerUtils;

namespace Discover.DroneRage.Enemies
{
    public class EnemyWeaponVisuals : MonoBehaviour
    {


        [SerializeField]
        private Weapon m_weapon;


        [SerializeField]
        private Transform m_muzzleTransform;


        [SerializeField]
        private ParticleSystem m_tracerPrefab;
        private ParticleSystem m_tracer;


        [SerializeField]
        private ParticleSystem m_muzzleFlashPrefab;
        private ParticleSystem m_muzzleFlash;


        [SerializeField]
        private AudioTriggerExtended[] m_fireSfx;

        private void Start()
        {
            Assert.IsNotNull(m_muzzleFlashPrefab, $"{nameof(m_muzzleFlash)} cannot be null.");
            Assert.IsNotNull(m_tracerPrefab, $"{nameof(m_tracer)} cannot be null.");

            m_muzzleFlash = GetAppContainer().Instantiate(m_muzzleFlashPrefab, m_muzzleTransform);
            var muzzleFlashTransform = m_muzzleFlash.transform;
            muzzleFlashTransform.localPosition = Vector3.zero;
            muzzleFlashTransform.localRotation = Quaternion.identity;
            muzzleFlashTransform.SetWorldScale(m_muzzleFlashPrefab.transform.localScale);

            m_tracer = GetAppContainer().Instantiate(m_tracerPrefab, m_muzzleTransform);
            var tracerTransform = m_tracer.transform;
            tracerTransform.localPosition = Vector3.zero;
            tracerTransform.localRotation = Quaternion.identity;
            tracerTransform.SetWorldScale(m_tracerPrefab.transform.localScale);
        }

        private void OnEnable()
        {
            m_weapon.WeaponFired += OnWeaponFired;
        }

        private void OnDisable()
        {
            m_weapon.WeaponFired -= OnWeaponFired;
        }

        public void OnWeaponFired(Vector3 shotOrigin, Vector3 shotDirection)
        {
            foreach (var sfx in m_fireSfx)
            {
                if (sfx != null)
                {
                    sfx.PlayAudio();
                }
            }

            if (m_muzzleFlash != null)
            {
                m_muzzleFlash.Stop(true);
                m_muzzleFlash.Play();
            }

            if (m_tracer != null)
            {
                m_tracer.Stop(true);
                m_tracer.transform.forward = shotDirection;
                m_tracer.Play();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_weapon == null)
            {
                m_weapon = GetComponent<Weapon>();
            }
        }
#endif
    }
}
