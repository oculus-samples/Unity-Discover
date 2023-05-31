// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.DroneRage.Scene;
using Discover.DroneRage.Weapons;
using UnityEngine;

namespace Discover.DroneRage.Enemies
{
    public class EnemyBehaviour
    {
        public class EnterArena : EnemyState
        {
            private void SelectEntrance(Enemy e)
            {
                e.TargetPos = e.Rigidbody.position.y > Spawner.Instance.RoomSize.y
                    ? Spawner.Instance.GetRandomRoomEntrance(e.TargetPlayer)
                    : Spawner.Instance.GetClosestRoomEntrance(e.TargetPlayer.transform, e.Rigidbody.position);
            }

            public void EnterState(Enemy e, EnemyState lastState)
            {
                if (e.TargetPlayer == null)
                {
                    var targetPlayer = Player.Player.GetRandomLivePlayer();
                    if (targetPlayer == null)
                    {
                        e.SwitchState(new ExitArena());
                        return;
                    }
                    else
                    {
                        e.TargetPlayer = targetPlayer;
                        e.LookTarget = targetPlayer.gameObject.transform;
                    }
                }

                SelectEntrance(e);

                // enter fast
                e.MaxVelocity = 1.5f * e.MaxVelocity;
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
                e.MoveAlong((e.Rigidbody.position - c.ClosestPointOnBounds(e.Rigidbody.position)).normalized);
                if (!c.gameObject.TryGetComponent<Wall>(out _))
                {
                    var pos = e.Rigidbody.position;
                    pos.y = 0.5f * (Spawner.Instance.RoomMinExtent.y + Spawner.Instance.RoomMaxExtent.y);
                    if (Enemy.IsInsideRoom(pos))
                    {
                        e.SwitchState(new Distribute());
                    }
                }
            }

            public void UpdateState(Enemy e)
            {
                _ = e.AimTowards(e.TargetPos);
                _ = e.KeepUpright(Vector3.up);
                if (e.FlyTo(e.TargetPos))
                {
                    e.SwitchState(new Distribute());
                }
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
                e.ResetMovementSettings();
            }
        }

        public class ExitArena : EnemyState
        {
            private bool m_reachedEntrance = false;

            public void EnterState(Enemy e, EnemyState lastState)
            {
                e.TargetPos = e.Rigidbody.position;
                e.TargetPos.y = Spawner.Instance.RoomMaxExtent.y + 2f * Spawner.Instance.EntranceYOffset;
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
                e.MoveAlong((e.Rigidbody.position - c.ClosestPointOnBounds(e.Rigidbody.position)).normalized);
            }

            public void UpdateState(Enemy e)
            {
                _ = e.AimTowards(e.TargetPos);
                _ = e.KeepUpright(Vector3.up);
                if (e.FlyTo(e.TargetPos))
                {
                    if (!m_reachedEntrance)
                    {
                        e.TargetPos = Spawner.Instance.GetClosestSpawnPoint(e.Rigidbody.position);
                        m_reachedEntrance = true;
                    }
                    else
                    {
                        e.DestroySelf(false);
                    }
                }
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
            }
        }

        public class Plan : EnemyState
        {
            private enum Strategy
            {
                ATTACK,
                RELOCATE = ATTACK + 3,
                PLAN,
                NUM_STRATS = PLAN + 2
            }

            private enum MovePattern
            {
                FORWARD,
                FORWARD_PLANE,
                BACKWARD,
                BACKWARD_PLANE = BACKWARD + 2,
                UP,
                DOWN,
                CIRCLE_STRAFE,
                NUM_PATTERNS = CIRCLE_STRAFE + 4
            }

            private static readonly float[] s_thinkTimePerWave = { 0.75f, 0.5f, 0.3f, 0.2f, 0.1f, 0.083f, 0.066f, 0.066f };
            private static readonly float[] s_speedScalePerWave = { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.22f, 0.25f, 0.25f };
            private MovePattern m_movePattern;
            private bool m_clockwise = false;
            private float m_thinkTime = 0f;

