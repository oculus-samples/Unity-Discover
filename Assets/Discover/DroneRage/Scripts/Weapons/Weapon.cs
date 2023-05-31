// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Discover.DroneRage.PowerUps;
using Meta.Utilities;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Discover.DroneRage.Weapons
{
    public class Weapon : MonoBehaviour
    {
        public event Action<Vector3, Vector3> WeaponFired;
        public event Action StartedFiring;
        public event Action StoppedFiring;


        [SerializeField]
        private LayerMask m_raycastLayers = Physics.DefaultRaycastLayers;
        public LayerMask RaycastLayers => m_raycastLayers;


        [SerializeField]
        private Vector2 m_weaponSpread = new(0.01f, 0.05f);
        public Vector2 WeaponSpread
        {
            get => m_weaponSpread;
            set => m_weaponSpread = value;
        }


        [SerializeField]
        private Vector2 m_weaponDamage = new(8f, 14f);
        public Vector2 WeaponDamage => m_weaponDamage;


        [SerializeField]
        private Vector2 m_weaponKnockback = new(10f, 20f);
        public Vector2 WeaponKnockback => m_weaponKnockback;

        [SerializeField]
        private float m_fireRate = 0.0f;
        public float WeaponFireRate => m_fireRate;


        [SerializeField]
        protected Transform m_muzzleTransform;
        public Transform MuzzleTransform => m_muzzleTransform;

        [SerializeField, AutoSetFromChildren] public HandGrabInteractable HandGrabInteractable;
        [SerializeField, AutoSetFromChildren] public PowerUpCollector PowerUpCollector;

        [HideInInspector]
        public IDamageable.DamageCallback DamageCallback = null;

        public WeaponHitHandler HitHandler { get; set; } = null;

        private bool m_isFiring = false;
        private bool m_isFiringThisFrame = false;
        private float m_lastShotTime = 0.0f;

        private void Start()
        {
            HitHandler = new WeaponHitHandler(this);
        }

        private void Update()
        {
            var isRapidFire = m_fireRate > 0.05f;
            if (m_isFiring && isRapidFire && Time.time - m_lastShotTime > 1.0f / m_fireRate)
            {
                m_isFiringThisFrame = true;
                m_lastShotTime = Time.time;
            }
        }

        private void FixedUpdate()
        {
            if (m_isFiringThisFrame)
            {
                m_isFiringThisFrame = false;
                Shoot();
            }
        }

        public void StartFiring()
        {
            m_isFiring = true;
            m_isFiringThisFrame = true;
            StartedFiring?.Invoke();
        }

        public void StopFiring()
        {
            m_isFiring = false;
            m_isFiringThisFrame = false;
            StoppedFiring?.Invoke();
        }

        /// <summary>
        /// Fires the weapon, including hit resolution.
        /// </summary>
        public void Shoot()
        {
            var shotOrigin = m_muzzleTransform.position;
            var shotDir = m_muzzleTransform.TransformDirection(WeaponUtils.RandomSpread(m_weaponSpread));

            Debug.DrawRay(shotOrigin, shotDir, Color.red, 1f);

            if (HitHandler != null)
            {
                HitHandler.ResolveHits(shotOrigin, shotDir);
            }

            WeaponFired?.Invoke(shotOrigin, shotDir);
        }
    }

    public class WeaponHitHandler
    {

        private Weapon m_weapon;

        public WeaponHitHandler(Weapon weapon) => m_weapon = weapon;

        public virtual void ResolveHits(Vector3 shotOrigin, Vector3 shotDirection)
        {
            var hits = Physics.RaycastAll(shotOrigin,
                shotDirection,
                Mathf.Infinity,
                m_weapon.RaycastLayers,
                QueryTriggerInteraction.Ignore);

            if (hits.Length <= 0)
            {
                return;
            }

            var closestHit = hits[0];
            for (var i = 1; i < hits.Length; ++i)
            {
                if (hits[i].distance < closestHit.distance)
                {
                    closestHit = hits[i];
                }
            }

            var hitStrength = Random.Range(0f, 1f);

            if (closestHit.rigidbody)
            {
                closestHit.rigidbody.AddForceAtPosition(
                    Mathf.Lerp(m_weapon.WeaponKnockback.x, m_weapon.WeaponKnockback.y, hitStrength) * shotDirection,
                    closestHit.point,
                    ForceMode.Impulse);
            }

            var isFriendlyFire = closestHit.transform.GetComponent<Player.Player>() != null;

            foreach (var damageable in closestHit.transform.gameObject.GetComponents<IDamageable>())
            {
                var damage = Mathf.Lerp(m_weapon.WeaponDamage.x, m_weapon.WeaponDamage.y, hitStrength);
                if (isFriendlyFire)
                {
                    damage *= 0.2f;
                }
                damageable.TakeDamage(damage, closestHit.point, closestHit.normal, m_weapon.DamageCallback);
            }
        }
    }
}
