// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using ColocationPackage;
using Fusion;
using UnityEngine;

namespace Discover.Colocation
{
    public class PhotonNetworkMessenger : NetworkBehaviour, INetworkMessenger
    {
        private readonly Dictionary<byte, Action<object>> m_callbackDictionary = new();
        private PhotonPlayerIDDictionary m_idDictionary;

        public override void Spawned()
        {
            NetworkAdapter.NetworkMessenger = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (ReferenceEquals(NetworkAdapter.NetworkMessenger, this))
            {
                NetworkAdapter.NetworkMessenger = null;
            }
        }

        public void Init(PhotonPlayerIDDictionary idDictionary)
        {
            m_idDictionary = idDictionary;
        }

        public void SendMessageUsingOculusId(byte eventCode, ulong oculusId, object messageData = null)
        {
            Debug.Log($"SendMessageUsingOculusId called: eventCode: {eventCode}, oculudId: {oculusId}");
            if (m_idDictionary == null)
            {
                Debug.LogError("NetcodeGameObjectsMessenger doesn't have a dictionary to go from oculus id to network id");
                return;
            }

            Debug.Log($"SendMessageUsingOculusId _idDictionary is {m_idDictionary}");
            var networkId = (int)m_idDictionary.GetNetworkId(oculusId);
            Debug.Log($"SendMessageUsingOculusId to player {networkId}");

            if (messageData != null)
            {
                var data = (ShareAndLocalizeParams)messageData;
                FindRPCToCallServerRPC(eventCode, networkId, data.oculusIdAnchorOwner, data.oculusIdAnchorRequester, data.headsetIdAnchorRequester, data.uuid.ToString(), data.anchorFlowSucceeded);
            }
            else
            {
                FindRPCToCallServerRPC(eventCode, networkId);
            }
        }

        public void SendMessageUsingHeadsetId(byte eventCode, Guid headsetId, object messageData = null)
        {
            Debug.Log($"SendMessageUsingHeadsetId called: eventCode: {eventCode}, headsetId: {headsetId}");

            Debug.Log($"SendMessageUsingHeadsetId _idDictionary is {m_idDictionary}");
            var networkId = (int)m_idDictionary.GetNetworkId(headsetId);
            Debug.Log($"SendMessageUsingHeadsetId to player {networkId}");

            if (messageData != null)
            {
                var data = (ShareAndLocalizeParams)messageData;
                FindRPCToCallServerRPC(
                    eventCode, networkId, data.oculusIdAnchorOwner, data.oculusIdAnchorRequester,
                    data.headsetIdAnchorRequester, data.uuid.ToString(),
                    data.anchorFlowSucceeded);
            }
            else
            {
                FindRPCToCallServerRPC(eventCode, networkId);
            }
        }

        public void SendMessageUsingNetworkId(byte eventCode, int networkId, object messageData = null)
        {
            Debug.Log($"SendMessageUsingNetworkId called: eventCode: {eventCode}, networkId: {networkId}");

            if (messageData != null)
            {
                var data = (ShareAndLocalizeParams)messageData;
                FindRPCToCallServerRPC(eventCode, networkId, data.oculusIdAnchorOwner, data.oculusIdAnchorRequester,
                    data.headsetIdAnchorRequester, data.uuid.ToString(), data.anchorFlowSucceeded);
            }
            else
            {
                FindRPCToCallServerRPC(eventCode, networkId);
            }
        }

        public void SendMessageToAll(byte eventCode, object messageData = null) => throw new NotImplementedException();

        public void RegisterEventCallback(byte eventCode, Action<object> callback)
        {
            m_callbackDictionary.Add(eventCode, callback);
        }

        public void UnregisterEventCallback(byte eventCode)
        {
            _ = m_callbackDictionary.Remove(eventCode);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void FindRPCToCallServerRPC(byte eventCode, int playerId, ulong oculusIdAnchorOwner,
            ulong oculusIdAnchorRequester, Guid headsetIdRequester, string uuid,
            NetworkBool anchorFlowSucceeded, RpcInfo info = default)
        {
            Debug.Log("FindRPCToCallServerRPC");
            PlayerRef playerRef = playerId;
            FindRPCToCallClientRPC(playerRef, eventCode, oculusIdAnchorOwner, oculusIdAnchorRequester, headsetIdRequester, uuid, anchorFlowSucceeded);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void FindRPCToCallServerRPC(byte eventCode, int playerId)
        {
            Debug.Log("FindRPCToCallServerRPC: Null");
            PlayerRef playerRef = playerId;
            FindRPCToCallClientRPC(playerRef, eventCode);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void FindRPCToCallClientRPC(
            [RpcTarget] PlayerRef player,
            byte eventCode,
            ulong oculusIdAnchorOwner, ulong oculusIdAnchorRequester, Guid headsetIdRequester,
            string uuid, NetworkBool anchorFlowSucceeded)
        {
            Debug.Log("FindRPCToCallClientRPC");
            var data = new ShareAndLocalizeParams(oculusIdAnchorOwner, oculusIdAnchorRequester, headsetIdRequester, uuid)
            {
                anchorFlowSucceeded = anchorFlowSucceeded
            };
            m_callbackDictionary[eventCode](data);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void FindRPCToCallClientRPC(
            [RpcTarget] PlayerRef player,
            byte eventCode
        )
        {
            Debug.Log("FindRPCToCallClientRPC: null");
            m_callbackDictionary[eventCode](null);
        }
    }
}