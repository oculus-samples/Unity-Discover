// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using com.meta.xr.colocation;
using Discover.Utilities;
using Fusion;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover.Colocation
{
    /// <summary>
    ///     Holds the count for all colocation groups, the anchors list, and players list.
    /// </summary>
    [MetaCodeSample("Discover")]
    public class PhotonNetworkData : NetworkSingleton<PhotonNetworkData>, INetworkData
    {
        [Networked] private uint ColocationGroupCount { get; set; }

        [Networked, Capacity(10)]
        private NetworkLinkedList<PhotonNetAnchor> AnchorList { get; }

        [Networked, Capacity(10)] private NetworkLinkedList<PhotonNetPlayer> PlayerList { get; }

        public void AddPlayer(Player player)
        {
            AddNetPlayer(new PhotonNetPlayer(player));
        }

        public void RemovePlayer(Player player)
        {
            RemoveNetPlayer(new PhotonNetPlayer(player));
        }
        
        public Player? GetPlayerWithPlayerId(ulong playerId)
        {
            foreach (var fusionPlayer in PlayerList)
            {
                if (fusionPlayer.GetPlayer().playerId == playerId)
                {
                    return fusionPlayer.GetPlayer();
                }
            }

            return null;
        }

        public Player? GetPlayerWithOculusId(ulong oculusId)
        {
            foreach (var fusionPlayer in PlayerList)
            {
                if (fusionPlayer.GetPlayer().oculusId == oculusId)
                {
                    return fusionPlayer.GetPlayer();
                }
            }

            return null;
        }

        public List<Player> GetAllPlayers()
        {
            var allPlayers = new List<Player>();
            foreach (var photonPlayer in PlayerList) allPlayers.Add(photonPlayer.GetPlayer());

            return allPlayers;
        }

        public Player? GetFirstPlayerInColocationGroup(uint colocationGroup)
        {
            foreach (var photonPlayer in PlayerList)
            {
                if (photonPlayer.ColocationGroupId == colocationGroup)
                {
                    return photonPlayer.GetPlayer();
                }
            }

            return null;
        }

        public void AddAnchor(Anchor anchor)
        {
            AnchorList.Add(new PhotonNetAnchor(anchor));
        }

        public void RemoveAnchor(Anchor anchor)
        {
            _ = AnchorList.Remove(new PhotonNetAnchor(anchor));
        }

        public Anchor? GetAnchor(ulong ownerOculusId)
        {
            foreach (var photonAnchor in AnchorList)
                if (photonAnchor.GetAnchor().ownerOculusId.Equals(ownerOculusId))
                {
                    return photonAnchor.GetAnchor();
                }

            return null;
        }

        public List<Anchor> GetAllAnchors()
        {
            var anchors = new List<Anchor>();
            foreach (var photonAnchor in AnchorList)
            {
                anchors.Add(photonAnchor.GetAnchor());
            }

            return anchors;
        }

        public uint GetColocationGroupCount()
        {
            Debug.Log($"GetColocationGroupCount: {ColocationGroupCount}");
            return ColocationGroupCount;
        }

        public void IncrementColocationGroupCount()
        {
            if (HasStateAuthority)
            {
                ColocationGroupCount++;
            }
            else
            {
                IncrementColocationGroupCountRpc();
            }
        }

        private void AddNetPlayer(PhotonNetPlayer player)
        {
            if (HasStateAuthority)
            {
                PlayerList.Add(player);
            }
            else
            {
                AddPlayerRpc(player);
            }
        }

        private void RemoveNetPlayer(PhotonNetPlayer player)
        {
            if (HasStateAuthority)
            {
                _ = PlayerList.Remove(player);
            }
            else
            {
                RemovePlayerRpc(player);
            }
        }

        private void AddNetAnchor(PhotonNetAnchor anchor)
        {
            if (HasStateAuthority)
            {
                AnchorList.Add(anchor);
            }
            else
            {
                AddAnchorRpc(anchor);
            }
        }

        private void RemoveNetAnchor(PhotonNetAnchor anchor)
        {
            if (HasStateAuthority)
            {
                _ = AnchorList.Remove(anchor);
            }
            else
            {
                RemoveAnchorRpc(anchor);
            }
        }

        #region Rpcs

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void AddPlayerRpc(PhotonNetPlayer player)
        {
            AddNetPlayer(player);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RemovePlayerRpc(PhotonNetPlayer player)
        {
            RemoveNetPlayer(player);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void AddAnchorRpc(PhotonNetAnchor anchor)
        {
            AddNetAnchor(anchor);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RemoveAnchorRpc(PhotonNetAnchor anchor)
        {
            RemoveNetAnchor(anchor);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void IncrementColocationGroupCountRpc()
        {
            IncrementColocationGroupCount();
        }

        #endregion
    }
}