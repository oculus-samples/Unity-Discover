// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using ColocationPackage;
using Discover.Utilities;
using Fusion;
using Unity.Collections;
using UnityEngine;

namespace Discover.Colocation
{
    public class PhotonNetworkData : NetworkSingleton<PhotonNetworkData>, INetworkData
    {
        [Networked] private uint ColocationGroupCount { get; set; }

        [Networked, Capacity(10)]
        private NetworkLinkedList<PhotonNetAnchor> AnchorList { get; }

        [Networked, Capacity(10)] private NetworkLinkedList<PhotonNetPlayer> PlayerList { get; }

        public override void Spawned()
        {
            NetworkAdapter.NetworkData = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (ReferenceEquals(NetworkAdapter.NetworkData, this))
            {
                NetworkAdapter.NetworkData = null;
            }
        }

        public void AddPlayer(Player player)
        {
            AddNetPlayer(new PhotonNetPlayer(player));
        }

        public void RemovePlayer(Player player)
        {
            RemoveNetPlayer(new PhotonNetPlayer(player));
        }

        public Player? GetPlayer(ulong oculusId)
        {
            foreach (var photonPlayer in PlayerList)
                if (photonPlayer.OculusId == oculusId)
                {
                    return photonPlayer.Player;
                }

            return null;
        }

        public List<Player> GetAllPlayers()
        {
            var allPlayers = new List<Player>();
            foreach (var photonPlayer in PlayerList) allPlayers.Add(photonPlayer.Player);

            return allPlayers;
        }

        public Player? GetFirstPlayerInColocationGroup(uint colocationGroup)
        {
            foreach (var photonPlayer in PlayerList)
            {
                if (photonPlayer.ColocationGroupId == colocationGroup)
                {
                    return photonPlayer.Player;
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

        public Anchor? GetAnchor(FixedString64Bytes uuid)
        {
            foreach (var photonAnchor in AnchorList)
                if (photonAnchor.Anchor.uuid.Equals(uuid))
                {
                    return photonAnchor.Anchor;
                }

            return null;
        }

        public List<Anchor> GetAllAnchors()
        {
            var anchors = new List<Anchor>();
            foreach (var photonAnchor in AnchorList) anchors.Add(photonAnchor.Anchor);

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