// Copyright (c) Meta Platforms, Inc. and affiliates.

using com.meta.xr.colocation;
using Fusion;

namespace Discover.Colocation
{
    /// <summary>
    ///     A Photon Fusion wrapper for ShareAndLocalizeParams
    ///     Used to be able to serialize and send the ShareAndLocalizeParams data over the network
    /// </summary>
    public struct PhotonShareAndLocalizeParams : INetworkStruct
    {
        public ulong RequestingPlayerId;
        public ulong RequestingPlayerOculusId;
        public NetworkString<_64> AnchorUuid;
        public NetworkBool AnchorFlowSucceeded;

        public PhotonShareAndLocalizeParams(ShareAndLocalizeParams data)
        {
            RequestingPlayerId = data.requestingPlayerId;
            RequestingPlayerOculusId = data.requestingPlayerOculusId;
            AnchorUuid = data.anchorUUID.ToString();
            AnchorFlowSucceeded = data.anchorFlowSucceeded;
        }

        public ShareAndLocalizeParams GetShareAndLocalizeParams()
        {
            return new ShareAndLocalizeParams(
                RequestingPlayerId, RequestingPlayerOculusId, AnchorUuid.ToString(), AnchorFlowSucceeded);
        }
    }
}