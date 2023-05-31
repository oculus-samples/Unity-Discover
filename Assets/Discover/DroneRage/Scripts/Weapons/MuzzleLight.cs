// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.DroneRage.Weapons
{
    public class MuzzleLight : MonoBehaviour
    {
        [SerializeField] private Light m_muzzlelight;
        [SerializeField] private ParticleSystem m_part;

        // Start is called before the first frame update
        private void Start()
        {
            m_muzzlelight.enabled = false;
        }

        // Update is called once per frame
        private void Update()
        {
            m_muzzlelight.enabled = m_part.particleCount > 0;
        }
    }
}
