// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Linq;
using Discover.DroneRage.Audio;
using Discover.DroneRage.Enemies;
using Discover.DroneRage.Game;
using Discover.DroneRage.UI.HealthIndicator;
using Discover.DroneRage.Weapons;
using Discover.Networking;
using Discover.Utilities;
using Fusion;
using Meta.Utilities;
using UnityEngine;
using static Discover.DroneRage.Bootstrapper.DroneRageAppContainerUtils;
using Random = UnityEngine.Random;

namespace Discover.DroneRage.Player
{
    public class Player : NetworkMultiton<Player>, IDamageable
    {
        public static int NumPlayers => Players.Count;
        public static int PlayersLeft => LivePlayers.Count();

        public static Player LocalPlayer;

        public static ReadOnlyList Players => Instances;
        public static IEnumerable<Player> LivePlayers => Players.Where(p => p.Health > 0);

        public event Action OnHpChange;
        public event Action OnDeath;

        public GameObject HealthUI;
        public GameObject CriticalHealthUI;
        public GameObject DamageVfx;
        public GameObject DamageBehindVfx;

        private CapsuleCollider m_capsuleCollider;

        [AutoSet] public PlayerStats PlayerStats;

        public int PlayerUid => Object.StateAuthority.PlayerId;

        [Networked(OnChanged = nameof(OnHealthChanged))]
        public float Health { get; private set; }

        private OVRCameraRig CameraRig => PhotonNetwork.CameraRig;

        public static Player GetClosestLivePlayer(Vector3 position)
        {
            return PlayersLeft <= 0 ? null : LivePlayers.OrderBy(p => (position - p.transform.position).sqrMagnitude).First();
        }

        public static Player GetRandomLivePlayer()
        {
            return PlayersLeft <= 0 ? null : LivePlayers.ElementAt(Random.Range(0, PlayersLeft));
        }

        private static readonly Vector3[] s_detectionOffsets =
        {
            Vector3.zero, new Vector3(
                0.99f,
                0.49f,
                0.99f),
            new Vector3(
                -0.99f,
                0.49f,
                -0.99f)
        };

