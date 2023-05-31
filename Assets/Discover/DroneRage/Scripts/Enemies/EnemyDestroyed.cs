// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Discover.DroneRage.Enemies
{
    public class EnemyDestroyed : MonoBehaviour
    {


        [SerializeField]
        private float m_destroyTime = 2.0f;


        [SerializeField]
        private GameObject[] m_physicsDebris = Array.Empty<GameObject>();

        private async void Start()
        {
            foreach (var go in m_physicsDebris)
            {
                go.transform.SetParent(null, true);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(m_destroyTime), cancellationToken: this.GetCancellationTokenOnDestroy());
            Destroy(gameObject);
        }
    }
}
