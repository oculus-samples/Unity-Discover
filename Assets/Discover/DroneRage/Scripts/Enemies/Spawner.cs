// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Linq;
using Discover.DroneRage.Game;
using Discover.DroneRage.Scene;
using Discover.Networking;
using Meta.Utilities;
using Meta.XR.MRUtilityKit;
using UnityEngine;
using static Discover.DroneRage.Bootstrapper.DroneRageAppContainerUtils;
using Random = UnityEngine.Random;

namespace Discover.DroneRage.Enemies
{
    [DefaultExecutionOrder(100)]
    public class Spawner : Singleton<Spawner>
    {
        public enum GameMode
        {
            SHORT,
            NORMAL,
            ENDLESS,
            NUM_GAME_MODES
        }

        public static readonly int[] ShortGameWaveSkip = { 1, 0, 1, 0, 0, 1, 0, 0 };
        public static readonly int[] DronesPerWave = { 1, 8, 6, 21, 21, 28, 20, 0 };
        public static readonly int[] MaxLiveDronesPerWave = { 1, 2, 2, 3, 3, 4, 4, 0 };
        public static readonly double[] DroneSpawnTimePerWave = { 4f, 2f, 1.5f, 1.4f, 1.3f, 1.25f, 1.21f, 999999f };
        public const float WAVE_COMPLETE_TIME = 4.5f;

        public event Action OnWaveAdvance;

        public GameObject DronePrefab;

        [SerializeField] public float EntranceYOffset = 0.65f;

        [SerializeField] private GameMode m_gameMode = GameMode.SHORT;

        public int Wave { get; private set; } = 0;
        private int m_dronesSpawned = 0;
        private int m_liveDrones = 0;
        private double m_timeUntilSpawn = 0f;

        private Vector3 m_roomMinExtent = Vector3.zero;
        public Vector3 RoomMinExtent => m_roomMinExtent;
        private Vector3 m_roomMaxExtent = new(1.8f, 3f, 1.8f);
        public Vector3 RoomMaxExtent => m_roomMaxExtent;
        private Vector3 m_roomSize = new(1.8f, 3f, 1.8f);
        public Vector3 RoomSize => m_roomSize;

        public void LOGDroneKill()
        {
            --m_liveDrones;

            if (m_liveDrones <= 0 &&
                m_dronesSpawned >= DronesPerWave[Wave])
            {
                AdvanceWave();
            }
        }

        public Vector3 GetClosestSpawnPoint(Vector3 position)
        {
            var spawnOffset = position - 0.5f * (m_roomMinExtent + m_roomMaxExtent);
            spawnOffset.y = 0f;
            spawnOffset = spawnOffset.normalized;

            if (spawnOffset.sqrMagnitude == 0f)
            {
                spawnOffset = Vector3.forward;
            }

            return 2f * (RoomSize.magnitude + 1f) * spawnOffset + new Vector3(0f, m_roomMaxExtent.y - 1f, 0f);
        }

        public Vector3 GetRandomSpawnPoint(Player.Player targetPlayer)
        {
            var spawnOffset = targetPlayer.transform.forward;
            spawnOffset.y = 0f;
            spawnOffset = spawnOffset.normalized;

            if (spawnOffset.sqrMagnitude == 0f)
            {
                spawnOffset = Vector3.forward;
            }

            // roll twice and prefer the more forward direction
            var r1 = Random.Range(-180f, 180f);
            var r2 = Random.Range(-180f, 180f);
            r1 = Mathf.Abs(r1) <= Mathf.Abs(r2) ? r1 : r2;
            spawnOffset = Quaternion.AngleAxis(r1, Vector3.up) * spawnOffset;
            return 2f * (RoomSize.magnitude + 1f) * spawnOffset + new Vector3(0f, m_roomMaxExtent.y - 1f, 0f);
        }

        public Vector3 GetClosestRoomEntrance(Transform insideRoom, Vector3 position)
        {
            Vector3 roomEntrance;
            var dir = position - 0.5f * (m_roomMinExtent + m_roomMaxExtent);
            dir.y = 0f;
            dir = dir.normalized;

            if (dir.sqrMagnitude == 0f)
            {
                dir = Vector3.forward;
            }

            var hits = Physics.RaycastAll(insideRoom.position,
                dir,
                100f,
                LayerMask.GetMask("OVRScene"),
                QueryTriggerInteraction.Ignore);
            var hitWall = false;
            var closestHit = new RaycastHit { distance = 100f };
            foreach (var hit in hits)
            {
                if (hit.distance < closestHit.distance &&
                    hit.transform.TryGetComponent<Wall>(out _))
                {
                    hitWall = true;
                    closestHit = hit;
                }
            }

            roomEntrance = hitWall
                ? (closestHit.distance - 0.4f) * dir + new Vector3(insideRoom.position.x,
                    m_roomMaxExtent.y + EntranceYOffset,
                    insideRoom.position.z)
                : (new(0f, m_roomMaxExtent.y + EntranceYOffset, 0f));

            return roomEntrance;
        }