        public bool IsDetectable(Transform eye)
        {
            foreach (var offset in s_detectionOffsets)
            {
                var dir = Vector3.Scale(
                    offset, new Vector3(
                        m_capsuleCollider.radius,
                        m_capsuleCollider.height,
                        m_capsuleCollider.radius));
                dir += m_capsuleCollider.center;
                dir = (transform.TransformPoint(dir) - eye.position).normalized;
                var hits = Physics.RaycastAll(
                    eye.position,
                    dir,
                    Mathf.Infinity,
                    LayerMask.GetMask("OVRScene", "Player"),
                    QueryTriggerInteraction.Ignore);
                if (hits.Length <= 0)
                {
                    continue;
                }

                var closestHit = hits[0];
                for (var i = 1; i < hits.Length; ++i)
                {
                    if (hits[i].transform.gameObject != gameObject &&
                        hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Player"))
                    {
                        // For purposes of detection, don't allow a player to hide behind other players.
                        continue;
                    }

                    if (hits[i].distance < closestHit.distance)
                    {
                        closestHit = hits[i];
                    }
                }

                if (closestHit.transform.gameObject == gameObject)
                {
                    return true;
                }
            }

            return false;
        }


        public void SetupPlayer()
        {
            Object.RequestStateAuthority();

            Health = 100;

            var playerInputHandler = gameObject.AddComponent<PlayerInputHandler>();
            playerInputHandler.SetTargetTransform(CameraRig.centerEyeAnchor);
            LocalPlayer = this;

            if (CriticalHealthUI != null)
            {
                _ = GetAppContainer().Instantiate(CriticalHealthUI, transform);
            }

            OnHpChange += () =>
            {
                DroneRageAudioManager.Instance.SetHealth(Mathf.RoundToInt(Health));
            };
        }

        public void Heal(float healing, IDamageable.DamageCallback callback = null)
        {
            HealOwnerRpc(healing);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void HealOwnerRpc(float healing)
        {
            if (!HasStateAuthority)
                return;

            if (Health <= 0)
            {
                return;
            }

            Health = Mathf.Min(100f, Health + healing);
            HealClientRPC(healing);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void HealClientRPC(float healing)
        {
            PlayerStats.HealingReceived += healing;

            if (this == LocalPlayer)
            {
                DroneRageAudioManager.Instance.HealSfx.Play();
            }
        }

        public void TakeDamage(float damage, Vector3 position, Vector3 normal, IDamageable.DamageCallback callback = null)
        {
            TakeDamageOwnerRPC(damage, position, normal);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void TakeDamageOwnerRPC(float damage, Vector3 position, Vector3 normal)
        {
            if (!HasStateAuthority)
                return;

            Health -= damage;
            TakeDamageClientRPC(damage, position, normal);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void TakeDamageClientRPC(float damage, Vector3 position, Vector3 normal)
        {
            PlayerStats.DamageTaken += damage;
            CreateHitFX(damage, position, normal);
        }

        private static void OnHealthChanged(Changed<Player> changed)
        {
            changed.Behaviour.OnHpChange?.Invoke();
            if (changed.Behaviour.Health <= 0f)
            {
                changed.Behaviour.Die();
            }
        }

        private void CreateHitFX(float damage, Vector3 position, Vector3 normal)
        {
            if (!HasStateAuthority)
            {
                var rot = Quaternion.LookRotation(
                    normal,
                    transform.up * (2f * Random.Range(0, 2) - 1f));
                _ = GetAppContainer().Instantiate(
                    DamageVfx,
                    transform.position,
                    rot,
                    transform);
                return;
            }

            var dir = (position - CameraRig.centerEyeAnchor.position).normalized;
            var forward = CameraRig.centerEyeAnchor.rotation * Vector3.forward;
            if (Vector3.Angle(forward, dir) <= 50f)
            {
                var rot = Quaternion.LookRotation(
                    dir,
                    CameraRig.centerEyeAnchor.up * (2f * Random.Range(0, 2) - 1f));
                _ = GetAppContainer().Instantiate(
                    DamageVfx,
                    CameraRig.centerEyeAnchor.position,
                    rot,
                    CameraRig.centerEyeAnchor);
            }
            else
            {
                _ = GetAppContainer().Instantiate(
                    DamageBehindVfx,
                    CameraRig.centerEyeAnchor.position,
                    CameraRig.centerEyeAnchor.rotation,
                    CameraRig.centerEyeAnchor);
            }
        }

        private void Die()
        {
            Health = 0;

            Debug.Log("Dying and swapping Player UID: " + PlayerUid + " with: " + PlayersLeft);

            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            OnDeath?.Invoke();
            if (PlayersLeft <= 0)
            {
                DroneRageGameController.Instance.TriggerGameOver(false);
            }
        }

        public void TrackDamageStats(IDamageable damagableAffected, float hpAffected, bool targetDied)
        {
            if (DroneRageGameController.Instance.GameOverState.GameOver ||
                damagableAffected is not Enemy)
            {
                return;
            }

            TrackDamageStatsOwnerRPC(hpAffected, targetDied);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void TrackDamageStatsOwnerRPC(float hpAffected, bool targetDied)
        {
            ++PlayerStats.ShotsHit;
            PlayerStats.DamageDealt += hpAffected;
            PlayerStats.EnemiesKilled += targetDied ? 1u : 0u;

            var dmg = (uint)Mathf.Ceil(hpAffected);
            PlayerStats.Score += 10u * dmg + (targetDied ? 1000u : 0u);
        }

        public void OnWeaponFired(Vector3 shotOrigin, Vector3 shotDirection)
        {
            if (DroneRageGameController.Instance.GameOverState.GameOver)
            {
                return;
            }

            ++PlayerStats.ShotsFired;
        }

        public void OnWaveAdvance()
        {
            ++PlayerStats.WavesSurvived;
        }

        private void Start()
        {
            Debug.Log("Players Left: " + PlayersLeft + " numPlayers: " + NumPlayers);

            m_capsuleCollider = GetComponent<CapsuleCollider>();

            if (HealthUI != null)
            {
                var hui = Instantiate(HealthUI, transform);
                hui.GetComponent<HealthUI>().Owner = this;
            }
        }

        private void FixedUpdate()
        {
            if (DroneRageGameController.Instance != null &&
                !DroneRageGameController.Instance.GameOverState.GameOver &&
                Health > 0)
            {
                ++PlayerStats.TicksSurvived;
            }
        }
    }
}