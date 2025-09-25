// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Discover.DroneRage.Game;
using Discover.DroneRage.Scene;
using Discover.DroneRage.Weapons;
using Discover.Networking;
using Fusion;
using Meta.Utilities;
using NaughtyAttributes;
using UnityEngine;
using static Discover.DroneRage.Bootstrapper.DroneRageAppContainerUtils;
using Random = UnityEngine.Random;

namespace Discover.DroneRage.Enemies
{
    public class Enemy : MonoBehaviour, IDamageable
    {
        [Serializable]
        public struct Drop
        {
            public float Chance;
            public int NumRolls;
            public int NumToDrop;
            public SimulationBehaviour DropPrefab;
        }

        [Serializable]
        public struct MovementSettings
        {
            public static readonly MovementSettings Defaults =
                new()
                {
                    MaxVelocity = 8f,
                    MaxAcceleration = 18f,
                    EasingDists = new Vector2(0.01f, 1f / (2f - 0.01f)),
                    MaxAngularVelocity = 25f,
                    MaxAngularAcceleration = 90f,
                    EasingAngularDists = new Vector2(0.5f, 1f / (20f - 0.5f)),
                    HoverRadius = 0.4f,
                    HoverNoiseOffset = 0f,
                    HoverNoiseFrequency = new Vector3(0.2f, 0.2f, 0.2f)
                };

            public float MaxVelocity;


            public float MaxAcceleration;

            public Vector2 EasingDists;


            public float MaxAngularVelocity;


            public float MaxAngularAcceleration;


            public Vector2 EasingAngularDists;

            public float HoverRadius;


            public float HoverNoiseOffset;


            public Vector3 HoverNoiseFrequency;
        }

        [SerializeField, AutoSet] private NetworkObject m_networkObject;


        [SerializeField]
        private MovementSettings m_movementSettings = MovementSettings.Defaults;


        [SerializeField]
        private MovementSettings m_defaultMovementSettings = MovementSettings.Defaults;

        public float MaxVelocity
        {
            get => m_movementSettings.MaxVelocity;
            set => m_movementSettings.MaxVelocity = value;
        }

        public float MaxAcceleration
        {
            get => m_movementSettings.MaxAcceleration;
            set => m_movementSettings.MaxAcceleration = value;
        }

        public Vector2 EasingDists
        {
            get => m_movementSettings.EasingDists;
            set => m_movementSettings.EasingDists = value;
        }

        public float MaxAngularVelocity
        {
            get => m_movementSettings.MaxAngularVelocity;
            set => m_movementSettings.MaxAngularVelocity = value;
        }

        public float MaxAngularAcceleration
        {
            get => m_movementSettings.MaxAngularAcceleration;
            set => m_movementSettings.MaxAngularAcceleration = value;
        }

        public Vector2 EasingAngularDists
        {
            get => m_movementSettings.EasingAngularDists;
            set => m_movementSettings.EasingAngularDists = value;
        }

        public float HoverRadius
        {
            get => m_movementSettings.HoverRadius;
            set => m_movementSettings.HoverRadius = value;
        }

        public float HoverNoiseOffset
        {
            get => m_movementSettings.HoverNoiseOffset;
            set => m_movementSettings.HoverNoiseOffset = value;
        }

        public Vector3 HoverNoiseFrequency
        {
            get => m_movementSettings.HoverNoiseFrequency;
            set => m_movementSettings.HoverNoiseFrequency = value;
        }


        [SerializeField]
        public Vector2 DistributionSpread = new(0.85f, 0.55f);


        [SerializeField]
        public Vector3 DistributionOffset = new(0f, 0.1f, 0f);


        [SerializeField]
        public float WeaponAimRange = 0.2f;


        [SerializeField]
        private float m_dropYVel = 3f;


        [SerializeField]
        private Vector2 m_dropSpread = new(1f, 1f);


        [SerializeField]
        private Drop[] m_smallDrops;


        [SerializeField]
        private Drop[] m_largeDrops;


        [SerializeField]
        public EnemyProximitySensor ProxSensor;


        [HideInInspector]
        public Rigidbody Rigidbody;