            public void EnterState(Enemy e, EnemyState lastState)
            {
                m_movePattern = e.LookTarget != null &&
                    (e.Rigidbody.position - e.LookTarget.position).magnitude <= 1.0f
                    ? MovePattern.BACKWARD
                    : (MovePattern)Random.Range((int)MovePattern.FORWARD, (int)MovePattern.NUM_PATTERNS);
                m_clockwise = Random.value < 0.5f;
                e.MaxVelocity = s_speedScalePerWave[Spawner.Instance.Wave] * e.MaxVelocity;

                // Since we Plan frequently, use this opportunity to have a failsafe check in case
                // we were knocked out of the room. We don't want to check this constantly as it
                // is not a completely trivial operation perf-wise.
                if (!e.IsInsideRoom())
                {
                    e.SwitchState(new EnterArena());
                }
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
                var dir = e.Rigidbody.position - c.ClosestPointOnBounds(e.Rigidbody.position);
                var sDist = dir.sqrMagnitude;
                dir = dir.normalized;
                e.MoveAlong(dir);

                dir = e.transform.InverseTransformDirection(dir);
                m_clockwise = dir.x >= 0f;
                m_movePattern = sDist >= 0.06f ? MovePattern.CIRCLE_STRAFE : dir.z >= 0f ? MovePattern.FORWARD : MovePattern.BACKWARD;
            }

            public void UpdateState(Enemy e)
            {
                switch (m_movePattern)
                {
                    case MovePattern.FORWARD:
                    case MovePattern.FORWARD_PLANE:
                        var rot = m_movePattern == MovePattern.FORWARD ? e.Rigidbody.rotation :
                                                                              Quaternion.AngleAxis(e.Rigidbody.rotation.eulerAngles.y,
                                                                                                   Vector3.up);
                        e.MoveAlong(rot * Vector3.forward);

                        if (e.LookTarget != null &&
                            (e.Rigidbody.position - e.LookTarget.position).magnitude <= 1.2f)
                        {
                            m_movePattern = MovePattern.CIRCLE_STRAFE;
                        }
                        break;
                    case MovePattern.BACKWARD:
                    case MovePattern.BACKWARD + 1:
                        e.MoveAlong(e.Rigidbody.rotation * Vector3.back);
                        break;
                    case MovePattern.BACKWARD_PLANE:
                        e.MoveAlong(Quaternion.AngleAxis(e.Rigidbody.rotation.eulerAngles.y, Vector3.up) * Vector3.back);
                        break;
                    case MovePattern.UP:
                        e.MoveAlong(Vector3.up);

                        if (e.Rigidbody.position.y >= Spawner.Instance.RoomSize.y - 0.75f)
                        {
                            m_movePattern = MovePattern.CIRCLE_STRAFE;
                        }
                        break;
                    case MovePattern.DOWN:
                        e.MoveAlong(Vector3.down);

                        if (e.Rigidbody.position.y <= 0.75f)
                        {
                            m_movePattern = MovePattern.CIRCLE_STRAFE;
                        }
                        break;
                    default:
                        e.CircleStrafe(e.LookTarget != null ? e.LookTarget.position : Vector3.zero, m_clockwise);
                        break;
                }
                _ = e.AimTowards(e.LookTarget != null ? e.LookTarget.position : Vector3.zero);
                _ = e.KeepUpright(Vector3.up);

                m_thinkTime += Time.fixedDeltaTime;
                if (m_thinkTime > s_thinkTimePerWave[Spawner.Instance.Wave])
                {
                    var strategy = (Strategy)Random.Range(0, (int)Strategy.NUM_STRATS);
                    switch (strategy)
                    {
                        case Strategy.ATTACK:
                        case Strategy.ATTACK + 1:
                        case Strategy.ATTACK + 2:
                            if ((Random.value <= 0.15f || Enemy.InView(e.TargetPlayer.transform, e.transform)) &&
                                (Random.value <= 0.2f || e.CanSee(e.TargetPlayer)))
                            {
                                e.SwitchState(new Attack());
                                return;
                            }
                            m_thinkTime = 0f;
                            break;
                        case Strategy.RELOCATE:
                            if (Random.value <= 0.05f ||
                                e.CanSee(e.TargetPlayer))
                            {
                                e.SwitchState(new Distribute());
                                return;
                            }
                            else
                            {
                                e.SwitchState(new Relocate());
                            }
                            return;
                        default:
                            m_thinkTime = 0f;
                            break;
                    }
                }
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
                e.ResetMovementSettings();
            }
        }

