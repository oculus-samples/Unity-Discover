// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.DroneRage.Weapons;
using UnityEngine;

namespace Discover.DroneRage.PowerUps
{
    public class HealthPowerUp : PowerUp
    {

        [SerializeField]
        private float m_healing = 20f;
        protected override void OnCollect(Player.Player player, Weapon weapon = null)
        {
            player.Heal(m_healing);
        }
    }
}
