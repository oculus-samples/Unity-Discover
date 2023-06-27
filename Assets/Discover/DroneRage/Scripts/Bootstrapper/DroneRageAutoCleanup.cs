// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Networking;
using Fusion;
using Meta.Utilities;
using UnityEngine;

namespace Discover.DroneRage.Bootstrapper
{
    public class DroneRageAutoCleanup : MonoBehaviour
    {
        [SerializeField, AutoSet]
        private NetworkObject m_toDestroy = null;

        private void Start()
        {
            if (!DroneRageAppLifecycle.Instance.IsAppRunning)
            {
                Cleanup();
                return;
            }
            DroneRageAppLifecycle.Instance.AppExited += OnAppExited;
        }

        private void OnDestroy()
        {
            DroneRageAppLifecycle.Instance.AppExited -= OnAppExited;
        }

        private void OnAppExited()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (m_toDestroy != null)
            {
                if (m_toDestroy.TryGetComponent(out NetworkObject photonView))
                {
                    if (PhotonNetwork.Runner.IsMasterClient() || photonView.HasStateAuthority)
                    {
                        m_toDestroy.Despawn();
                    }
                }
                else
                {
                    Destroy(m_toDestroy);
                }
                m_toDestroy = null;
            }
        }
    }
}