        [HideInInspector]
        public Weapon[] Weapons;

        public Vector3 TargetPos = new(1.5f, 3f, 0f);
        public Transform LookTarget = null;
        private Player.Player m_targetPlayer = null;

        [ShowNativeProperty]
        public Player.Player TargetPlayer
        {
            get => m_targetPlayer;

            set
            {
                if (m_targetPlayer != null)
                {
                    m_targetPlayer.OnDeath -= OnTargetDeath;
                }

                m_targetPlayer = value;
                if (m_targetPlayer != null)
                {
                    m_targetPlayer.OnDeath += OnTargetDeath;
                }
            }
        }


        [SerializeField]
        public float Health = 100f;


        [SerializeField]
        public float PainThreshold = 12f;

        private EnemyState m_curState;
        private IDamageable.DamageCallback m_lastDamageCallback;

        [ShowNativeProperty] private string CurrentState => m_curState?.GetType()?.Name;

        public void SwitchState(EnemyState nextState)
        {
            var lastState = m_curState;
            m_curState.ExitState(this, nextState);
            m_curState = nextState;
            m_curState.EnterState(this, lastState);
        }

        protected void OnCollisionStay(Collision c)
        {
            m_curState.OnCollisionStay(this, c);

            var speed = c.relativeVelocity.magnitude;
            if (speed >= 2.5f &&
                c.gameObject != null &&
                c.gameObject.layer == LayerMask.NameToLayer("OVRScene"))
            {
                var cp = c.GetContact(0);
                TakeDamage(0.08f * speed * speed, cp.point, cp.normal, m_lastDamageCallback);
            }
        }

        internal void OnProximityStay(Collider c)
        {
            m_curState.OnProximityStay(this, c);
        }

        internal void OnTargetDeath()
        {
            TargetPlayer = null;
            SwitchState(new EnemyBehaviour.Distribute());
        }

        public bool IsInsideRoom()
        {
            return IsInsideRoom(Rigidbody.position);
        }

        public static bool IsInsideRoom(Vector3 pos)
        {
            var hits = Physics.RaycastAll(
                pos,
                Vector3.right,
                Mathf.Infinity,
                LayerMask.GetMask("OVRScene"),
                QueryTriggerInteraction.Ignore);
            var numWalls = 0;
            foreach (var hit in hits)
            {
                numWalls += hit.transform.TryGetComponent<Wall>(out _) ? 1 : 0;
            }

            return (numWalls & 1) == 1;
        }

        public static bool InView(Transform eye, Transform target)
        {
            var dir = (target.position - eye.position).normalized;
            dir = Quaternion.Inverse(eye.rotation) * dir;
            if (dir.z <= 0f)
            {
                return false;
            }

            dir /= dir.z;
            return Mathf.Abs(dir.x) <= 1f &&
                   Mathf.Abs(dir.y) <= 1f;
        }

        public bool CanSee(Player.Player targetPlayer)
        {
            return InView(transform, targetPlayer.transform) && targetPlayer.IsDetectable(transform);
        }

        public void ResetMovementSettings()
        {
            m_movementSettings = m_defaultMovementSettings;
        }

        public void UpdateMovementDefaults()
        {
            m_defaultMovementSettings = m_movementSettings;
        }

        private enum HoverComponent
        {
            X,
            Y,
            Z
        }

        // to maintain precision, do not allow our time offset to exceed an hour
        private const double HOVER_NOISE_MAX_TIME = 3600.0;

        private float GenerateHoverNoise(HoverComponent comp, float freqScale = 1f)
        {
            var x = (float)((Time.timeAsDouble + HoverNoiseOffset) % HOVER_NOISE_MAX_TIME) * freqScale * HoverNoiseFrequency[(int)comp];
            var y = (float)comp * 10f + HoverNoiseOffset;
            return 2f * Mathf.Clamp01(Mathf.PerlinNoise(x, y)) - 1f;
        }

        private float GenerateHoverSin(HoverComponent comp, float freqScale = 1f)
        {
            var x = (float)((Time.timeAsDouble + HoverNoiseOffset) % HOVER_NOISE_MAX_TIME) * freqScale * (HoverNoiseFrequency[(int)comp] * 0.4f + 0.6f);
            var y = (float)comp * 10f + HoverNoiseOffset;
            return Mathf.Sin(x + y);
        }

