// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.DroneRage.Game;
using Discover.DroneRage.Weapons;
using Discover.Networking;
using Fusion;
using Meta.Utilities;
using UnityEngine;

namespace Discover.DroneRage.PowerUps
{
    public abstract class PowerUp : MonoBehaviour
    {
        [SerializeField, AutoSet] private NetworkObject m_networkObject;


        [SerializeField]
        private float m_floatHeightFloor = 1.2192f;

        [SerializeField]
        private float m_floatHeightObject = 0.4f;

        [SerializeField]
        private float m_floatBobOffset = 0.1f;

        [SerializeField]
        private float m_floatBobSpeed = 0.15f;

        [SerializeField]
        private Vector3 m_easingDists = new Vector2(0.01f, 1f / (0.08f - 0.01f));

        [SerializeField]
        private Quaternion m_rotationSpeed = Quaternion.AngleAxis(2f, Vector3.up);

        [SerializeField]
        private float m_attractSpeed = 0f;

        [SerializeField]
        private float m_lifetime = 20f;

        [SerializeField]
        private float m_blinktime = 3.5f;

        [SerializeField]
        private bool m_handsOnlyCollect = false;

        private Rigidbody m_rigidbody;

        public void Collect(Player.Player player, Weapon weapon = null)
        {
            if (m_handsOnlyCollect && weapon == null)
            {
                return;
            }

            OnCollect(player, weapon);
            m_networkObject.Despawn();
        }

        protected abstract void OnCollect(Player.Player player, Weapon weapon = null);

        private bool Float()
        {
            var targetHeight = m_floatHeightFloor;
            var reachedTarget = false;
            if (Physics.Raycast(m_rigidbody.position, Vector3.down, out var hit, 100.0f, LayerMask.GetMask("OVRScene")))
            {
                targetHeight = Mathf.Max(targetHeight, hit.point.y + m_floatHeightObject);
            }

            // continue to bob in the air as if riding waves
            targetHeight += m_floatBobOffset * Mathf.Sin(2f * Mathf.PI * m_floatBobSpeed * m_lifetime);

            var accel = new Vector3();
            var depth = targetHeight - m_rigidbody.position.y;
            if (Mathf.Abs(depth) < m_easingDists.x &&
                m_rigidbody.velocity.sqrMagnitude <= 0.2f)
            {
                // try to stay locked to the surface if we're very close
                accel = new Vector3(0f, depth / (Time.fixedDeltaTime * Time.fixedDeltaTime), 0f);
                accel -= m_rigidbody.velocity / Time.fixedDeltaTime;
                reachedTarget = true;
            }
            else if (m_rigidbody.position.y < targetHeight)
            {
                accel = Vector3.Lerp(Vector3.zero,
                                     -Physics.gravity,
                                     m_easingDists.y * depth);

                // when we're "underwater" apply some drag to slow us down
                accel -= 0.1f * m_rigidbody.velocity / Time.fixedDeltaTime;
                // Account for gravity
                accel -= Physics.gravity;
            }

            m_rigidbody.AddForce(accel,
                               ForceMode.Acceleration);

            return reachedTarget;
        }

        private void Attract(Transform target, float speed)
        {
            var vel = target.position - m_rigidbody.position;
            vel.y = 0f;
            vel = vel.normalized * speed;
            m_rigidbody.AddForce(vel, ForceMode.VelocityChange);
        }

        private void Age()
        {
            m_lifetime -= Time.fixedDeltaTime;

            if (m_lifetime < m_blinktime)
            {
                SetVisible((int)(2f * m_lifetime) % 2 == 0);
            }
            if (m_lifetime <= 0f)
            {
                m_networkObject.Despawn();
            }
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.enabled = visible;
            }
        }

        private void Start()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            if (!PhotonNetwork.Runner.IsMasterClient())
            {
                m_rigidbody.isKinematic = true;
                Destroy(this);
                return;
            }
        }

        private void FixedUpdate()
        {
            if (DroneRageGameController.Instance.GameOverState.GameOver)
            {
                m_networkObject.Despawn();
                return;
            }

            _ = Float();
            Attract(Player.Player.GetClosestLivePlayer(m_rigidbody.position).transform, m_attractSpeed);
            Age();
            m_rigidbody.MoveRotation(m_rotationSpeed * m_rigidbody.rotation);
        }
    }
}
