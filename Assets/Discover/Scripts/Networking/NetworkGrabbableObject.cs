// Copyright (c) Meta Platforms, Inc. and affiliates.

using Fusion;
using Meta.Utilities;
using Meta.XR.Samples;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace Discover.Networking
{
    [RequireComponent(typeof(Grabbable))]
    [MetaCodeSample("Discover")]
    public class NetworkGrabbableObject : NetworkBehaviour
    {
        [AutoSet]
        [SerializeField] private Grabbable m_grabbable;

        public UnityEvent<bool> OnUnselected;

        private void OnEnable()
        {
            m_grabbable.WhenPointerEventRaised += OnPointerEventRaised;
        }

        private void OnDisable()
        {
            m_grabbable.WhenPointerEventRaised -= OnPointerEventRaised;
        }

        private void OnPointerEventRaised(PointerEvent pointerEvent)
        {
            switch (pointerEvent.Type)
            {
                case PointerEventType.Select:
                    if (m_grabbable.SelectingPointsCount == 1)
                    {
                        TransferOwnershipToLocalPlayer();
                    }
                    break;
                case PointerEventType.Unselect:
                    OnUnselected?.Invoke(HasStateAuthority);
                    break;
                case PointerEventType.Hover:
                    break;
                case PointerEventType.Unhover:
                    break;
                case PointerEventType.Move:
                    break;
                case PointerEventType.Cancel:
                    break;
                default:
                    break;
            }
        }

        private void TransferOwnershipToLocalPlayer()
        {
            if (!HasStateAuthority)
            {
                Object.RequestStateAuthority();
            }
        }
    }
}