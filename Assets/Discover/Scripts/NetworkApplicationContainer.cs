// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Meta.XR.Samples;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Discover
{
    [MetaCodeSample("Discover")]
    public class NetworkApplicationContainer : NetworkBehaviour
    {
        [Networked] public string AppName { get; set; }

        [Networked(OnChanged = nameof(OnIsClosedChanged))]
        public NetworkBool IsClosed { get; private set; }

        private readonly List<GameObject> m_instantiatedObjects = new();
        private readonly List<NetworkObject> m_spawnedObjects = new();

        public override void Spawned()
        {
            Debug.Log($"{nameof(NetworkApplicationContainer)} Spawned {AppName}");
            NetworkApplicationManager.Instance.OnApplicationStart(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            NetworkApplicationManager.Instance.OnApplicationClosed(this);
            DespawnContainedObjects();
        }

        private static void OnIsClosedChanged(Changed<NetworkApplicationContainer> changed)
        {
            var container = changed.Behaviour;
            if (container.IsClosed)
            {
                container.CloseImpl();
            }
        }

        private void DespawnContainedObjects()
        {
            // Need to call ForceGlobalUpdateTrigger() when destroying one or more InteractableTriggerBroadcaster since Physics.autoSimulation is set to false
            Oculus.Interaction.InteractableTriggerBroadcaster.ForceGlobalUpdateTriggers();
            
            foreach (var obj in m_instantiatedObjects)
                Destroy(obj);
            foreach (var obj in m_spawnedObjects)
                Runner.Despawn(obj);

            m_instantiatedObjects.Clear();
            m_spawnedObjects.Clear();
        }

        private async void CloseImpl()
        {
            DespawnContainedObjects();

            if (HasStateAuthority)
            {
                // wait a second for all the clients to despawn their objects
                await UniTask.Delay(1000);

                Runner.Despawn(Object);
            }
        }

        public virtual void Shutdown()
        {
            IsClosed = true;
        }

        public new T Instantiate<T>(
            T original,
            Transform parent,
            bool worldPositionStays = false)
            where T : Object
        {
            var instance = UnityEngine.Object.Instantiate(original, parent);
            m_instantiatedObjects.Add(GetGameObject(instance));
            return instance;
        }

        private static GameObject GetGameObject<T>(T instance) where T : Object =>
            instance switch
            {
                GameObject obj => obj,
                Component c => c.gameObject,
                _ => throw new ArgumentOutOfRangeException(nameof(instance), instance?.GetType()?.FullName)
            };

        public new T Instantiate<T>(
            T original,
            Vector3 position,
            Quaternion rotation,
            Transform parent = default)
            where T : Object
        {
            var instance = UnityEngine.Object.Instantiate(original, position, rotation, parent);
            m_instantiatedObjects.Add(GetGameObject(instance));
            return instance;
        }

        public T NetInstantiate<T>(T prefab, Vector3 transformPosition, Quaternion transformRotation)
            where T : SimulationBehaviour
        {
            var instance = Runner.Spawn(prefab, transformPosition, transformRotation);
            m_spawnedObjects.Add(instance.Object);
            return instance;
        }

        public NetworkObject NetInstantiate(GameObject prefab, Vector3 transformPosition, Quaternion transformRotation)
        {
            var instance = Runner.Spawn(prefab, transformPosition, transformRotation);
            m_spawnedObjects.Add(instance);
            return instance;
        }
    }
}