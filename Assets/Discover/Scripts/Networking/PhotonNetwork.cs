// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Linq;
using Fusion;
using Meta.XR.Samples;

namespace Discover.Networking
{
    [MetaCodeSample("Discover")]
    public static class PhotonNetwork
    {
        private static NetworkRunner s_runner;

        public static NetworkRunner Runner =>
            s_runner != null ? s_runner : (s_runner = NetworkRunner.Instances.FirstOrDefault());

        public static bool IsMasterClient(this NetworkRunner r) =>
            r != null && (r.IsSharedModeMasterClient || r.GameMode is GameMode.Single);

        public static OVRCameraRig CameraRig => AppInteractionController.Instance != null ? AppInteractionController.Instance.CameraRig : null;

        public static void Despawn(this NetworkObject obj)
        {
            Runner.Despawn(obj);
        }
    }
}