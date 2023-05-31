// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Text;
using Fusion;
using UnityEngine;

namespace Discover.Colocation
{
    public class PhotonPlayerIDDictionary : NetworkBehaviour
    {
        private bool m_hasNetworkSpawn;

        [Networked, Capacity(30)] private NetworkLinkedList<ulong> OculusIds { get; }
        [Networked, Capacity(30)] private NetworkLinkedList<int> NetworkIds { get; }
        [Networked, Capacity(30)] private NetworkLinkedList<Guid> HeadsetIds { get; }


        public override void Spawned()
        {
            m_hasNetworkSpawn = true;
            ColocationDriverNetObj.Instance.SetPlayerIdDictionary(this);
        }

        private bool CheckIfNetworkSpawnOccured()
        {
            if (!m_hasNetworkSpawn)
            {
                Debug.LogError("You are using something in NetworkDictionary before OnNetworkSpawn occured");
            }

            return m_hasNetworkSpawn;
        }

        public void Add(ulong key, int value, Guid headsetGuid)
        {
            if (!CheckIfNetworkSpawnOccured())
            {
                return;
            }

            OculusIds.Add(key);
            NetworkIds.Add(value);
            HeadsetIds.Add(headsetGuid);
        }

        public void Clear()
        {
            OculusIds.Clear();
            NetworkIds.Clear();
            HeadsetIds.Clear();
        }

        public ulong? GetOculusId(int networkId)
        {
            var ulongNetworkId = networkId;
            for (var i = 0; i < NetworkIds.Count; i++)
                if (ulongNetworkId == NetworkIds[i])
                {
                    return OculusIds[i];
                }

            return null;
        }

        public object GetNetworkId(ulong oculusId)
        {
            for (var i = 0; i < OculusIds.Count; i++)
                if (oculusId == OculusIds[i])
                {
                    return NetworkIds[i];
                }

            return null;
        }

        public object GetNetworkId(Guid headsetId)
        {
            for (var i = 0; i < HeadsetIds.Count; i++)
                if (headsetId == HeadsetIds[i])
                {
                    return NetworkIds[i];
                }

            return null;
        }

        public bool ContainsOculusId(object oculusId)
        {
            if (!CheckIfNetworkSpawnOccured())
            {
                return false;
            }

            var ulongoculusId = (ulong)oculusId;
            return OculusIds.Contains(ulongoculusId);
        }

        public bool ContainsNetworkId(object networkId)
        {
            if (!CheckIfNetworkSpawnOccured())
            {
                return false;
            }

            var ulongNetworkId = (int)networkId;

            return NetworkIds.Contains(ulongNetworkId);
        }

        public bool ContainsHeadsetId(Guid headsetId)
        {
            return CheckIfNetworkSpawnOccured() && HeadsetIds.Contains(headsetId);
        }

        public void RemoveUsingOculusId(object oculusId)
        {
            if (!CheckIfNetworkSpawnOccured())
            {
                return;
            }

            var ulongOculusId = (ulong)oculusId;

            for (var i = 0; i < OculusIds.Count; i++)
                if (ulongOculusId == OculusIds[i])
                {
                    var netId = NetworkIds[i];
                    var headsetId = HeadsetIds[i];
                    _ = OculusIds.Remove(ulongOculusId);
                    _ = NetworkIds.Remove(netId);
                    _ = HeadsetIds.Remove(headsetId);
                    return;
                }

            Debug.LogError($"NetworkDictionary: Unable to find oculusId to delete: {ulongOculusId}");
        }

        public void RemoveUsingNetworkId(object networkId)
        {
            if (!CheckIfNetworkSpawnOccured())
            {
                return;
            }

            var ulongNetworkId = (int)networkId;

            for (var i = 0; i < NetworkIds.Count; i++)
                if (ulongNetworkId == NetworkIds[i])
                {
                    var oculusId = OculusIds[i];
                    var headsetId = HeadsetIds[i];
                    _ = NetworkIds.Remove(ulongNetworkId);
                    _ = OculusIds.Remove(oculusId);
                    _ = HeadsetIds.Remove(headsetId);
                    return;
                }

            Debug.LogError($"NetworkDictionary: Unable to find networkId: {ulongNetworkId}");
        }

        public void RemoveUsingHeadsetId(Guid headsetId)
        {
            if (!CheckIfNetworkSpawnOccured())
            {
                return;
            }

            for (var i = 0; i < HeadsetIds.Count; i++)
                if (headsetId == HeadsetIds[i])
                {
                    var oculusId = OculusIds[i];
                    var netId = NetworkIds[i];
                    _ = NetworkIds.Remove(netId);
                    _ = OculusIds.Remove(oculusId);
                    _ = HeadsetIds.Remove(headsetId);
                    return;
                }

            Debug.LogError($"NetworkDictionary: Unable to find headsetId: {headsetId}");
        }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();
            for (var i = 0; i < OculusIds.Count; i++)
            {
                _ = stringBuilder.Append($"[{OculusIds[i]},{NetworkIds[i]},{HeadsetIds[i]}]");
                if (i < OculusIds.Count - 1)
                {
                    _ = stringBuilder.Append(",");
                }
            }

            return stringBuilder.ToString();
        }
    }
}