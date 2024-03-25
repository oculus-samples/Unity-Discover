// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using com.meta.xr.colocation;
using Fusion;

namespace Discover.Colocation
{
    /// <summary>
    ///     Represents a connected user, identified by Oculus ID and Colocation group ID.
    /// </summary>
    public struct PhotonNetPlayer : INetworkStruct, IEquatable<PhotonNetPlayer>
    {
        public ulong PlayerId;
        public ulong OculusId;
        public uint ColocationGroupId;

        public PhotonNetPlayer(com.meta.xr.colocation.Player player)
        {
            PlayerId = player.playerId;
            OculusId = player.oculusId;
            ColocationGroupId = player.colocationGroupId;
        }

        public Player GetPlayer()
        {
            return new Player(PlayerId, OculusId, ColocationGroupId);
        }

        public bool Equals(PhotonNetPlayer other)
        {
            return GetPlayer().Equals(other.GetPlayer());
        }
    }
}