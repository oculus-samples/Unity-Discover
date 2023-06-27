// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using ColocationPackage;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Oculus.Platform.Models;
using UnityEngine;

namespace Discover.Colocation
{
    public class ColocationDriverNetObj : NetworkBehaviour, INetworkRunnerCallbacks
    {
        public static ColocationDriverNetObj Instance { get; private set; }

        public static Action<bool> OnColocationCompletedCallback;

        public static bool SkipColocation;

        [SerializeField] private GameObject m_networkDataPrefab;
        [SerializeField] private GameObject m_networkDictionaryPrefab;
        [SerializeField] private GameObject m_networkMessengerPrefab;
        [SerializeField] private GameObject m_anchorPrefab;
        [SerializeField] private GameObject m_alignmentAnchorManagerPrefab;

        private AlignmentAnchorManager m_alignmentAnchorManager;
        private ColocationLauncher m_colocationLauncher;

        private Transform m_ovrCameraRigTransform;
        private User m_oculusUser;
        private Guid m_headsetGuid;

        public PhotonPlayerIDDictionary PlayerIDDictionary { get; private set; }

        private void Awake()
        {
            Debug.Assert(Instance == null, $"{nameof(ColocationDriverNetObj)} instance already exists");
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (m_colocationLauncher != null)
            {
                m_colocationLauncher.DestroyAlignementAnchor();
            }

            if (m_alignmentAnchorManager)
            {
                Destroy(m_alignmentAnchorManager.gameObject);
            }
        }

        public void SetPlayerIdDictionary(PhotonPlayerIDDictionary idDictionary)
        {
            PlayerIDDictionary = idDictionary;
        }

        public override void Spawned()
        {
            Runner.AddCallbacks(this);
            Init();
        }

        private async void Init()
        {
            m_ovrCameraRigTransform = FindObjectOfType<OVRCameraRig>().transform;
            m_oculusUser = await OculusPlatformUtils.GetLoggedInUser();
            m_headsetGuid = Guid.NewGuid();
            await SetupForColocation();
        }

        private async UniTask SetupForColocation()
        {
            if (HasStateAuthority)
            {
                Debug.Log("SetUpAndStartColocation for host");
                _ = Runner.Spawn(m_networkDataPrefab).GetComponent<PhotonNetworkData>();
                _ = Runner.Spawn(m_networkDictionaryPrefab).GetComponent<PhotonPlayerIDDictionary>();
                _ = Runner.Spawn(m_networkMessengerPrefab).GetComponent<PhotonNetworkMessenger>();
            }

            Debug.Log("SetUpAndStartColocation: Wait for network objects to spawn");
            await UniTask.WaitUntil(() => NetworkAdapter.NetworkData != null && NetworkAdapter.NetworkMessenger != null && PlayerIDDictionary != null);

            Debug.Log("SetUpAndStartColocation: Add user to Player dictionary");
            AddToIdDictionary(m_oculusUser?.ID ?? default, Runner.LocalPlayer.PlayerId, m_headsetGuid);

            Debug.Log("SetUpAndStartColocation: Initialize messenger");
            var messenger = (PhotonNetworkMessenger)NetworkAdapter.NetworkMessenger;
            messenger.Init(PlayerIDDictionary);

            var sharedAnchorManager = new SharedAnchorManager
            {
                AnchorPrefab = m_anchorPrefab
            };

            m_alignmentAnchorManager =
                Instantiate(m_alignmentAnchorManagerPrefab).GetComponent<AlignmentAnchorManager>();

            m_alignmentAnchorManager.Init(m_ovrCameraRigTransform);
            Debug.Log("SetUpAndStartColocation: Colocation Launch Init");

            var overrideEventCode = new Dictionary<CaapEventCode, byte> {
                {CaapEventCode.TellOwnerToShareAnchor, 4},
                {CaapEventCode.TellAnchorRequesterToLocalizeAnchor, 7}
            };

            m_colocationLauncher = new ColocationLauncher();
            m_colocationLauncher.Init(
                m_oculusUser?.ID ?? default,
                m_headsetGuid,
                NetworkAdapter.NetworkData,
                NetworkAdapter.NetworkMessenger,
                sharedAnchorManager,
                m_alignmentAnchorManager,
                overrideEventCode
            );

            m_colocationLauncher.RegisterOnAfterColocationReady(OnAfterColocationReady);
            if (HasStateAuthority)
            {
                m_colocationLauncher.CreateColocatedSpace();
            }
            else
            {
                // Don't try to colocate if we join remotely
                if (SkipColocation)
                {
                    OnColocationCompletedCallback?.Invoke(false);
                }
                else
                {
                    m_colocationLauncher.CreateAnchorIfColocationFailed = false;
                    m_colocationLauncher.OnAutoColocationFailed += OnColocationFailed;
                    m_colocationLauncher.ColocateAutomatically();
                }
            }
        }

        private void OnAfterColocationReady()
        {
            Debug.Log("Colocation is Ready!");
            OnColocationCompletedCallback?.Invoke(true);
        }

        private void OnColocationFailed()
        {
            Debug.Log("Colocation failed!");
            OnColocationCompletedCallback?.Invoke(false);
        }

        private void AddToIdDictionary(ulong oculusId, int playerId, Guid headsetGuid)
        {
            if (HasStateAuthority)
            {
                PlayerIDDictionary.Add(oculusId, playerId, headsetGuid);
            }
            else
            {
                TellHostToAddToIdDictionaryServerRpc(oculusId, playerId, headsetGuid);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void TellHostToAddToIdDictionaryServerRpc(ulong oculusId, int playerId, Guid headsetGuid)
        {
            PlayerIDDictionary.Add(oculusId, playerId, headsetGuid);
            Debug.Log($"TellHostToAddToIdDictionaryServerRpc: {PlayerIDDictionary}");
        }

        #region INetworkRunnerCallbacks
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (HasStateAuthority)
            {
                Debug.Log($"[ColocationDriverNetObj] Player {player} left, removing from dictionary and colocationLauncher");
                var oculusId = PlayerIDDictionary.GetOculusId(player);
                if (oculusId.HasValue)
                {
                    m_colocationLauncher.OnPlayerLeft(oculusId.Value);
                }

                PlayerIDDictionary.RemoveUsingNetworkId((int)player);
            }
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

        public void OnConnectedToServer(NetworkRunner runner) { }

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
        #endregion //INetworkRunnerCallbacks
    }
}