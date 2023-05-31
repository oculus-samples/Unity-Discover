// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace MRBike
{
    public class NetworkedBikeTools : MonoBehaviour
    {
        [SerializeField] private GameObject m_networkedWrench;
        [SerializeField] private TransformTarget[] m_wrenchTargets;
        [SerializeField] private GameObject m_wrenchOrigin;

        private void Start()
        {
            InitLocalObjects();
        }

        private void InitLocalObjects()
        {
            if (m_wrenchOrigin != null)
            {
                m_networkedWrench.GetComponent<TransformReset>().ReturnHomeTarget = m_wrenchOrigin.transform;
            }
            if (m_wrenchTargets != null)
            {
                foreach (var target in m_wrenchTargets)
                {
                    target.GrabbedObject = m_networkedWrench;
                }
            }
        }

        public void OnWrenchTargetComplete(GameObject targetHit)
        {
        }
    }
}
