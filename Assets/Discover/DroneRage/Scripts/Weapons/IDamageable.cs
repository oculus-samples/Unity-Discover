// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.DroneRage.Weapons
{
    public interface IDamageable
    {
        public delegate void DamageCallback(IDamageable damagableAffected, float hpAffected, bool targetDied);

        void Heal(float healing, DamageCallback callback = null);
        void TakeDamage(float damage, Vector3 position, Vector3 normal, DamageCallback callback = null);
    }
}
