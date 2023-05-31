// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Utilities.Extensions;
using Fusion;
using UnityEngine;

namespace Discover.DroneRage.Weapons
{
    public class BasicDamageablePhoton : NetworkBehaviour
    {

        [HideInInspector]
        public GameObject ImpactPrefab;

        public void OnHit(Vector3 position, Vector3 normal)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            TakeDamageClientRPC(position, normal);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void TakeDamageClientRPC(Vector3 position, Vector3 normal)
        {
            var impactParticles = ImpactPrefab.GetComponent<BulletImpactParticles>();
            var go = BulletImpactParticles.Create(impactParticles, transform).gameObject;
            go.transform.position = position;
            go.transform.forward = normal;
            go.transform.SetWorldScale(ImpactPrefab.transform.localScale);
        }
    }
}
