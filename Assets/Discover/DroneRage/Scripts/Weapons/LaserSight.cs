// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Utilities.Extensions;
using UnityEngine;

namespace Discover.DroneRage.Weapons
{
    public class LaserSight : MonoBehaviour
    {

        [SerializeField]
        private LayerMask m_raycastLayers = Physics.DefaultRaycastLayers;


        [SerializeField]
        private Transform m_laserDotTransform;

        private void Start()
        {
            m_laserDotTransform.SetWorldScale(m_laserDotTransform.localScale);
        }

        private void LateUpdate()
        {
            var visible = Physics.Raycast(transform.position, transform.forward, out var raycastHit, 100.0f, m_raycastLayers);
            m_laserDotTransform.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            m_laserDotTransform.position = raycastHit.point;
            m_laserDotTransform.forward = raycastHit.normal;
        }
    }
}
