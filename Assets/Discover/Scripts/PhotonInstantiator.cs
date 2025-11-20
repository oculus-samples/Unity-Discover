// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Linq;
using Fusion;
using Meta.Utilities;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover
{
    /// <summary>
    /// When instantiated it will spawn the network object specified
    /// </summary>
    [MetaCodeSample("Discover")]
    public class PhotonInstantiator : Multiton<PhotonInstantiator>
    {
        [SerializeField] private NetworkObject m_networkObject;

        private bool m_instantiated = false;
        private NetworkRunner m_instantiationRunner;
        public void TryInstantiate()
        {
            var thisTransform = transform;
            var runner = NetworkRunner.Instances?.FirstOrDefault();
            if (runner != null && runner.State == NetworkRunner.States.Running)
            {
                if (!m_instantiated || runner != m_instantiationRunner)
                {
                    var obj = runner.Spawn(m_networkObject, position: thisTransform.position,
                        rotation: thisTransform.rotation,
                        onBeforeSpawned: OnBeforeSpawned);
                    Debug.Log($"[PhotonInstantiator] Spawned {obj} at {obj.transform.position}", obj);
                    m_instantiationRunner = runner;
                    m_instantiated = true;
                }
            }
            else
            {
                Debug.Log($"[PhotonInstantiator] Photon disabled; not spawning {m_networkObject} at {thisTransform.position}", this);
            }
        }

        private void Start()
        {
            TryInstantiate();
        }

        private void OnBeforeSpawned(NetworkRunner runner, NetworkObject obj)
        {
            Transform newTrans;
            (newTrans = obj.transform).SetParent(transform.parent);
            newTrans.localScale = transform.localScale;
        }
    }
}