        public class Attack : EnemyState
        {
            private static readonly Vector2[] s_weaponSpreadPerWave = { new Vector2(0.07f,  0.12f),
                                                                      new Vector2(0.06f,  0.10f),
                                                                      new Vector2(0.05f,  0.08f),
                                                                      new Vector2(0.035f, 0.07f),
                                                                      new Vector2(0.02f,  0.06f),
                                                                      new Vector2(0.01f,  0.05f),
                                                                      new Vector2(0.01f,  0.05f),
                                                                      new Vector2(0.01f,  0.05f) };
            private const float PRE_ATTACK_TIME = 0.5f;
            private const float REFIRE_TIME = 0.05f;
            private const float POST_ATTACK_TIME = 0.3f;
            private const int NUM_VOLLEYS = 2;

            private int m_weaponIndex = 0;
            private int m_volley = 0;
            private float m_timer = PRE_ATTACK_TIME;

            public void EnterState(Enemy e, EnemyState lastState)
            {
                e.TargetPos = e.Rigidbody.position;

                // track the player more slowly when we're aiming so they might dodge
                e.MaxAngularVelocity = 0.3f * e.MaxAngularVelocity;
                e.MaxAngularAcceleration = 0.2f * e.MaxAngularAcceleration;

                foreach (var w in e.Weapons)
                {
                    w.WeaponSpread = s_weaponSpreadPerWave[Spawner.Instance.Wave];
                }
                // only aim the weapons at the start so that we give the player more leeway to dodge
                e.AimWeaponsAt(e.LookTarget != null ? e.LookTarget.position : Vector3.zero);
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
                // We're focused on shooting, so they must be the ones in the way
            }

            public void UpdateState(Enemy e)
            {
                e.HoverAround(e.TargetPos);
                _ = e.AimTowards(e.LookTarget != null ? e.LookTarget.position : Vector3.zero);
                _ = e.KeepUpright(Vector3.up);

                if (m_timer <= 0f)
                {
                    if (m_weaponIndex >= e.Weapons.Length)
                    {
                        e.SwitchState(new Plan());
                        return;
                    }

                    e.Weapons[m_weaponIndex].Shoot();
                    ++m_weaponIndex;

                    if (m_weaponIndex < e.Weapons.Length)
                    {
                        m_timer = REFIRE_TIME;
                    }
                    else
                    {
                        if (m_volley < NUM_VOLLEYS)
                        {
                            m_weaponIndex = 0;
                            ++m_volley;
                            m_timer = REFIRE_TIME;
                        }
                        else
                        {
                            m_timer = POST_ATTACK_TIME;
                        }
                    }
                }
                else
                {
                    m_timer -= Time.fixedDeltaTime;
                }
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
                e.ResetMovementSettings();
            }
        }

        public class Pain : EnemyState
        {
            private static readonly float[] s_painChancePerWave = { 1f, 1f, 0.75f, 0.73f, 0.7f, 0.7f, 0.65f, 0.5f };
            private float m_timeLeft = 0.5f;
            private bool m_clockwise;

            public static bool CausesPain(Enemy e, float damage)
            {
                return damage >= e.PainThreshold &&
                       Random.value <= s_painChancePerWave[Spawner.Instance.Wave];
            }

            public void EnterState(Enemy e, EnemyState lastState)
            {
                m_clockwise = Random.value < 0.5f;

                if (Random.value <= 0.3f * s_painChancePerWave[Spawner.Instance.Wave])
                {
                    // we're in so much pain, we're stopped in our tracks
                    e.TargetPos = e.Rigidbody.position;
                }
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
                // We're in too much pain to react to someone about to bump into us
            }

