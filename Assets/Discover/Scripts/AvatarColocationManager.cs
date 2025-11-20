// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Meta.XR.Samples;

namespace Discover
{
    [MetaCodeSample("Discover")]
    public class AvatarColocationManager
    {
        private static AvatarColocationManager s_instance;

        public static AvatarColocationManager Instance
        {
            get
            {
                s_instance ??= new AvatarColocationManager();
                return s_instance;
            }
        }

        public bool IsCurrentPlayerRemote { get; set; }

        public DiscoverPlayer LocalPlayer { get; set; }

        public Action OnLocalPlayerColocationGroupUpdated;

        public bool CanPlaceOrMoveIcons =>
            !IsCurrentPlayerRemote && LocalPlayer != null && LocalPlayer.IsPlayerColocated;

        private AvatarColocationManager()
        {
        }
    }
}