// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_INTERACTION

using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Events;

namespace Meta.Utilities.Input
{
    public class OnHandUpdated : MonoBehaviour
    {
        [SerializeField, Interface(typeof(IHand))]
        private MonoBehaviour m_hand;
        private IHand Hand => (IHand)m_hand;
        [SerializeField] private UnityEvent m_whenHandUpdated;

        private void OnEnable()
        {
            Hand.WhenHandUpdated += OnEvent;
        }

        private void OnDisable()
        {
            Hand.WhenHandUpdated -= OnEvent;
        }

        private void OnEvent() => m_whenHandUpdated?.Invoke();
    }
}

#endif