        private void RandomizeHover()
        {
            m_defaultMovementSettings.HoverNoiseOffset = Random.Range(-3600f, 3600f);
            m_defaultMovementSettings.HoverNoiseFrequency = Vector3.Scale(
                new Vector3(
                    0.9f * Random.value + 0.1f,
                    0.9f * Random.value + 0.1f,
                    0.9f * Random.value + 0.1f),
                HoverNoiseFrequency);

            m_movementSettings.HoverNoiseOffset = m_defaultMovementSettings.HoverNoiseOffset;
            m_movementSettings.HoverNoiseFrequency = m_defaultMovementSettings.HoverNoiseFrequency;
        }

        public bool FlyTo(Vector3 target)
        {
            var targetVel = target - Rigidbody.position;
            targetVel = Vector3.Lerp(
                Vector3.zero,
                targetVel.normalized * MaxVelocity,
                EasingDists.y * (Mathf.Sqrt(targetVel.magnitude) - EasingDists.x));
            var reachedTarget = targetVel.sqrMagnitude <= 0.1f;
            targetVel -= Rigidbody.linearVelocity;

            // We want to try to accelerate to this velocity within the next fixed time step
            var accel = targetVel / Time.fixedDeltaTime;
            if (accel.sqrMagnitude > MaxAcceleration * MaxAcceleration)
            {
                accel = accel.normalized * MaxAcceleration;
            }

            // Account for gravity
            accel -= Physics.gravity;

            Rigidbody.AddForce(
                accel,
                ForceMode.Acceleration);
            return reachedTarget;
        }

        public void HoverAround(Vector3 target)
        {
            var noise = Vector3.zero;
            if (HoverRadius != 0f)
            {
                noise = HoverRadius * new Vector3(
                    GenerateHoverNoise(HoverComponent.X),
                    GenerateHoverNoise(HoverComponent.Y),
                    GenerateHoverNoise(HoverComponent.Z));
            }

            _ = FlyTo(target + noise);
        }

        public void CircleStrafe(Vector3 circleCenter, bool clockwise)
        {
            var targetVel = Rigidbody.position - circleCenter;
            var circleRadius = targetVel.magnitude;
            targetVel = Vector3.ProjectOnPlane(targetVel, Vector3.up).normalized;
            targetVel = Vector3.Cross(targetVel, Vector3.up);
            targetVel = (clockwise ? MaxVelocity : -MaxVelocity) * targetVel;

            var closeVel = Rigidbody.position + Time.fixedDeltaTime * targetVel;
            closeVel -= circleCenter;
            closeVel = (closeVel.magnitude - circleRadius) * closeVel.normalized;
            targetVel += closeVel;

            // add some vertical noise to make the hovering look less perfect similar to HoverAround
            var hoverNoise = Vector3.up;
            if (HoverRadius != 0f)
            {
                hoverNoise *= 0.5f * HoverRadius * GenerateHoverSin(HoverComponent.Y, 2f * Mathf.PI);
            }

            targetVel += hoverNoise;

            targetVel -= Rigidbody.linearVelocity;

            // We want to try to accelerate to this velocity within the next fixed time step
            var accel = targetVel / Time.fixedDeltaTime;
            if (accel.sqrMagnitude > MaxAcceleration * MaxAcceleration)
            {
                accel = accel.normalized * MaxAcceleration;
            }

            // Account for gravity
            accel -= Physics.gravity;

            Rigidbody.AddForce(
                accel,
                ForceMode.Acceleration);
        }

