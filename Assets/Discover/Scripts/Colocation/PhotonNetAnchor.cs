// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using ColocationPackage;
using Fusion;

namespace Discover.Colocation
{
    public struct PhotonNetAnchor : INetworkStruct, IEquatable<PhotonNetAnchor>
    {
        public NetworkBool IsAlignmentAnchor;
        public NetworkString<_64> Uuid;
        public ulong OwnerOculusId;
        public uint ColocationGroupId;

        public Anchor Anchor => new(IsAlignmentAnchor, Uuid.ToString(), OwnerOculusId, ColocationGroupId);

        public PhotonNetAnchor(Anchor anchor)
        {
            IsAlignmentAnchor = anchor.isAlignmentAnchor;
            Uuid = anchor.uuid.ToString();
            OwnerOculusId = anchor.ownerOculusId;
            ColocationGroupId = anchor.colocationGroupId;
        }

        public PhotonNetAnchor(NetworkBool isAlignmentAnchor, NetworkString<_64> uuid, ulong ownerOculusId, uint colocationGroupId)
        {
            IsAlignmentAnchor = isAlignmentAnchor;
            Uuid = uuid;
            OwnerOculusId = ownerOculusId;
            ColocationGroupId = colocationGroupId;
        }

        public bool Equals(PhotonNetAnchor other)
        {
            return IsAlignmentAnchor == other.IsAlignmentAnchor
                   && Uuid == other.Uuid
                   && OwnerOculusId == other.OwnerOculusId
                   && ColocationGroupId == other.ColocationGroupId;
        }

    }
}