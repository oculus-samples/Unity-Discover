// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Linq;
using Fusion;

namespace Discover.Networking
{
    public static class PhotonNetwork
    {
        public static NetworkRunner Runner => NetworkRunner.Instances.FirstOrDefault();

        public static bool IsMasterClient => Runner?.IsSharedModeMasterClient is true;

        public static OVRCameraRig CameraRig => AppInteractionController.Instance != null ? AppInteractionController.Instance.CameraRig : null;

        public static void Despawn(this NetworkObject obj)
        {
            Runner.Despawn(obj);
        }
    }
}