        public void MoveAlong(Vector3 dir)
        {
            var targetVel = MaxVelocity * dir;

            // add some vertical noise to make the hovering look less perfect similar to HoverAround
            var hoverNoise = Vector3.up;
            if (HoverRadius != 0f)
            {
                hoverNoise *= 0.5f * HoverRadius * GenerateHoverSin(HoverComponent.Y, 2f * Mathf.PI);
            }

            targetVel += hoverNoise;

            targetVel -= Rigidbody.linearVelocity;

            // We want to try to accelerate to this velocity within the next fixed time step
            var accel = targetVel / Time.fixedDeltaTime;
            if (accel.sqrMagnitude > MaxAcceleration * MaxAcceleration)
            {
                accel = accel.normalized * MaxAcceleration;
            }

            // Account for gravity
            accel -= Physics.gravity;

            Rigidbody.AddForce(
                accel,
                ForceMode.Acceleration);
        }

        public bool AimAlong(Vector3 dir)
        {
            var rot = Rigidbody.rotation * Vector3.forward;
            Debug.DrawRay(Rigidbody.position, rot, Color.blue);

            var angularVel = Vector3.Angle(rot, dir);
            angularVel = Mathf.Lerp(
                0f,
                MaxAngularVelocity,
                EasingAngularDists.y * (angularVel - EasingAngularDists.x));
            rot = Vector3.Cross(rot, dir).normalized * angularVel;
            rot -= Rigidbody.angularVelocity;

            // We want to try to accelerate to this angular velocity within the next fixed time step
            rot /= Time.fixedDeltaTime;
            if (rot.sqrMagnitude > MaxAngularAcceleration * MaxAngularAcceleration)
            {
                rot = rot.normalized * MaxAngularAcceleration;
            }

            Rigidbody.AddTorque(
                rot,
                ForceMode.Acceleration);
            return angularVel == 0f;
        }

        public bool AimTowards(Vector3 target)
        {
            return AimAlong((target - Rigidbody.position).normalized);
        }

        public bool AimAway(Vector3 target)
        {
            return AimAlong((Rigidbody.position - target).normalized);
        }

        public bool KeepUpright(Vector3 up)
        {
            var rot = Rigidbody.rotation * Vector3.up;
            Debug.DrawRay(Rigidbody.position, rot, Color.green);

            var forward = Rigidbody.rotation * Vector3.forward;
            up = Vector3.ProjectOnPlane(up, forward);
            Debug.DrawRay(Rigidbody.position, up, Color.magenta);

            var angularVel = Vector3.Angle(rot, up);
            angularVel = Mathf.Lerp(
                0f,
                MaxAngularVelocity,
                EasingAngularDists.y * (angularVel - EasingAngularDists.x));
            rot = Vector3.Cross(rot, up).normalized * angularVel;

            // We want to try to accelerate to this angular velocity within the next fixed time step
            rot /= Time.fixedDeltaTime;

            Rigidbody.AddTorque(
                rot,
                ForceMode.Acceleration);
            return angularVel == 0f;
        }

        public void BarrelRoll(bool clockwise)
        {
            var rot = Rigidbody.rotation * Vector3.forward;
            rot *= clockwise ? MaxAngularVelocity : -MaxAngularVelocity;

            // We want to try to accelerate to this angular velocity within the next fixed time step
            rot /= Time.fixedDeltaTime;
            Rigidbody.AddTorque(
                rot,
                ForceMode.Acceleration);
        }

        public void AimWeaponsAt(Vector3 target)
        {
            foreach (var w in Weapons)
            {
                target = w.MuzzleTransform.InverseTransformPoint(target);
                target.y = 0f;
                target = target.normalized;
                target.z = Mathf.Abs(target.z);
                target.x = Mathf.Clamp(target.x, -WeaponAimRange, WeaponAimRange);
                target = target.normalized;
                target = w.MuzzleTransform.TransformDirection(target);

                var rot = Quaternion.LookRotation(target, Vector3.up);
                w.MuzzleTransform.rotation = rot;
            }
        }

        public void DropSmallItems()
        {
            if (DroneRageGameController.Instance.GameOverState.GameOver)
            {
                return;
            }

            foreach (var d in m_smallDrops)
            {
                for (var roll = 0; roll < d.NumRolls; ++roll)
                {
                    if (Random.value > d.Chance)
                    {
                        continue;
                    }

                    for (var i = 0; i < d.NumToDrop; ++i)
                    {
                        var drop = GetAppContainer().NetInstantiate(d.DropPrefab, Rigidbody.position, Quaternion.identity);
                        if (drop.TryGetComponent(out Rigidbody dropBody))
                        {
                            var vel = WeaponUtils.RandomSpread(m_dropSpread);
                            vel.z = vel.y;
                            vel.y = 0.75f * m_dropYVel;
                            dropBody.AddForce(vel, ForceMode.VelocityChange);
                        }
                    }
                }
            }
        }

