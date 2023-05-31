// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_INTERACTION

using System.Linq;
using Oculus.Interaction;
using UnityEngine;
using static Oculus.Interaction.RayInteractor;

namespace Meta.Utilities.Input
{
    public class InteractableFilterActiveState : MonoBehaviour, IActiveState
    {
        [SerializeField, Interface(typeof(IInteractorView))]
        private MonoBehaviour m_interactor;
        private IInteractorView Interactor => (IInteractorView)m_interactor;

        [SerializeField]
        private string[] m_excludedTags;

        public bool Active => Interactor.CandidateProperties is RayCandidateProperties candidate &&
            candidate.ClosestInteractable != null &&
            m_excludedTags.All(t => candidate.ClosestInteractable.CompareTag(t) is false);
    }
}

#endif