        public Vector3 GetRandomRoomEntrance(Player.Player targetPlayer)
        {
            var spawnOffset = targetPlayer.transform.forward;
            spawnOffset.y = 0f;
            spawnOffset = spawnOffset.normalized;

            if (spawnOffset.sqrMagnitude == 0f)
            {
                spawnOffset = Vector3.forward;
            }

            // roll twice and prefer the more forward direction
            var r1 = Random.Range(-180f, 180f);
            var r2 = Random.Range(-180f, 180f);
            r1 = Mathf.Abs(r1) <= Mathf.Abs(r2) ? r1 : r2;
            spawnOffset = Quaternion.AngleAxis(r1, Vector3.up) * spawnOffset;
            Vector3 roomEntrance;

            var hits = Physics.RaycastAll(targetPlayer.transform.position,
                spawnOffset,
                100f,
                LayerMask.GetMask("OVRScene"),
                QueryTriggerInteraction.Ignore);
            var hitWall = false;
            var closestHit = new RaycastHit { distance = 100f };
            foreach (var hit in hits)
            {
                if (hit.distance < closestHit.distance &&
                    hit.transform.TryGetComponent<Wall>(out _))
                {
                    hitWall = true;
                    closestHit = hit;
                }
            }

            roomEntrance = hitWall
                ? (closestHit.distance - 0.4f) * spawnOffset + new Vector3(targetPlayer.transform.position.x,
                    m_roomMaxExtent.y + EntranceYOffset,
                    targetPlayer.transform.position.z)
                : (new(0f, m_roomMaxExtent.y + EntranceYOffset, 0f));

            return roomEntrance;
        }

        public void AdvanceWave()
        {
            if (DroneRageGameController.Instance != null &&
                DroneRageGameController.Instance.GameOverState.GameOver)
            {
                return;
            }

            m_timeUntilSpawn = WAVE_COMPLETE_TIME;
            if (m_gameMode == GameMode.SHORT)
            {
                Wave += ShortGameWaveSkip[Wave];
            }

            Wave = Mathf.Min(Wave + 1, m_gameMode == GameMode.ENDLESS ? DronesPerWave.Length - 2 : DronesPerWave.Length - 1);
            m_dronesSpawned = 0;
            m_liveDrones = 0;

            if (DronesPerWave[Wave] <= 0)
            {
                DroneRageGameController.Instance.TriggerGameOver(true);
            }

            OnWaveAdvance?.Invoke();
        }

        private bool Spawn()
        {
            if (DroneRageGameController.Instance.GameOverState.GameOver)
            {
                return false;
            }
            else if (m_liveDrones >= MaxLiveDronesPerWave[Wave])
            {
                return false;
            }
            else if (m_dronesSpawned >= DronesPerWave[Wave])
            {
                return false;
            }

            var spawnOffset = GetRandomSpawnPoint(Player.Player.GetRandomLivePlayer());
            _ = GetAppContainer().NetInstantiate(DronePrefab,
                spawnOffset,
                Quaternion.identity);
            ++m_liveDrones;
            ++m_dronesSpawned;
            // scale speed at which the drones spawn based on the number of players
            m_timeUntilSpawn = DroneSpawnTimePerWave[Wave] / (0.22f * (Player.Player.NumPlayers + 3f));
            return true;
        }

        private void CalculateRoomExtents()
        {
            var anchors = SceneElementsManager.Instance.GetElementsByLabel(MRUKAnchor.SceneLabels.WALL_FACE).ToList();
            if (anchors.Any())
            {
                m_roomMinExtent = anchors[0].transform.position;
                m_roomMaxExtent = anchors[0].transform.position;
                m_roomSize = Vector3.zero;
            }

            foreach (var anchor in anchors)
            {
                var anchorTransform = anchor.transform;
                var position = anchorTransform.position;
                var scale = anchorTransform.lossyScale;
                var right = anchorTransform.right * scale.x * 0.5f;
                var up = anchorTransform.up * scale.y * 0.5f;
                var wallPoints = new Vector3[]
                {
                    position - right - up,
                    position - right + up,
                    position + right - up,
                    position + right + up,
                };

                foreach (var wp in wallPoints)
                {
                    Debug.Log($"wall point {wp}");

                    m_roomMinExtent = Vector3.Min(m_roomMinExtent, wp);
                    m_roomMaxExtent = Vector3.Max(m_roomMaxExtent, wp);

                    m_roomSize.y = Mathf.Max(m_roomSize.y, transform.lossyScale.y);
                }
            }

            m_roomSize = m_roomMaxExtent - m_roomMinExtent;
            Debug.Log("Room Size: " + m_roomSize + " Room Min Extents: " + m_roomMinExtent + " Room Max Extents: " + m_roomMaxExtent);
        }

        private void Start()
        {
            if (!PhotonNetwork.Runner.IsMasterClient())
            {
                Destroy(gameObject);
                return;
            }

            CalculateRoomExtents();
        }

        private void FixedUpdate()
        {
            if (Player.Player.PlayersLeft > 0 && m_timeUntilSpawn <= 0f)
            {
                _ = Spawn();
            }
            else
            {
                m_timeUntilSpawn -= Time.fixedDeltaTime;
            }
        }

        private void OnDrawGizmosSelected()
        {
            var bounds = new Bounds(RoomMinExtent, Vector3.zero);
            bounds.Encapsulate(RoomMaxExtent + Vector3.down * 0.3f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}