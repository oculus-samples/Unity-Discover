// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Text;
using com.meta.xr.colocation;
using Fusion;

namespace Discover.Colocation
{
    /// <summary>
    ///     Provides a mean to send network messages among all connected users.
    ///     It holds the dictionary of all connected users, who can be included into the messaging system.
    /// </summary>
    public class PhotonNetworkMessenger : NetworkBehaviour, INetworkMessenger
    {
        [Networked, Capacity(10)]
        private NetworkLinkedList<int> NetworkIds { get; }

        [Networked, Capacity(10)]
        private NetworkLinkedList<ulong> PlayerIds { get; }

        public event Action<ShareAndLocalizeParams> AnchorShareRequestReceived;
        public event Action<ShareAndLocalizeParams> AnchorShareRequestCompleted;

        private enum MessageEvent
        {
            ANCHOR_SHARE_REQUEST,
            ANCHOR_SHARE_COMPLETE,
        }

        public void RegisterLocalPlayer(ulong localPlayerId)
        {
            Logger.Log($"{nameof(PhotonNetworkMessenger)}: RegisterLocalPlayer: localPlayerId {localPlayerId}",
                LogLevel.Verbose);
            Logger.Log($"{nameof(PhotonNetworkMessenger)} RegisterLocalPlayer: fusionId {Runner.LocalPlayer.PlayerId}",
                LogLevel.Verbose);
            AddPlayerIdHostRPC(localPlayerId, Runner.LocalPlayer.PlayerId);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void AddPlayerIdHostRPC(ulong localPlayerId, int localNetworkId)
        {
            Logger.Log("Add Player Id Host RPC: player id", LogLevel.Verbose);
            PlayerIds.Add(localPlayerId);
            Logger.Log("Add Player Id Host RPC: network id", LogLevel.Verbose);
            NetworkIds.Add(localNetworkId);

            PrintIDDictionary();
        }

        private bool TryGetNetworkId(ulong playerId, out int networkId)
        {
            for (var i = 0; i < PlayerIds.Count; i++)
            {
                if (playerId == PlayerIds[i])
                {
                    networkId = NetworkIds[i];
                    return true;
                }
            }

            networkId = 0;
            Logger.Log($"PhotonNetworkMessenger: playerId {playerId} got invalid networkId {networkId}", LogLevel.Error);
            return false;
        }

        public void SendAnchorShareRequest(ulong targetPlayerId, ShareAndLocalizeParams shareAndLocalizeParams)
        {
            Logger.Log(
                $"{nameof(PhotonNetworkMessenger)}: Sending anchor share request to player {targetPlayerId}. (anchorID {shareAndLocalizeParams.anchorUUID})",
                LogLevel.Verbose);
            var fusionData = new PhotonShareAndLocalizeParams(shareAndLocalizeParams);
            SendMessageToPlayer(MessageEvent.ANCHOR_SHARE_REQUEST, targetPlayerId, fusionData);
        }

        public void SendAnchorShareCompleted(ulong targetPlayerId, ShareAndLocalizeParams shareAndLocalizeParams)
        {
            Logger.Log(
                $"{nameof(PhotonNetworkMessenger)}: Sending anchor share completed to player {targetPlayerId}. (anchorID {shareAndLocalizeParams.anchorUUID})",
                LogLevel.Verbose);
            var fusionData = new PhotonShareAndLocalizeParams(shareAndLocalizeParams);
            SendMessageToPlayer(MessageEvent.ANCHOR_SHARE_COMPLETE, targetPlayerId, fusionData);
        }

        private void SendMessageToPlayer(MessageEvent eventCode, ulong playerId,
            PhotonShareAndLocalizeParams fusionData)
        {
            Logger.Log($"Calling SendMessageToPlayer with MessageEvent: {eventCode}, to playerId {playerId}",
                LogLevel.Verbose);
            if (TryGetNetworkId(playerId, out var fusionId))
            {
                Logger.Log($"Calling FindRPCToCallServerRPC playerId {playerId} maps to fusionId {fusionId}",
                    LogLevel.Verbose);
                FindRPCToCallServerRPC(eventCode, fusionId, fusionData);
            }
            else
            {
                Logger.Log($"Could not find fusionId for playerId {playerId}", LogLevel.Error);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void FindRPCToCallServerRPC(MessageEvent eventCode, int fusionId,
            PhotonShareAndLocalizeParams fusionData, RpcInfo info = default)
        {
            Logger.Log("FindRPCToCallServerRPC called", LogLevel.Verbose);
            PlayerRef fusionPlayerRef = fusionId;
            Logger.Log("Created PlayerRef right before calling HandleMessageClientRPC", LogLevel.Verbose);
            HandleMessageClientRPC(fusionPlayerRef, eventCode, fusionData);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void HandleMessageClientRPC([RpcTarget] PlayerRef playerRef, MessageEvent eventCode,
            PhotonShareAndLocalizeParams fusionData)
        {
            Logger.Log($"HandleMessageClientRPC: {eventCode}", LogLevel.Verbose);
            switch (eventCode)
            {
                case MessageEvent.ANCHOR_SHARE_REQUEST:
                    AnchorShareRequestReceived?.Invoke(fusionData.GetShareAndLocalizeParams());
                    break;
                case MessageEvent.ANCHOR_SHARE_COMPLETE:
                    AnchorShareRequestCompleted?.Invoke(fusionData.GetShareAndLocalizeParams());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventCode), eventCode, null);
            }
        }

        private void PrintIDDictionary()
        {
            var stringBuilder = new StringBuilder();
            for (var i = 0; i < PlayerIds.Count; i++)
            {
                _ = stringBuilder.Append($"[{PlayerIds[i]},{NetworkIds[i]}]");
                if (i < PlayerIds.Count - 1)
                {
                    _ = stringBuilder.Append(",");
                }
            }

            Logger.Log($"{nameof(PhotonNetworkMessenger)}: ID dictionary is {stringBuilder}", LogLevel.Verbose);
        }
    }
}