            public void UpdateState(Enemy e)
            {
                e.HoverAround(e.TargetPos);
                _ = e.AimTowards(e.LookTarget != null ? e.LookTarget.position : Vector3.zero);
                e.BarrelRoll(m_clockwise);

                m_timeLeft -= Time.fixedDeltaTime;
                if (m_timeLeft <= 0f)
                {
                    e.SwitchState(new Plan());
                }
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
            }
        }

        public class HideWeakpoint : EnemyState
        {
            private float m_timeLeft = 1.5f;

            public void EnterState(Enemy e, EnemyState lastState)
            {
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
                var dir = new Vector3
                {
                    y = e.Rigidbody.position.y - c.ClosestPointOnBounds(e.Rigidbody.position).y
                };
                dir.y = dir.y == 0f ? -1f : dir.y;
                dir = dir.normalized;
                e.MoveAlong(dir);
            }

            public void UpdateState(Enemy e)
            {
                e.HoverAround(e.TargetPos);
                _ = e.AimAway(e.LookTarget != null ? e.LookTarget.position : Vector3.zero);
                _ = e.KeepUpright(Vector3.up);

                m_timeLeft -= Time.fixedDeltaTime;
                if (m_timeLeft <= 0f)
                {
                    e.SwitchState(new Plan());
                }
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
            }
        }

        public class Dodge : EnemyState
        {
            private static readonly float[] s_dodgeChancePerWave = { 0f, 0.25f, 0.3f, 0.5f, 0.75f, 0.8f, 0.9f, 1f };
            public const float X_DIST = 0.3f;
            private bool m_left;

            public static bool ShouldDodge()
            {
                return Random.value <= s_dodgeChancePerWave[Spawner.Instance.Wave];
            }

            public void EnterState(Enemy e, EnemyState lastState)
            {
                m_left = Random.value < 0.5f;
                e.TargetPos = e.Rigidbody.position;
                var rot = Quaternion.AngleAxis(e.Rigidbody.rotation.eulerAngles.y, Vector3.up);
                if (e.TargetPos.y >= 0.75f &&
                    Random.value <= 0.5f)
                {
                    e.TargetPos.y = Random.Range(0.45f, Mathf.Max(0.45f, 0.9f * e.TargetPos.y - 0.46f));
                    e.TargetPos += rot * (X_DIST * (m_left ? Vector3.left : Vector3.right));
                }
                else
                {
                    e.TargetPos += X_DIST * (m_left ? Vector3.up : Vector3.down);
                    e.TargetPos += rot * (Random.Range(2f * X_DIST, 3.75f * X_DIST) * (m_left ? Vector3.left : Vector3.right));
                }
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
                var closest = c.ClosestPointOnBounds(e.Rigidbody.position);
                var cLocal = closest - e.Rigidbody.position;
                var cDir = cLocal.normalized;
                e.MoveAlong(-cDir);

                var tLocal = e.TargetPos - e.Rigidbody.position;
                if (Vector3.Dot(tLocal, cDir) >= cLocal.magnitude - e.ProxSensor.Radius)
                {
                    // if we need to move into or past this collider to reach our target,
                    // reflect and dampen our target
                    cLocal = closest - e.TargetPos;
                    e.TargetPos += 1.3f * cLocal + e.ProxSensor.Radius * cLocal.normalized;
                }
            }

            public void UpdateState(Enemy e)
            {
                e.BarrelRoll(m_left);
                _ = e.AimTowards(e.LookTarget != null ? e.LookTarget.position : Vector3.zero);
                _ = e.FlyTo(e.TargetPos);

                if (e.Rigidbody.position.y - e.TargetPos.y <= 0.05f)
                {
                    e.TargetPos = e.Rigidbody.position;
                    e.SwitchState(new Plan());
                }
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
            }
        }

