// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.DroneRage.Bootstrapper;
using UnityEngine;

namespace Discover.DroneRage.Audio
{
    public class AudioTester : MonoBehaviour
    {
        public GameObject FXPrefab;
        public AudioTriggerExtended[] StartTriggers;
        public AudioTriggerExtended[] Triggers;
        public AudioTriggerExtended[] StopTriggers;

        private bool m_spaceHit;

        private void Update()
        {
            if (Input.GetKey(KeyCode.Space) && m_spaceHit == false)
            {
                // Play sound
                m_spaceHit = true;

                if (FXPrefab != null)
                {
                    _ = DroneRageAppContainerUtils.GetAppContainer().Instantiate(FXPrefab, transform);
                }

                foreach (var trg in Triggers)
                {
                    trg.PlayAudio();
                }

                foreach (var trg in StartTriggers)
                {
                    trg.PlayAudio();
                }
            }

            if (!Input.GetKey(KeyCode.Space))
            {
                if (m_spaceHit)
                {
                    foreach (var trg in StopTriggers)
                    {
                        trg.PlayAudio();
                    }

                    foreach (var trg in Triggers)
                    {
                        trg.StopAudio();
                    }
                }

                m_spaceHit = false;
            }
        }
    }
}