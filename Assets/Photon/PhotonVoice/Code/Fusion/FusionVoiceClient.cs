#if FUSION_WEAVER
namespace Photon.Voice.Fusion
{
    using global::Fusion;
    using global::Fusion.Sockets;
    using PhotonAppSettings = global::Fusion.Photon.Realtime.PhotonAppSettings;
    using System.Collections.Generic;
    using Realtime;
    using ExitGames.Client.Photon;
    using UnityEngine;
    using Unity;
    using System;

    [AddComponentMenu("Photon Voice/Fusion/Fusion Voice Client")]
    [RequireComponent(typeof(NetworkRunner))]
    public class FusionVoiceClient : VoiceConnection, INetworkRunnerCallbacks
    {
#region Private Fields

        private NetworkRunner networkRunner;

        private EnterRoomParams voiceRoomParams = new EnterRoomParams
        {
            RoomOptions = new RoomOptions { IsVisible = false }
        };

#endregion

#region Properties

        /// <summary>
        /// Whether or not to use the Voice AppId and all the other AppSettings from Fusion's RealtimeAppSettings ScriptableObject singleton in the Voice client/app.
        /// </summary>
        [field: SerializeField]
        public bool UseFusionAppSettings = true;

        /// <summary>
        /// Whether or not to use the same AuthenticationValues used in Fusion client/app in Voice client/app as well.
        /// This means that the same UserID will be used in both clients.
        /// If custom authentication is used and setup in Fusion AppId from dashboard, the same configuration should be done for the Voice AppId.
        /// </summary>
        [field: SerializeField]
        public bool UseFusionAuthValues = true;

#endregion

#region Private Methods

        protected void Start()
        {
            // skip "Temporary Runner Prefab"
            if (this.networkRunner.State == NetworkRunner.States.Shutdown)
            {
                return;
            }

            if (this.UsePrimaryRecorder)
            {
                if (this.PrimaryRecorder != null)
                {
                    AddRecorder(this.PrimaryRecorder);
                }
                else
                {
                    this.Logger.LogError("Primary Recorder is not set.");
                }
            }

            if (this.networkRunner.IsPlayer && (this.networkRunner.IsConnectedToServer || this.networkRunner.IsServer))
            {
                this.VoiceConnectOrJoinRoom();
            }
        }

        protected override void Awake()
        {
            base.Awake();

            this.networkRunner = this.GetComponent<NetworkRunner>();
            VoiceRegisterCustomTypes();
            this.Client.StateChanged += this.OnVoiceClientStateChanged;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            this.Client.StateChanged -= this.OnVoiceClientStateChanged;
        }

        private void OnVoiceClientStateChanged(ClientState previous, ClientState current)
        {
            this.VoiceConnectOrJoinRoom();
        }

        protected override Speaker InstantiateSpeakerForRemoteVoice(int playerId, byte voiceId, object userData)
        {
            if (userData == null) // Recorder w/o VoiceNetworkObject: probably created due to this.UsePrimaryRecorder = true
            {
                this.Logger.LogInfo("Creating Speaker for remote voice {0}/{1} FusionVoiceClient Primary Recorder (userData == null).", playerId, voiceId);
                return this.InstantiateSpeakerPrefab(this.gameObject, true);
            }

            if (!(userData is NetworkId))
            {
                this.Logger.LogWarning("UserData ({0}) is not of type NetworkId. Remote voice {1}/{2} not linked. Do you have a Recorder not used with a VoiceNetworkObject? is this expected?",
                    userData == null ? "null" : userData.ToString(), playerId, voiceId);
                return null;
            }
            NetworkId networkId = (NetworkId)userData;
            if (!networkId.IsValid)
            {
                this.Logger.LogWarning("NetworkId is not valid ({0}). Remote voice {1}/{2} not linked.", networkId, playerId, voiceId);
                return null;
            }
            VoiceNetworkObject voiceNetworkObject = this.networkRunner.TryGetNetworkedBehaviourFromNetworkedObjectRef<VoiceNetworkObject>(networkId);
            if (ReferenceEquals(null, voiceNetworkObject) || !voiceNetworkObject)
            {
                this.Logger.LogWarning("No voiceNetworkObject found with ID {0}. Remote voice {1}/{2} not linked.", networkId, playerId, voiceId);
                return null;
            }
            this.Logger.LogWarning("Using VoiceNetworkObject {0} Speaker for remote voice {1}/{2}.", userData, playerId, voiceId);
            return voiceNetworkObject.SpeakerInUse;
        }

