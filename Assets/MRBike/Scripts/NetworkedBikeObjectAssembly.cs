// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Interaction;
using UnityEngine;

namespace MRBike
{
    /// <summary>
    /// Assembles the parts with their targets and origin.
    /// Logic to trigger the vo when piece hits the target
    /// </summary>
    public class NetworkedBikeObjectAssembly : MonoBehaviour
    {
        [SerializeField] private GameObject m_spawnedNetworkObject = null;
        [SerializeField] private TransformTarget m_target;
        [SerializeField] private GameObject m_origin;
        [SerializeField] private TaskVOPlayer m_voPlayer;
        [SerializeField] private int m_voClipNum;

        private bool m_inFinishedState = false;
        private bool m_pedalFinished = false;

        private void Start()
        {
            InitNetworkedObject();
        }

        private void InitNetworkedObject()
        {
            m_spawnedNetworkObject.GetComponent<PointableUnityEventWrapper>().WhenSelect.AddListener(ObjectSelected);
            m_spawnedNetworkObject.GetComponent<PointableUnityEventWrapper>().WhenRelease.AddListener(ObjectReleased);

            if (m_origin)
                m_spawnedNetworkObject.GetComponent<TransformReset>().ReturnHomeTarget = m_origin.transform;

            if (m_target)
                m_target.GrabbedObject = m_spawnedNetworkObject;
        }

        private void ObjectSelected()
        {
            if (m_target)
                m_target.gameObject.SetActive(true);
            if (m_voPlayer)
                m_voPlayer.PlayOnce(m_voClipNum);
        }

        private void ObjectReleased()
        {
            if (m_target)
                m_target.gameObject.SetActive(false);
        }

        public void CompletedBikeState(string state)
        {
            if (!m_inFinishedState)
            {
                m_inFinishedState = true;
            }
        }

        public void CompletedLeftPedalAttachment()
        {
            if (!m_pedalFinished)
            {
                m_pedalFinished = true;
            }
        }

        public void CompletedRightPedalAttachment()
        {
            if (!m_pedalFinished)
            {
                m_pedalFinished = true;
            }
        }
    }
}
