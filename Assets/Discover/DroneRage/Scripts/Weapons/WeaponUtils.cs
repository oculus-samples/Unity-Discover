// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.DroneRage.Weapons
{
    public static class WeaponUtils
    {
        /*
        * Given the maximum horizontal and vertical weapon spread at a distance of 1m,
        * returns a shot direction with rectilinearly uniform distributed randomized spread
        */
        public static Vector3 RandomSpread(Vector2 spreadRange)
        {
            var shotDir = Vector3.forward;
            shotDir += new Vector3(Random.Range(-0.5f * spreadRange.x, 0.5f * spreadRange.x),
                                   Random.Range(-0.5f * spreadRange.y, 0.5f * spreadRange.y));
            return shotDir.normalized;
        }
    }
}
