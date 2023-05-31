// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

namespace MRBike
{
    public class HandednessGrabUtils : MonoBehaviour
    {
        [SerializeField] private HandGrabInteractable m_rightGrab;
        [SerializeField] private HandGrabInteractable m_leftGrab;
        [SerializeField] private HandedObjectSwapper m_handedObjectSwapper;

        private void OnEnable()
        {
            m_rightGrab.WhenPointerEventRaised += SetRight;
            m_leftGrab.WhenPointerEventRaised += SetLeft;
        }

        private void SetRight(PointerEvent p)
        {
            if (m_handedObjectSwapper != null)
            {
                m_handedObjectSwapper.SetRight();
            }
        }

        private void SetLeft(PointerEvent p)
        {
            if (m_handedObjectSwapper != null)
            {
                m_handedObjectSwapper.SetLeft();
            }
        }
    }
}
