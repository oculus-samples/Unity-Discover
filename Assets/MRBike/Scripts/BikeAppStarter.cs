// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace MRBike
{
    public class BikeAppStarter : MonoBehaviour
    {
        [SerializeField] private VONetworkPlayer m_voPlayer;

        [SerializeField] private TaskHandler m_taskHandler;

        [SerializeField] private float m_delay = 10;

        private void Start()
        {
            // When we start the app, play the VO and mark the 2 first tasks as completed
            m_taskHandler.TaskComplete(0);
            Invoke("PlayDelay", m_delay);
        }

        private void PlayDelay()
        {
            m_taskHandler.TaskComplete(1);
        }
    }
}
