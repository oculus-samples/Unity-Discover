// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Discover.DroneRage.Game;
using Discover.DroneRage.PowerUps;
using Discover.Networking;
using Fusion;
using Meta.Utilities;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Discover.DroneRage.Weapons
{
    public class NetworkedWeaponController : NetworkBehaviour
    {
        private class NetworkedWeaponHitHandler : WeaponHitHandler
        {
            private WeaponHitHandler m_hitHandler;

            public NetworkedWeaponHitHandler(Weapon weapon) : base(weapon) => m_hitHandler = weapon.HitHandler;

            public override void ResolveHits(Vector3 shotOrigin, Vector3 shotDirection)
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    return;
                }

                m_hitHandler.ResolveHits(shotOrigin, shotDirection);
            }
        }

        [Networked(OnChanged = nameof(OnIsFiringChanged))]
        public NetworkBool IsFiring { get; private set; }

        [Networked(OnChanged = nameof(OnOwnerChanged))]
        public Player.Player Owner { get; set; }


        [SerializeField, AutoSetFromChildren]
        private Weapon m_controlledWeapon;


        [SerializeField, AutoSetFromChildren]
        private WeaponVisuals m_controlledWeaponVisuals;
        
        [SerializeField, AutoSetFromChildren]
        private PowerUpCollector m_powerUpCollector;


        [SerializeField]
        private GameObject[] m_laserSights = Array.Empty<GameObject>();

        public Weapon ControlledWeapon => m_controlledWeapon;

        private void Start()
        {
            m_controlledWeapon.HitHandler = new NetworkedWeaponHitHandler(m_controlledWeapon);
            if (!HasStateAuthority)
            {
                m_controlledWeapon.enabled = false;
                foreach (var laser in m_laserSights)
                {
                    laser.SetActive(false);
                }
            }
        }

        private void OnEnable()
        {
            m_controlledWeapon.WeaponFired += OnWeaponFired;
            m_controlledWeapon.StartedFiring += OnStartedFiring;
            m_controlledWeapon.StoppedFiring += OnStoppedFiring;
            m_controlledWeapon.DamageCallback += OnDamage;
        }

        private void OnDisable()
        {
            m_controlledWeapon.WeaponFired -= OnWeaponFired;
            m_controlledWeapon.StartedFiring -= OnStartedFiring;
            m_controlledWeapon.StoppedFiring -= OnStoppedFiring;
            m_controlledWeapon.DamageCallback -= OnDamage;
        }

        private void OnWeaponFired(Vector3 shotOrigin, Vector3 shotDirection)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            WeaponFiredClientRPC(shotOrigin, shotDirection);
        }

        private void OnStartedFiring()
        {
            if (!HasStateAuthority)
                return;

            IsFiring = true;
        }

        private void OnStoppedFiring()
        {
            if (!HasStateAuthority)
                return;

            IsFiring = false;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void WeaponFiredClientRPC(Vector3 shotOrigin, Vector3 shotDirection)
        {
            if (DroneRageGameController.Instance.HasStateAuthority)
            {
                m_controlledWeapon.HitHandler.ResolveHits(shotOrigin, shotDirection);
            }

            if (!m_controlledWeaponVisuals.gameObject.activeInHierarchy)
                return;

            m_controlledWeaponVisuals.OnWeaponFired(shotOrigin, shotDirection);
        }

        private static void OnIsFiringChanged(Changed<NetworkedWeaponController> changed)
        {
            var visuals = changed.Behaviour.m_controlledWeaponVisuals;
            if (!visuals.gameObject.activeInHierarchy)
                return;

            if (changed.Behaviour.IsFiring)
                visuals.OnStartedFiring();
            else
                visuals.OnStoppedFiring();
        }

        public void EquipWeapon(Handedness hand, Player.Player owner)
        {
            Debug.Log($"EquipWeapon - player {Runner.LocalPlayer.PlayerId}, owner {Object.StateAuthority.PlayerId}");
            Object.RequestStateAuthority();

            var weaponInputHandler = gameObject.AddComponent<WeaponInputHandler>();
            weaponInputHandler.ControlledWeapon = GetComponentInChildren<Weapon>();
            var interactionController = AppInteractionController.Instance;
            weaponInputHandler.Setup(interactionController.GetControllerGrabInteractor(hand), interactionController.GetHandGrabInteractor(hand), hand);

            m_controlledWeapon.enabled = true;
            foreach (var laser in m_laserSights)
            {
                laser.SetActive(true);
            }

            Owner = owner;
        }

        private static void OnOwnerChanged(Changed<NetworkedWeaponController> changed)
        {
            var player = changed.Behaviour.Owner;
            if (player != null)
            {
                player.OnDeath += () =>
                {
                    _ = changed.Behaviour.m_controlledWeaponVisuals.SpawnDroppedWeapon();
                    changed.Behaviour.gameObject.SetActive(false);
                };

                changed.Behaviour.m_powerUpCollector.Player = player;
            }
        }

        private void OnDamage(IDamageable damageableAffected, float hpAffected, bool targetDied)
        {
            Owner.TrackDamageStats(damageableAffected, hpAffected, targetDied);
        }
    }
}