        public void DropLargeItem(bool force = false)
        {
            if (DroneRageGameController.Instance.GameOverState.GameOver)
            {
                return;
            }

            foreach (var d in m_largeDrops)
            {
                if (!force && Random.value > d.Chance)
                {
                    continue;
                }

                var drop = GetAppContainer().NetInstantiate(d.DropPrefab, Rigidbody.position, Quaternion.identity);
                if (drop.TryGetComponent(out Rigidbody dropBody))
                {
                    // toss the large item towards the closest player
                    var vel = Player.Player.GetClosestLivePlayer(Rigidbody.position).transform.position - Rigidbody.position;
                    var dist = vel.magnitude;
                    vel.y = 0f;
                    vel = Mathf.Min(0.8f * dist, Mathf.Max(m_dropSpread.x, m_dropSpread.y)) * vel.normalized;
                    vel.y = m_dropYVel;
                    dropBody.AddForce(vel, ForceMode.VelocityChange);
                }

                return;
            }
        }

        public void Heal(float healing, IDamageable.DamageCallback callback = null)
        {
            Health += healing;
        }

        public void TakeDamage(float damage, Vector3 position, Vector3 normal, IDamageable.DamageCallback callback = null)
        {
            Health -= damage;

            if (Health > 0 &&
                m_curState is not EnemyBehaviour.EnterArena &&
                m_curState is not EnemyBehaviour.ExitArena)
            {
                if (EnemyBehaviour.Pain.CausesPain(this, damage))
                {
                    SwitchState(new EnemyBehaviour.Pain());
                }
                else if (m_curState is not EnemyBehaviour.Attack &&
                         m_curState is not EnemyBehaviour.Pain &&
                         m_curState is not EnemyBehaviour.Dodge &&
                         m_curState is not EnemyBehaviour.HideWeakpoint &&
                         EnemyBehaviour.Dodge.ShouldDodge())
                {
                    SwitchState(new EnemyBehaviour.Dodge());
                }
                else if (Random.value <= 0.25f)
                {
                    SwitchState(new EnemyBehaviour.HideWeakpoint());
                }
            }

            if (callback != null)
            {
                m_lastDamageCallback = callback;

                if (Health < 0f)
                {
                    damage += Health;
                }

                if (damage > 0f)
                {
                    callback(this, damage, Health <= 0f);
                }
            }
        }

        public void DestroySelf(bool explode = true)
        {
            if (!explode)
            {
                var edh = gameObject.GetComponent<EnemyDeathHandler>();
                if (edh != null)
                {
                    edh.DestroyedPrefab = null;
                }
            }

            m_networkObject.Despawn();
        }

        private void Start()
        {
            Rigidbody = GetComponent<Rigidbody>();

            if (!PhotonNetwork.Runner.IsMasterClient())
            {
                Rigidbody.isKinematic = true;
                Destroy(this);
                return;
            }

            Weapons = GetComponents<Weapon>();
            ResetMovementSettings();
            m_curState = new EnemyBehaviour.EnterArena();
            m_curState.EnterState(this, m_curState);

            RandomizeHover();
        }

        private void OnDestroy()
        {
            TargetPlayer = null;
            if (Spawner.Instance != null)
            {
                Spawner.Instance.LOGDroneKill();
            }
        }

        private void FixedUpdate()
        {
            if (Health <= 0f &&
                m_curState is not EnemyBehaviour.Die &&
                m_curState is not EnemyBehaviour.DieExplode &&
                m_curState is not EnemyBehaviour.DieFall &&
                m_curState is not EnemyBehaviour.DieMalfunction)
            {
                SwitchState(new EnemyBehaviour.Die());
            }

            m_curState.UpdateState(this);
        }
    }
}
