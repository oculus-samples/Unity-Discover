// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.DroneRage.Enemies
{
    public interface EnemyState
    {
        void EnterState(Enemy e, EnemyState lastState);
        void OnCollisionStay(Enemy e, Collision c);
        void OnProximityStay(Enemy e, Collider c);
        void UpdateState(Enemy e);
        void ExitState(Enemy e, EnemyState nextState);
    }
}
