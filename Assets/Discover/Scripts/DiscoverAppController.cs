// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Discover.Colocation;
using Discover.Menus;
using Discover.Networking;
using Discover.NUX;
using Discover.UI.Modal;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using Meta.Utilities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Discover
{
    public class DiscoverAppController : Singleton<DiscoverAppController>, INetworkRunnerCallbacks
    {
        private const string NUX_NETWORK_KEY = "NUX_Network";
        private const string NUX_EXPERIENCE_KEY = "NUX_Experience";
        [SerializeField] private NetworkSceneManagerDefault m_sceneManager;
        [SerializeField] private NetworkObject m_colocationPrefab;
        [SerializeField] private MRSceneLoader m_mrSceneLoader;
        [SerializeField] private NetworkObject m_playerPrefab;
        [SerializeField] private NetworkRunner m_networkRunnerPrefab;
        [SerializeField] private NetworkObject m_networkApplicationManagerPrefab;

        [SerializeField] private string m_roomName;
        private NetworkObject m_playerObject;
        private string m_selectedRegionCode = null;

        private bool m_showPlayerId;

        public UnityEvent OnHostMigrationOccured;

        public Action OnShowPlayerIdChanged;

        public NetworkRunner Runner { get; private set; }

        public bool ShowPlayerId
        {
            get => m_showPlayerId;
            set
            {
                m_showPlayerId = value;
                OnShowPlayerIdChanged?.Invoke();
            }
        }

        private void Start()
        {
            MainMenuController.Instance.EnableMenuButton(false);
            ShowNetworkNux();
        }

        public void OnRegionSelected(string regionCode)
        {
            m_selectedRegionCode = regionCode;
        }

        public void ShowNetworkNux()
        {
            NUXManager.Instance.StartNux(NUX_NETWORK_KEY, OnNetworkNuxCompleted);
        }

        [ContextMenu("Host")]
        private void DebugHost()
        {
            NetworkModalWindowController.Instance.Hide();
            BeginHosting(m_roomName);
        }

        [ContextMenu("Join")]
        private void DebugJoin()
        {
            NetworkModalWindowController.Instance.Hide();
            BeginJoining(m_roomName, false);
        }

        [ContextMenu("Join Remote")]
        private void DebugJoinRemote()
        {
            NetworkModalWindowController.Instance.Hide();
            BeginJoining(m_roomName, true);
        }

        public void StartSinglePlayer()
        {
            NUXManager.Instance.StartNux(
                NUX_EXPERIENCE_KEY,
                () => StartConnectionAsync(true, GameMode.Single));
        }

        public void StartHost()
        {
            NUXManager.Instance.StartNux(
                NUX_EXPERIENCE_KEY,
                () => StartConnection(true));
        }

        public void StartClient()
        {
            if (string.IsNullOrWhiteSpace(m_roomName))
            {
                NetworkModalWindowController.Instance.ShowMessage("Enter room name to join");
                ShowNetworkSelectionMenu();
            }
            else
            {
                NUXManager.Instance.StartNux(
                    NUX_EXPERIENCE_KEY,
                    () => StartConnection(false));
            }
        }

        private void StartConnection(bool isHost)
        {
            StartConnectionAsync(isHost);
        }

        private async void StartConnectionAsync(bool isHost, GameMode mode = GameMode.Shared)
        {
            if (isHost)
            {
                Debug.Log("Load MR Scene");
                NetworkModalWindowController.Instance.ShowMessage("Loading Room");
                _ = await m_mrSceneLoader.LoadScene();
            }

            Debug.Log("StartConnection");
            SetupForNetworkRunner();
            NetworkModalWindowController.Instance.ShowMessage("Connecting to Photon...");
            ColocationDriverNetObj.OnColocationCompletedCallback += OnColocationReady;
            ColocationDriverNetObj.SkipColocation = AvatarColocationManager.Instance.IsCurrentPlayerRemote;
            await Connect(isHost, mode);
        }

        private async UniTask Connect(bool isHost, GameMode mode)
        {
            var sessionName = string.IsNullOrWhiteSpace(m_roomName) ? null : m_roomName;
            if (isHost && string.IsNullOrWhiteSpace(sessionName))
            {
                // if we Host and no room name was given we create a random 6 character room name
                sessionName = RoomNameGenerator.GenerateRoom(6);
                m_roomName = sessionName;
                // Given the scope we don't check for collision with existing room name, but checking if the room exists
                // in the lobby would be a great validator to make sure we don't join someone else session.
            }

            var args = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = sessionName,
                Scene = SceneManager.GetActiveScene().buildIndex,
                SceneManager = m_sceneManager,
                DisableClientSessionCreation = !isHost,
                IsVisible = false,
            };
            if (!string.IsNullOrEmpty(m_selectedRegionCode))
            {
                args.CustomPhotonAppSettings = CreateAppSettingsForRegion(m_selectedRegionCode);
            }

            var joined = await Runner.StartGame(args);
            if (!joined.Ok)
            {
                var errorMsg = $"Connection failed: {joined.ShutdownReason}";
                Debug.LogError(errorMsg);
                NetworkModalWindowController.Instance.ShowMessage(errorMsg);
                ShowNetworkSelectionMenu();
            }
        }

        private AppSettings CreateAppSettingsForRegion(string region)
        {
            var appSettings = PhotonAppSettings.Instance.AppSettings.GetCopy();

            if (!string.IsNullOrEmpty(region))
            {
                appSettings.FixedRegion = region.ToLower();
            }

            return appSettings;
        }

        private void ShowNetworkSelectionMenu()
        {
            // Ensure main menu is closed and disabled until we reconnect
            MainMenuController.Instance.CloseMenu();
            MainMenuController.Instance.EnableMenuButton(false);
            NetworkModalWindowController.Instance.ShowNetworkSelectionMenu(
                BeginHosting,
                BeginJoining,
                BeginSinglePlayer,
                OnRegionSelected,
                m_roomName
            );
        }

        public void BeginSinglePlayer()
        {
            NetworkModalWindowController.Instance.Hide();
            AvatarColocationManager.Instance.IsCurrentPlayerRemote = false;
            StartSinglePlayer();
        }

        public void BeginHosting(string roomName)
        {
            NetworkModalWindowController.Instance.Hide();
            m_roomName = roomName;
            AvatarColocationManager.Instance.IsCurrentPlayerRemote = false;
            StartHost();
        }

        public void BeginJoining(string roomName, bool isRemote)
        {
            NetworkModalWindowController.Instance.Hide();
            m_roomName = roomName;
            AvatarColocationManager.Instance.IsCurrentPlayerRemote = isRemote;
            StartClient();
        }

        private void OnNetworkNuxCompleted()
        {
            ShowNetworkSelectionMenu();
        }

        private void OnColocationReady(bool success)
        {
            if (success)
            {
                NetworkModalWindowController.Instance.ShowMessage("Colocation Ready");
            }
            else
            {
                AvatarColocationManager.Instance.IsCurrentPlayerRemote = true;
                AvatarColocationManager.Instance.LocalPlayer.IsRemote = true;
                AvatarColocationManager.Instance.OnLocalPlayerColocationGroupUpdated?.Invoke();
                NetworkModalWindowController.Instance.ShowMessage("Joined Remotely");
            }
        }

        private void SetupForNetworkRunner()
        {
            if (Runner != null)
            {
                return;
            }

            Runner = Instantiate(m_networkRunnerPrefab);

            Runner.AddCallbacks(this);
            Runner.ProvideInput = true;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            Debug.Log($"OnApplicationPause {pauseStatus}");
            if (!pauseStatus)
            {
                if (Runner != null && !Runner.IsConnectedToServer)
                {
                    Debug.LogWarning("Disconnected from room when coming back to application");
                    DisconnectFromRoom();
                }
            }
        }

        #region INetworkRunnerCallbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"OnPlayerJoined playerRef: {player}");

            if (runner.GameMode is GameMode.Single)
            {
                OnConnectedToServer(runner);
            }
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
            NetworkModalWindowController.Instance.ShowMessage(
                $"Connected To Photon Session: {runner.SessionInfo.Name}");
            if (runner.IsMasterClient())
            {
                Debug.Log("Instantiate Room Scene objects");
                foreach (var instantiator in PhotonInstantiator.Instances)
                {
                    instantiator.TryInstantiate();
                }

                Debug.Log("Spawn Network Application Manager Prefab");
                _ = Runner.Spawn(m_networkApplicationManagerPrefab);
                Debug.Log("Spawn Colocation Prefab");
                _ = Runner.Spawn(m_colocationPrefab);

                AppsManager.Instance.CanMoveIcon = true;
                AppsManager.Instance.InitializeIcons();
            }

            m_playerObject = runner.Spawn(
                m_playerPrefab, onBeforeSpawned: (_, obj) =>
                {
                    obj.GetComponent<DiscoverPlayer>().IsRemote =
                        AvatarColocationManager.Instance.IsCurrentPlayerRemote;
                });
            runner.SetPlayerObject(runner.LocalPlayer, m_playerObject);
            MainMenuController.Instance.EnableMenuButton(true);
        }

        public void OnDisconnectedFromServer(NetworkRunner runner) { }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            OnHostMigrationOccured?.Invoke();
            AppsManager.Instance.CanMoveIcon = runner.IsMasterClient();
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }

        public void OnSceneLoadDone(NetworkRunner runner) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }

        #endregion // INetworkRunnerCallbacks

        public async void DisconnectFromRoom()
        {
            await Runner.Shutdown();
            NetworkModalWindowController.Instance.ShowMessage(
                $"You left the Room");
            ShowNetworkSelectionMenu();
        }
    }
}