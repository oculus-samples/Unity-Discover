// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.DroneRage.Weapons;
using Discover.Networking;
using UnityEngine;

namespace Discover.DroneRage.PowerUps
{
    public class PowerUpCollector : MonoBehaviour
    {

        [SerializeField]
        public Player.Player Player;

        [SerializeField]
        private Weapon m_weapon;

        private void Start()
        {
            if (!PhotonNetwork.Runner.IsMasterClient())
            {
                Destroy(this);
                return;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!collision.gameObject.TryGetComponent<PowerUp>(out var pwr))
            {
                return;
            }

            pwr.Collect(Player, m_weapon);
        }
    }
}
