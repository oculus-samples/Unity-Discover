// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using ColocationPackage;
using Fusion;

namespace Discover.Colocation
{
    public struct PhotonNetPlayer : INetworkStruct, IEquatable<PhotonNetPlayer>
    {
        public ulong OculusId;
        public uint ColocationGroupId;


        public Player Player => new(OculusId, ColocationGroupId);

        public PhotonNetPlayer(Player player)
        {
            OculusId = player.oculusId;
            ColocationGroupId = player.colocationGroupId;
        }

        public PhotonNetPlayer(ulong oculusId, uint colocationGroupId)
        {
            OculusId = oculusId;
            ColocationGroupId = colocationGroupId;
        }

        public bool Equals(PhotonNetPlayer other)
        {
            return OculusId == other.OculusId && ColocationGroupId == other.ColocationGroupId;
        }
    }
}