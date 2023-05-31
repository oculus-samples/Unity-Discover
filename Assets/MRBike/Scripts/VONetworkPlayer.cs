// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Utilities;
using UnityEngine;

namespace MRBike
{
    public class VONetworkPlayer : MonoBehaviour
    {
        [AutoSetFromParent(IncludeInactive = true)]
        [SerializeField] private VONetworkManager m_voManager;

        private void Awake()
        {
            if (m_voManager == null)
            {
                m_voManager = GetComponentInParent<VONetworkManager>();
            }
        }

        public void PlayVO(int voNum)
        {
            if (m_voManager == null)
            {
                Debug.LogError($"{nameof(VONetworkPlayer)}: No {nameof(VONetworkManager)} found in parent. " +
                               $"Can't play VO from {name}");
                return;
            }

            m_voManager.PlayVO(voNum);
        }
    }
}