        private string VoiceGetMirroringRoomName()
        {
            return string.Format("{0}_voice", this.networkRunner.SessionInfo.Name);
        }

        private void VoiceConnectOrJoinRoom()
        {
            switch (this.ClientState)
            {
                case ClientState.PeerCreated:
                case ClientState.Disconnected:
                    if (!this.VoiceConnectAndFollowFusion())
                    {
                        this.Logger.LogError("Connecting to server failed.");
                    }
                    break;
                case ClientState.ConnectedToMasterServer:
                    if (!this.VoiceJoinMirroringRoom())
                    {
                        this.Logger.LogError("Joining a voice room failed.");
                    }
                    break;
                case ClientState.Joined:
                    string expectedRoomName = this.VoiceGetMirroringRoomName();
                    string currentRoomName = this.Client.CurrentRoom.Name;
                    if (!currentRoomName.Equals(expectedRoomName))
                    {
                        this.Logger.LogWarning("Voice room mismatch: Expected:\"{0}\" Current:\"{1}\", leaving the second to join the first.", expectedRoomName, currentRoomName);
                        if (!this.Client.OpLeaveRoom(false))
                        {
                            this.Logger.LogError("Leaving the current voice room failed.");
                        }
                    }
                    break;
            }
        }

        private bool VoiceConnectAndFollowFusion()
        {
            AppSettings settings = new AppSettings();
            if (this.UseFusionAppSettings)
            {
                settings.AppIdVoice = PhotonAppSettings.Instance.AppSettings.AppIdVoice;
                settings.AppVersion = PhotonAppSettings.Instance.AppSettings.AppVersion;
                settings.FixedRegion = PhotonAppSettings.Instance.AppSettings.FixedRegion;
                settings.UseNameServer = PhotonAppSettings.Instance.AppSettings.UseNameServer;
                settings.Server = PhotonAppSettings.Instance.AppSettings.Server;
                settings.Port = PhotonAppSettings.Instance.AppSettings.Port;
                settings.ProxyServer = PhotonAppSettings.Instance.AppSettings.ProxyServer;
                settings.BestRegionSummaryFromStorage = PhotonAppSettings.Instance.AppSettings.BestRegionSummaryFromStorage;
                settings.EnableLobbyStatistics = false;
                settings.EnableProtocolFallback = PhotonAppSettings.Instance.AppSettings.EnableProtocolFallback;
                settings.Protocol = PhotonAppSettings.Instance.AppSettings.Protocol;
                settings.AuthMode = (AuthModeOption)(int)PhotonAppSettings.Instance.AppSettings.AuthMode;
                settings.NetworkLogging = PhotonAppSettings.Instance.AppSettings.NetworkLogging;
            }
            else
            {
                this.Settings.CopyTo(settings);
            }
            string fusionRegion = this.networkRunner.SessionInfo.Region;
            if (string.IsNullOrEmpty(fusionRegion))
            {
                this.Logger.LogWarning("Unexpected: fusion region is empty.");
                if (!string.IsNullOrEmpty(settings.FixedRegion))
                {
                    this.Logger.LogWarning("Unexpected: fusion region is empty while voice region is set to \"{0}\". Setting it to null now.", settings.FixedRegion);
                    settings.FixedRegion = null;
                }
            }
            else if (!string.Equals(settings.FixedRegion, fusionRegion, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(settings.FixedRegion))
                {
                    this.Logger.LogInfo("Setting voice region to \"{0}\" to match fusion region.", fusionRegion);
                }
                else
                {
                    this.Logger.LogInfo("Switching voice region to \"{0}\" from \"{1}\" to match fusion region.", fusionRegion, settings.FixedRegion);
                }
                settings.FixedRegion = fusionRegion;
            }
            if (this.UseFusionAuthValues && this.networkRunner.AuthenticationValues != null)
            {
                this.Client.AuthValues = new AuthenticationValues(this.networkRunner.AuthenticationValues.UserId)
                {
                    AuthGetParameters = this.networkRunner.AuthenticationValues.AuthGetParameters,
                    AuthType = (CustomAuthenticationType)(int)this.networkRunner.AuthenticationValues.AuthType
                };
                if (this.networkRunner.AuthenticationValues.AuthPostData != null)
                {
                    if (this.networkRunner.AuthenticationValues.AuthPostData is byte[] byteData)
                    {
                        this.Client.AuthValues.SetAuthPostData(byteData);
                    }
                    else if (this.networkRunner.AuthenticationValues.AuthPostData is string stringData)
                    {
                        this.Client.AuthValues.SetAuthPostData(stringData);
                    }
                    else if (this.networkRunner.AuthenticationValues.AuthPostData is Dictionary<string, object> dictData)
                    {
                        this.Client.AuthValues.SetAuthPostData(dictData);
                    }
                }
            }
            return this.ConnectUsingSettings(settings);
        }

