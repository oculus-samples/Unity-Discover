// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Diagnostics;
using System.Linq;
using Fusion;
using Meta.Utilities;
using Meta.Utilities.Avatars;
using Meta.XR.Samples;
using UnityEngine;
using static Oculus.Avatar2.OvrAvatarEntity;

namespace Discover.Networking
{
    [MetaCodeSample("Discover")]
    public class PhotonFusionAvatarNetworking : NetworkBehaviour, IAvatarNetworking
    {
        [SerializeField, AutoSet] private AvatarEntity m_entity;

        [SerializeField] private EnumDictionary<StreamLOD, NullableFloat> m_updateFrequencySecondsByLod;
        [SerializeField] private float m_streamDelayMultiplier = 0.5f;

        [Networked(OnChanged = nameof(OnUserIdChanged))]
        public ulong UserId { get; private set; }

        private byte[] m_avatarDataBuffer = new byte[2048];
        private Stopwatch m_streamDelayWatch = new();
        private float m_currentStreamDelay;

        public override void Spawned()
        {
            base.Spawned();
            m_entity.Initialize();
        }

        private IEnumerator UpdateDataStream()
        {
            var lastUpdateTime = new EnumDictionary<StreamLOD, double>();
            while (isActiveAndEnabled && m_entity.IsLocal)
            {
                if (m_entity.IsCreated && m_entity.HasJoints && Object.IsValid)
                {
                    var now = Time.unscaledTimeAsDouble;
                    var (lod, timeSinceLastUpdate) = lastUpdateTime.Select(pair => (pair.Key, now - pair.Value)).
                        Where(pair =>
                            m_updateFrequencySecondsByLod[pair.Key].Value is { } frequency && pair.Item2 > frequency).
                        OrderByDescending(pair => pair.Item2).
                        FirstOrDefault();
                    if (timeSinceLastUpdate != default)
                    {
                        // act like every lower frequency lod got updated too
                        var lodFrequency = m_updateFrequencySecondsByLod[lod].Value;
                        foreach (var (key, _) in m_updateFrequencySecondsByLod.Where(pair =>
                                     pair.Value.Value <= lodFrequency))
                            lastUpdateTime[key] = now;

                        SendAvatarData(lod);
                    }
                }

                yield return null;
            }
        }

        private void SendAvatarData(StreamLOD lod)
        {
            var numBytes = m_entity.RecordStreamData_AutoBuffer(lod, ref m_avatarDataBuffer);
            RPC_SetStreamData(m_avatarDataBuffer, numBytes);
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Unreliable)]
        private void RPC_SetStreamData(byte[] data, uint numBytes)
        {
            if (m_entity == null || m_entity.IsLocal)
            {
                return;
            }

            _ = m_entity.ApplyStreamData(data[..(int)numBytes]);

            var latency = (float)m_streamDelayWatch.Elapsed.TotalSeconds;
            var delay = Mathf.Clamp01(latency * m_streamDelayMultiplier);
            m_currentStreamDelay = Mathf.LerpUnclamped(m_currentStreamDelay, delay, PLAYBACK_SMOOTH_FACTOR);
            m_entity.SetPlaybackTimeDelay(m_currentStreamDelay);
            m_streamDelayWatch.Restart();
        }

        private const float PLAYBACK_SMOOTH_FACTOR = 0.1f;

        public static void OnUserIdChanged(Changed<PhotonFusionAvatarNetworking> changed)
        {
            changed.Behaviour.OnUserIdChanged(changed.Behaviour.UserId);
        }

        private void OnUserIdChanged(ulong id)
        {
            if (id != 0 && m_entity.IsCreated)
                m_entity.LoadUser(id);
        }

        void IAvatarNetworking.Initialize()
        {
            if (m_entity.IsLocal)
            {
                _ = StartCoroutine(UpdateDataStream());
            }
            else
            {
                OnUserIdChanged(UserId);
            }
        }

        void IAvatarNetworking.OnUserIdSet(ulong id)
        {
            UserId = id;
        }

        bool IAvatarNetworking.IsNetworked => Object?.Runner?.IsRunning is true;
        bool IAvatarNetworking.IsOwner => Object?.HasStateAuthority is true;
    }
}