        public class Distribute : EnemyState
        {
            public void EnterState(Enemy e, EnemyState lastState)
            {
                if (e.TargetPlayer == null)
                {
                    var targetPlayer = Player.Player.GetRandomLivePlayer();
                    if (targetPlayer == null)
                    {
                        e.SwitchState(new ExitArena());
                        return;
                    }
                    else
                    {
                        e.TargetPlayer = targetPlayer;
                        e.LookTarget = targetPlayer.gameObject.transform;
                    }
                }

                var bodyFacing = Quaternion.AngleAxis(e.LookTarget.rotation.eulerAngles.y,
                                                             Vector3.up);
#if DISTRIBUTE_VISUALIZE
                // Visualize the distribution range
                Vector3 spr = new Vector3(e.DistributionSpread.x, e.DistributionSpread.y, 1f).normalized;
                Debug.DrawRay(e.LookTarget.position, bodyFacing * (spr + e.DistributionOffset).normalized, Color.cyan, 20f);
                spr.x = -spr.x;
                Debug.DrawRay(e.LookTarget.position, bodyFacing * (spr + e.DistributionOffset).normalized, Color.cyan, 20f);
                spr.y = -spr.y;
                Debug.DrawRay(e.LookTarget.position, bodyFacing * (spr + e.DistributionOffset).normalized, Color.cyan, 20f);
                spr.x = -spr.x;
                Debug.DrawRay(e.LookTarget.position, bodyFacing * (spr + e.DistributionOffset).normalized, Color.cyan, 20f);
#endif

                var enemyDir = bodyFacing * (WeaponUtils.RandomSpread(e.DistributionSpread) + e.DistributionOffset).normalized;
                var maxDist = 0f;
                var hits = Physics.RaycastAll(e.LookTarget.position,
                                                       enemyDir,
                                                       Mathf.Infinity,
                                                       LayerMask.GetMask("OVRScene"),
                                                       QueryTriggerInteraction.Ignore);
                Debug.DrawRay(e.LookTarget.position, enemyDir, hits.Length > 0f ? Color.green : Color.red, 20f);
                if (hits.Length <= 0)
                {
                    enemyDir = bodyFacing * Vector3.forward;
                    hits = Physics.RaycastAll(e.LookTarget.position,
                                              enemyDir,
                                              Mathf.Infinity,
                                              LayerMask.GetMask("OVRScene"),
                                              QueryTriggerInteraction.Ignore);
                    Debug.DrawRay(e.LookTarget.position, enemyDir, hits.Length > 0f ? Color.green : Color.red, 20f);
                }
                if (hits.Length > 0)
                {
                    var closestHit = hits[0];
                    for (var i = 1; i < hits.Length; ++i)
                    {
                        if (hits[i].distance < closestHit.distance)
                        {
                            closestHit = hits[i];
                        }
                    }

                    maxDist = closestHit.distance;
                }

                if (maxDist < 1f)
                {
                    e.SwitchState(new Relocate());
                    return;
                }

                enemyDir *= Random.Range(1f, Mathf.Max(1f, maxDist - 0.5f));
                e.TargetPos = e.LookTarget.position + enemyDir;
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
                if (c.attachedRigidbody == null)
                {
                    // for simplicity's sake, don't worry about trying to make it to the exact spot
                    // if there is static geometry in the way
                    e.TargetPos = e.Rigidbody.position;
                    e.SwitchState(new Plan());
                    return;
                }
                else if (c.bounds.Contains(e.TargetPos))
                {
                    // they're in our spot, so let's call this good enough
                    e.TargetPos = e.Rigidbody.position;
                    e.SwitchState(new Plan());
                    return;
                }

                if ((c.ClosestPointOnBounds(e.TargetPos) - e.TargetPos).magnitude < 0.51f)
                {
                    // the collider prevents us from getting much closer to the targetPos anyways
                    e.TargetPos = e.Rigidbody.position;
                    e.SwitchState(new Plan());
                    return;
                }

                var dir = new Vector3
                {
                    y = e.Rigidbody.position.y - c.ClosestPointOnBounds(e.Rigidbody.position).y
                };
                dir.y = dir.y == 0f ? -1f : dir.y;
                dir = dir.normalized;
                e.MoveAlong(dir);
            }

            public void UpdateState(Enemy e)
            {
                _ = e.AimTowards(e.LookTarget != null ? e.LookTarget.position : Vector3.zero);
                _ = e.KeepUpright(Vector3.up);
                if (e.FlyTo(e.TargetPos))
                {
                    e.SwitchState(new Plan());
                }
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
            }
        }

