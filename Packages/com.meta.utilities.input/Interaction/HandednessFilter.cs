// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_INTERACTION

using Oculus.Interaction;
using UnityEngine;

namespace Meta.Utilities.Input
{
    public class HandednessFilter : MonoBehaviour, IGameObjectFilter
    {
        [SerializeField]
        private Oculus.Interaction.Input.Handedness m_hand;
        public bool Filter(GameObject gameObject)
        {
            return gameObject.TryGetComponent<Oculus.Interaction.Input.HandRef>(out var hand) && hand.Handedness == m_hand;
        }
    }
}

#endif
