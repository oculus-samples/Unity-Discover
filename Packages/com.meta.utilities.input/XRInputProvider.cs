// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_AVATARS

using Meta.Utilities;
using Meta.Utilities.Input;
using UnityEngine;

namespace Meta.Decommissioned.Input
{
    public class XRInputProvider : Singleton<XRInputProvider>
    {
        [field: SerializeField, AutoSet]
        public XRInputManager InputManager { get; private set; }
    }
}

#endif