        private void VoiceDisconnect()
        {
            this.Client.Disconnect();
        }

        private bool VoiceJoinRoom(string voiceRoomName)
        {
            if (string.IsNullOrEmpty(voiceRoomName))
            {
                this.Logger.LogError("Voice room name is null or empty.");
                return false;
            }
            this.voiceRoomParams.RoomName = voiceRoomName;
            return this.Client.OpJoinOrCreateRoom(this.voiceRoomParams);
        }

        private bool VoiceJoinMirroringRoom()
        {
            return this.VoiceJoinRoom(this.VoiceGetMirroringRoomName());
        }

        private static void VoiceRegisterCustomTypes()
        {
            PhotonPeer.RegisterType(typeof(NetworkId), FusionNetworkIdTypeCode, SerializeFusionNetworkId, DeserializeFusionNetworkId);
        }

        private const byte FusionNetworkIdTypeCode = 0; // we need to make sure this does not clash with other custom types?

        private static object DeserializeFusionNetworkId(StreamBuffer instream, short length)
        {
            NetworkId networkId = new NetworkId();
            lock (memCompressedUInt64)
            {
                ulong ul = ReadCompressedUInt64(instream);
                networkId.Raw = (uint)ul;
            }
            return networkId;
        }

        private static ulong ReadCompressedUInt64(StreamBuffer stream)
        {
            ulong value = 0;
            int shift = 0;

            byte[] data = stream.GetBuffer();
            int offset = stream.Position;

            while (shift != 70)
            {
                if (offset >= data.Length)
                {
                    throw new System.IO.EndOfStreamException("Failed to read full ulong.");
                }

                byte b = data[offset];
                offset++;

                value |= (ulong)(b & 0x7F) << shift;
                shift += 7;
                if ((b & 0x80) == 0)
                {
                    break;
                }
            }

            stream.Position = offset;
            return value;
        }

        private static byte[] memCompressedUInt64 = new byte[10];

        private static int WriteCompressedUInt64(StreamBuffer stream, ulong value)
        {
            int count = 0;
            lock (memCompressedUInt64)
            {
                // put values in an array of bytes with variable length encoding
                memCompressedUInt64[count] = (byte)(value & 0x7F);
                value = value >> 7;
                while (value > 0)
                {
                    memCompressedUInt64[count] |= 0x80;
                    memCompressedUInt64[++count] = (byte)(value & 0x7F);
                    value = value >> 7;
                }
                count++;

                stream.Write(memCompressedUInt64, 0, count);
            }
            return count;
        }

        private static short SerializeFusionNetworkId(StreamBuffer outstream, object customobject)
        {
            NetworkId networkId = (NetworkId) customobject;
            return (short)WriteCompressedUInt64(outstream, networkId.Raw);
        }

#endregion

#region INetworkRunnerCallbacks

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            this.Logger.LogInfo("OnPlayerJoined {0}", player);
            if (runner.LocalPlayer == player)
            {
                this.Logger.LogInfo("Local player joined, calling VoiceConnectOrJoinRoom");
                this.VoiceConnectOrJoinRoom();
            }
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            this.Logger.LogInfo("OnPlayerLeft {0}", player);
            if (runner.LocalPlayer == player)
            {
                this.Logger.LogInfo("Local player left, calling VoiceDisconnect");
                this.VoiceDisconnect();
            }
        }

        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
        {
        }

        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
        {
            this.VoiceConnectOrJoinRoom();
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner)
        {
            this.Logger.LogInfo("OnDisconnectedFromServer, calling VoiceDisconnect");
            this.VoiceDisconnect();
        }

        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
        }

        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
        }

        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }

        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
        }

        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
        }

        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data)
        {
        }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
        {
        }

        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner)
        {
        }

#endregion
    }
}
#endif