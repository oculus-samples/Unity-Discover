// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_INTERACTION

using Meta.Utilities;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Meta.Utilities.Input
{
    public class XRHandRefChooser : MonoBehaviour
    {
        [SerializeField] private HandRef[] m_targetRefs = new HandRef[0];
        [Interface(typeof(IHand))]
        [SerializeField] private MonoBehaviour[] m_handTrackingHands = new MonoBehaviour[0];
        [Interface(typeof(IHand))]
        [SerializeField] private MonoBehaviour[] m_virtualHands = new MonoBehaviour[0];

        [SerializeField] private GameObject[] m_setActiveForHandTracking = new GameObject[0];
        [SerializeField] private GameObject[] m_setActiveForVirtualHands = new GameObject[0];

        // TODO: Optimize away the Update
        private bool? m_wasHandTracking;

#if HAS_NAUGHTY_ATTRIBUTES
        [NaughtyAttributes.ShowNativeProperty]
#endif
        public bool IsHandTracking => m_wasHandTracking ?? false;

        private void Update()
        {
            var isHandTracking = (OVRInput.GetConnectedControllers() & OVRInput.Controller.Hands) != 0;
            if (m_wasHandTracking == isHandTracking)
                return;
            m_wasHandTracking = isHandTracking;

            var sources = isHandTracking ? m_handTrackingHands : m_virtualHands;
            foreach (var (source, target) in sources.Zip(m_targetRefs))
            {
                target.InjectAllHandRef(source as IHand);
            }

            foreach (var obj in m_setActiveForHandTracking)
            {
                obj.SetActive(isHandTracking);
            }
            foreach (var obj in m_setActiveForVirtualHands)
            {
                obj.SetActive(!isHandTracking);
            }
        }
    }
}

#endif
