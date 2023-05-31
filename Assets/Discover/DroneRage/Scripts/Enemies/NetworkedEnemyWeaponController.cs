// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.DroneRage.Weapons;
using Fusion;
using UnityEngine;

namespace Discover.DroneRage.Enemies
{
    public class NetworkedEnemyWeaponController : NetworkBehaviour
    {


        [SerializeField]
        private Weapon m_controlledWeaponLeft;

        [SerializeField]
        private Weapon m_controlledWeaponRight;

        [SerializeField]
        private EnemyWeaponVisuals m_controlledWeaponVisuals;

        private void OnEnable()
        {
            m_controlledWeaponLeft.WeaponFired += OnWeaponFired;
            m_controlledWeaponRight.WeaponFired += OnWeaponFired;
        }

        private void OnDisable()
        {
            m_controlledWeaponLeft.WeaponFired -= OnWeaponFired;
            m_controlledWeaponRight.WeaponFired -= OnWeaponFired;
        }

        private void OnWeaponFired(Vector3 shotOrigin, Vector3 shotDirection)
        {
            if (!HasStateAuthority)
            {
                return;
            }
            WeaponFiredClientRPC(shotOrigin, shotDirection);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
        private void WeaponFiredClientRPC(Vector3 shotOrigin, Vector3 shotDirection)
        {
            m_controlledWeaponVisuals.OnWeaponFired(shotOrigin, shotDirection);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_controlledWeaponVisuals == null)
            {
                m_controlledWeaponVisuals = GetComponentInChildren<EnemyWeaponVisuals>();
            }
        }
#endif
    }
}
