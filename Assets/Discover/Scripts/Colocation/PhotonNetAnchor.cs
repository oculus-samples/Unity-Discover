// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using com.meta.xr.colocation;
using Fusion;
using Meta.XR.Samples;

namespace Discover.Colocation
{
    /// <summary>
    ///     Represents a replicated spatial anchor via Photon network.
    /// </summary>
    [MetaCodeSample("Discover")]
    public struct PhotonNetAnchor : INetworkStruct, IEquatable<PhotonNetAnchor>
    {
        public NetworkBool IsAutomaticAnchor;
        public NetworkBool IsAlignmentAnchor;
        public ulong OwnerOculusId;
        public uint ColocationGroupId;
        public NetworkString<_64> AutomaticAnchorUuid;

        public PhotonNetAnchor(Anchor anchor)
        {
            IsAutomaticAnchor = anchor.isAutomaticAnchor;
            IsAlignmentAnchor = anchor.isAlignmentAnchor;
            OwnerOculusId = anchor.ownerOculusId;
            ColocationGroupId = anchor.colocationGroupId;
            AutomaticAnchorUuid = anchor.automaticAnchorUuid.ToString();
        }

        public Anchor GetAnchor()
        {
            return new Anchor(IsAutomaticAnchor, IsAlignmentAnchor, OwnerOculusId, ColocationGroupId,
                AutomaticAnchorUuid.ToString());
        }

        public bool Equals(PhotonNetAnchor other)
        {
            return GetAnchor().Equals(other.GetAnchor());
        }
    }
}