// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using static Discover.DroneRage.Bootstrapper.DroneRageAppContainerUtils;

namespace Discover.DroneRage.Enemies
{
    public class EnemyDeathHandler : MonoBehaviour
    {


        [SerializeField]
        public GameObject DestroyedPrefab;

        private void OnDestroy()
        {
            var container = GetAppContainer();
            if (container == null || DestroyedPrefab == null)
            {
                return;
            }

            _ = container.Instantiate(DestroyedPrefab, transform.position, transform.rotation);
        }
    }
}
