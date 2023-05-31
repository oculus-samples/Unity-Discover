// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Networking;
using Fusion;
using UnityEngine;
using static Discover.DroneRage.Bootstrapper.DroneRageAppContainerUtils;

namespace Discover.DroneRage.Scene
{
    public class NetworkedScenePrimitive : MonoBehaviour
    {


        [SerializeField]
        private GameObject m_networkedScenePrimitivePrefab;
        private NetworkObject m_primitive;

        private void OnEnable()
        {
            Debug.Log($"{nameof(NetworkedScenePrimitive)}: Instantiating primitive.");
            m_primitive = GetAppContainer().NetInstantiate(m_networkedScenePrimitivePrefab, transform.position, transform.rotation);
            m_primitive.transform.localScale = transform.lossyScale;
        }

        private void OnDisable()
        {
            m_primitive.Despawn();
        }
    }
}
