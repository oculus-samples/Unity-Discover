// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discover.Networking;
using Fusion;
using Fusion.Sockets;
using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Discover.Colocation.Test
{
    [MetaCodeSample("Discover")]
    public class ColocationTestBootStrapper : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkRunner m_networkRunner;
        [SerializeField] private NetworkSceneManagerDefault m_sceneManager;
        [SerializeField] private NetworkObject m_colocationPrefab;

        public UnityEvent OnConnectionStarted;
        public UnityEvent<string> OnNetworkEvent;

        private void Awake()
        {
            m_networkRunner.AddCallbacks(this);
            m_networkRunner.ProvideInput = true;
        }

        public void StartHost()
        {
            StartConnection(true);
        }

        public void StartClient()
        {
            StartConnection(false);
        }

        public async void StartConnection(bool isHost)
        {
            OnConnectionStarted?.Invoke();
            OnNetworkEvent?.Invoke("Connecting to Photon...");
            ColocationDriverNetObj.OnColocationCompletedCallback += OnColocationReady;
            await Connect(isHost);
        }

        private async Task Connect(bool isHost)
        {
            var args = new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = "ColocationTest",
                Scene = SceneManager.GetActiveScene().buildIndex,
                SceneManager = m_sceneManager
            };
            _ = await m_networkRunner.StartGame(args);
        }

        private void OnColocationReady(bool success)
        {
            if (success)
            {
                OnNetworkEvent?.Invoke("Colocation Ready");
            }
            else
            {
                OnNetworkEvent?.Invoke("Joined Remotely");
            }
        }

        #region INetworkRunnerCallbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"OnPlayerJoined playerRef: {player}");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"OnPlayerLeft playerRef: {player}");
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            OnNetworkEvent?.Invoke("Connected To Photon");
            if (m_networkRunner.IsMasterClient())
            {
                Debug.Log("Spawn Colocation Prefab");
                _ = m_networkRunner.Spawn(m_colocationPrefab);
            }
        }

        public void OnDisconnectedFromServer(NetworkRunner runner) { }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }

        public void OnSceneLoadDone(NetworkRunner runner) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }
        #endregion // INetworkRunnerCallbacks
    }
}