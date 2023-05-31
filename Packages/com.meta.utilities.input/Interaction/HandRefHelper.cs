// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_INTERACTION

using Meta.Utilities;
using Oculus.Interaction.Input;

namespace Meta.Utilities.Input
{
    public class HandRefHelper : Singleton<HandRefHelper>
    {
        [UnityEngine.Serialization.FormerlySerializedAs("m_leftHandRef")]
        public Hand LeftHandRef;
        [UnityEngine.Serialization.FormerlySerializedAs("m_rightHandRef")]
        public Hand RightHandRef;

        [UnityEngine.Serialization.FormerlySerializedAs("m_leftHandAnchor")]
        public HandRef LeftHandAnchor;
        [UnityEngine.Serialization.FormerlySerializedAs("m_rightHandAnchor")]
        public HandRef RightHandAnchor;
    }
}

#endif