        public class Relocate : EnemyState
        {
            public void EnterState(Enemy e, EnemyState lastState)
            {
                e.TargetPos = new Vector3(Random.Range(Spawner.Instance.RoomMinExtent.x, Spawner.Instance.RoomMaxExtent.x),
                                          Random.Range(Spawner.Instance.RoomMinExtent.y, Spawner.Instance.RoomMaxExtent.y - 0.3f),
                                          Random.Range(Spawner.Instance.RoomMinExtent.z, Spawner.Instance.RoomMaxExtent.z));
                e.MaxVelocity *= Random.Range(0.2f, 0.3f);
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
                var closest = c.ClosestPointOnBounds(e.Rigidbody.position);
                var cLocal = closest - e.Rigidbody.position;
                var cDir = cLocal.normalized;
                e.MoveAlong(-cDir);

                var tLocal = e.TargetPos - e.Rigidbody.position;
                if (Vector3.Dot(tLocal, cDir) >= cLocal.magnitude - e.ProxSensor.Radius)
                {
                    // We need to move into or past this collider to reach our target.
                    // Instead, let's stop close to but not in proximity of this collider.
                    cLocal = closest - e.TargetPos;
                    e.TargetPos += cLocal - (e.ProxSensor.Radius + 0.01f) * cDir;
                }
            }

            public void UpdateState(Enemy e)
            {
                _ = e.AimTowards(e.LookTarget != null ? e.LookTarget.position : e.TargetPos);
                _ = e.KeepUpright(Vector3.up);
                if (e.FlyTo(e.TargetPos))
                {
                    e.SwitchState(new Plan());
                }
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
                e.ResetMovementSettings();
            }
        }

        public class Die : EnemyState
        {
            public void EnterState(Enemy e, EnemyState lastState)
            {
                e.DropSmallItems();
                e.DropLargeItem();

                var chance = Random.value;
                if (chance <= 0.75f)
                {
                    e.SwitchState(new DieFall());
                }
                else if (chance <= 0.95f)
                {
                    e.SwitchState(new DieMalfunction());
                }
                else
                {
                    e.SwitchState(new DieExplode());
                }
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
            }

            public void UpdateState(Enemy e)
            {
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
            }
        }

        public class DieExplode : EnemyState
        {
            public const float EXPLODE_HEALTH = -75f;

            public void EnterState(Enemy e, EnemyState lastState)
            {
                e.DestroySelf();
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
            }

            public void UpdateState(Enemy e)
            {
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
            }
        }

        public class DieFall : EnemyState
        {
            public void EnterState(Enemy e, EnemyState lastState)
            {
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
                e.DestroySelf();
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
            }

            public void UpdateState(Enemy e)
            {
                if (e.Health < DieExplode.EXPLODE_HEALTH)
                {
                    e.SwitchState(new DieExplode());
                    return;
                }
                else if (e.Rigidbody.position.y < 0f)
                {
                    e.DestroySelf();
                    return;
                }
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
            }
        }

        public class DieMalfunction : EnemyState
        {
            private bool m_clockwise;

            public void EnterState(Enemy e, EnemyState lastState)
            {
                m_clockwise = Random.value < 0.5f;

                e.MaxAngularVelocity *= 2f;
            }

            public void OnCollisionStay(Enemy e, Collision c)
            {
                e.DestroySelf();
            }

            public void OnProximityStay(Enemy e, Collider c)
            {
            }

            public void UpdateState(Enemy e)
            {
                if (e.Health < DieExplode.EXPLODE_HEALTH)
                {
                    e.SwitchState(new DieExplode());
                    return;
                }
                else if (e.Rigidbody.position.y < 0f)
                {
                    e.DestroySelf();
                    return;
                }

                e.BarrelRoll(m_clockwise);
                var dir = (2f * Vector3.down + e.transform.TransformDirection(Vector3.up)).normalized;
                _ = e.AimAlong(dir);
                e.MoveAlong(dir);
            }

            public void ExitState(Enemy e, EnemyState nextState)
            {
            }
        }
    }
}
