// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Interaction;
using UnityEngine;

namespace Discover
{
    public class ColocationActiveState : MonoBehaviour, IActiveState
    {
        public bool ActiveWhenUnknown;
        public bool ActiveWhenColocated;
        public bool ActiveWhenRemote;

        public bool Active => AvatarColocationManager.Instance == null ? ActiveWhenUnknown :
            AvatarColocationManager.Instance.IsCurrentPlayerRemote ? ActiveWhenRemote :
            ActiveWhenColocated;
    }
}
