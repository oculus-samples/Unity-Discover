// ----------------------------------------------------------------------------
// <copyright file="VoiceConnection.cs" company="Exit Games GmbH">
//   Photon Voice for Unity - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>
//  Component that represents a client voice connection to Photon Servers.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#define USE_NEW_TRANSPORT

using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace Photon.Voice.Unity
{
    /// <summary> Component that represents a Voice client and manages a simple Unity integration: a single Recorder and multiple remote speakers. </summary>
    [AddComponentMenu("Photon Voice/Unity Voice Client")]
    [HelpURL("https://doc.photonengine.com/en-us/voice/v2/getting-started/voice-intro")]
    public class UnityVoiceClient : VoiceConnection
    {
        public override bool AlwaysUsePrimaryRecorder => true;

        /// <summary>
        /// Whether or not to use the Voice AppId and all the other AppSettings from Fusion's RealtimeAppSettings ScriptableObject singleton in the Voice client/app.
        /// </summary>
        [field: SerializeField]
        public bool UseVoiceAppSettings = false;

        protected void Start()
        {
            if (this.PrimaryRecorder != null)
            {
                AddRecorder(this.PrimaryRecorder);
            }
        }

        public override bool ConnectUsingSettings(AppSettings overwriteSettings = null)
        {
            if (overwriteSettings != null)
            {
                return base.ConnectUsingSettings(overwriteSettings);
            }
            if (this.UseVoiceAppSettings)
            {
                return base.ConnectUsingSettings(PhotonAppSettings.Instance.AppSettings);
            }
            else
            {
                return base.ConnectUsingSettings();
            }
        }

        protected override Speaker InstantiateSpeakerForRemoteVoice(int playerId, byte voiceId, object userData)
        {
            // Create a new Speaker on each OnRemoteVoiceInfo() call
            return this.InstantiateSpeakerPrefab(this.gameObject, true);
        }
    }

    [DisallowMultipleComponent]
    /// <summary> Component that represents a Voice client. </summary>
    public class VoiceConnection : ConnectionHandler
    {
        #region Private Fields

        // VoiceComponentImpl instance instead if VoiceComponent inheritance
        private VoiceComponentImpl voiceComponentImpl = new VoiceComponentImpl();

        /// <summary>Key to save the "Best Region Summary" in the Player Preferences.</summary>
        private const string PlayerPrefsKey = "VoiceCloudBestRegion";

        private LoadBalancingTransport client;

        private SupportLogger supportLoggerComponent;

        [SerializeField]
        private bool runInBackground = true;

        /// <summary>
        /// time [ms] between statistics calculations
        /// </summary>
        [SerializeField]
        private int statsResetInterval = 1000;

        private int nextStatsTickCount = Environment.TickCount;

        private float statsReferenceTime;
        private int referenceFramesLost;
        private int referenceFramesReceived;

        [SerializeField]
        private GameObject speakerPrefab;

        private List<RemoteVoiceLink> cachedRemoteVoices = new List<RemoteVoiceLink>();

        [SerializeField]
        [FormerlySerializedAs("PrimaryRecorder")]
        private Recorder primaryRecorder;

        /// <summary>
        /// If true, <see cref="VoiceConnection.PrimaryRecorder"/> will be used by this VoiceConnection instnance directly.
        /// </summary>
        [SerializeField]
        [Tooltip("Use primary recorder directly by Voice Client")]
        private bool usePrimaryRecorder;

        // to allow VoiceConnection ignore usePrimaryRecorder and do not show it in Editor
        public virtual bool AlwaysUsePrimaryRecorder => false;

        private List<Speaker> linkedSpeakers = new List<Speaker>();
        private List<Recorder> recorders = new List<Recorder>();

        #endregion

        public VoiceConnection()
        {
#if USE_NEW_TRANSPORT
            this.client = new LoadBalancingTransport2(this.Logger);
#else
            this.client = new LoadBalancingTransport(this.Logger);
#endif
            this.client.VoiceClient.ThreadingEnabled = Application.platform != RuntimePlatform.WebGLPlayer;

            this.client.ClientType = ClientAppType.Voice;
            this.client.VoiceClient.OnRemoteVoiceInfoAction += this.OnRemoteVoiceInfo;
            this.client.StateChanged += this.OnVoiceStateChanged;
            this.client.OpResponseReceived += this.OnOperationResponseReceived;
            base.Client = this.client;
            this.StartFallbackSendAckThread();
        }

#region Public Fields

        /// <summary> Settings to be used by this Voice Client</summary>
        public AppSettings Settings;
#if UNITY_EDITOR
        [HideInInspector]
        public bool ShowSettings = true;
#endif

        /// <summary> Fires when a speaker has been linked to a remote audio stream</summary>
        public event Action<Speaker> SpeakerLinked;
        /// <summary> Fires when a remote voice stream is added</summary>
        public event Action<RemoteVoiceLink> RemoteVoiceAdded;

#if UNITY_PS4 || UNITY_SHARLIN
        /// <summary>PlayStation user ID of the local user</summary>
        /// <remarks>Pass the userID of the local PlayStation user who should receive any incoming audio. This value is used by Photon Voice when sending output to the headphones on the PlayStation.
        /// If you don't provide a user ID, then Photon Voice uses the user ID of the user at index 0 in the list of local users
        /// and in case that there are multiple local users, the audio output might be sent to the headphones of a different user than intended.</remarks>
        public int PlayStationUserID = 0; // set from your games code
#endif

#endregion

#region Properties

        protected Voice.ILogger Logger => voiceComponentImpl.Logger;
        // to set logging level from code
        public VoiceLogger VoiceLogger => voiceComponentImpl.VoiceLogger;

        public new LoadBalancingTransport Client { get { return this.client; } }

        /// <summary>Returns underlying Photon Voice client.</summary>
        public VoiceClient VoiceClient { get { return this.Client.VoiceClient; } }

        /// <summary>Returns Photon Voice client state.</summary>
        public ClientState ClientState { get { return this.Client.State; } }

        /// <summary>Number of frames received per second.</summary>
        public float FramesReceivedPerSecond { get; private set; }
        /// <summary>Number of frames lost per second.</summary>
        public float FramesLostPerSecond { get; private set; }
        /// <summary>Percentage of lost frames.</summary>
        public float FramesLostPercent { get; private set; }

        /// <summary> Prefab that contains Speaker component to be instantiated when receiving a new remote audio source info</summary>
        public GameObject SpeakerPrefab
        {
            get => this.speakerPrefab;
            set => this.speakerPrefab = value;
        }

#if UNITY_EDITOR
        public List<RemoteVoiceLink> CachedRemoteVoices
        {
            get { return this.cachedRemoteVoices; }
        }
#endif

        /// <summary>
        /// Primary Recorder to be used by VoiceConnection implementations directly or via integration objects.
        /// </summary>
        public Recorder PrimaryRecorder
        {
            get => this.primaryRecorder;
            set => this.primaryRecorder = value;
        }

        /// <summary>
        /// Use <see cref="VoiceConnection.PrimaryRecorder"/> directly.
        /// </summary>
        public bool UsePrimaryRecorder => this.usePrimaryRecorder;

        /// <summary>Used to store and access the "Best Region Summary" in the Player Preferences.</summary>
        public string BestRegionSummaryInPreferences
        {
            get
            {
                return PlayerPrefs.GetString(PlayerPrefsKey, null);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.DeleteKey(PlayerPrefsKey);
                }
                else
                {
                    PlayerPrefs.SetString(PlayerPrefsKey, value);
                }
            }
        }

#endregion

#region Public Methods

        /// <summary>
        /// Connect to Photon server using <see cref="Settings"/>
        /// </summary>
        /// <param name="overwriteSettings">Overwrites <see cref="Settings"/> before connecting</param>
        /// <returns>If true voice connection command was sent from client</returns>
        public virtual bool ConnectUsingSettings(AppSettings overwriteSettings = null)
        {
            if (this.Client.LoadBalancingPeer.PeerState != PeerStateValue.Disconnected)
            {
                this.Logger.LogWarning("ConnectUsingSettings() failed. Can only connect while in state 'Disconnected'. Current state: {0}", this.Client.LoadBalancingPeer.PeerState);
                return false;
            }
            if (overwriteSettings != null)
            {
                this.Settings = overwriteSettings;
            }
            if (this.Settings == null)
            {
                this.Logger.LogError("Settings are null");
                return false;
            }
            if (string.IsNullOrEmpty(this.Settings.AppIdVoice) && string.IsNullOrEmpty(this.Settings.Server))
            {
                this.Logger.LogError("Provide an AppId or a Server address in Settings to be able to connect");
                return false;
            }
            if (this.Settings.IsMasterServerAddress && string.IsNullOrEmpty(this.Client.UserId))
            {
                this.Client.UserId = Guid.NewGuid().ToString(); // this is a workaround to use when connecting to self-hosted Photon Server v4, which does not return a UserId to the client if generated randomly server side
            }
            if (string.IsNullOrEmpty(this.Settings.BestRegionSummaryFromStorage))
            {
                this.Settings.BestRegionSummaryFromStorage = this.BestRegionSummaryInPreferences;
            }
            return this.client.ConnectUsingSettings(this.Settings);
        }

        /// <summary>
        /// Tries to link local Speaker with remote voice stream using UserData.
        /// Useful if Speaker created after stream is started.
        /// </summary>
        /// <param name="speaker">Speaker ot try linking.</param>
        /// <param name="userData">UserData object used to bind local Speaker with remote voice stream.</param>
        /// <returns></returns>
        public bool AddSpeaker(Speaker speaker, object userData)
        {
            for (int i = 0; i < this.cachedRemoteVoices.Count; i++)
            {
                RemoteVoiceLink rvl = this.cachedRemoteVoices[i];
                if (userData.Equals(rvl.VoiceInfo.UserData))
                {
                    this.Logger.LogDebug("Speaker linking for remoteVoice {0}.", rvl);
                    this.LinkSpeaker(speaker, rvl);
                    return speaker.IsLinked;
                }
            }

            return false;
        }

#endregion

#region Private Methods

        protected override void Awake()
        {
            base.Awake();
            voiceComponentImpl.Awake(this);

            if (this.ApplyDontDestroyOnLoad)
            {
                // also apply to the relevant VoiceLogger
                DontDestroyOnLoad(voiceComponentImpl.VoiceLogger.gameObject);
            }

            this.supportLoggerComponent = this.GetComponent<SupportLogger>();
            if (this.supportLoggerComponent != null)
            {
                this.supportLoggerComponent.Client = this.Client;
                this.supportLoggerComponent.LogTrafficStats = true;
            }
            if (this.runInBackground)
            {
                Application.runInBackground = this.runInBackground;
            }
        }

        protected virtual void Update()
        {
            this.VoiceClient.Service();
        }

        protected virtual void FixedUpdate()
        {
            while (this.Client.LoadBalancingPeer.DispatchIncomingCommands()) ;
        }

        private void LateUpdate()
        {
            while (this.Client.LoadBalancingPeer.SendOutgoingCommands()) ;

            if (this.statsResetInterval > 0)
            {
                int currentMsSinceStart = Environment.TickCount; // avoiding Environment.TickCount, which could be negative on long-running platforms
                if (currentMsSinceStart - this.nextStatsTickCount > 0)
                {
                    this.CalcStatistics();
                    this.nextStatsTickCount = currentMsSinceStart + this.statsResetInterval;
                }
            }
        }

        protected virtual void OnDestroy()
        {
            this.client.StateChanged -= this.OnVoiceStateChanged;
            this.client.OpResponseReceived -= this.OnOperationResponseReceived;
            this.client.Disconnect();
            if (this.client.LoadBalancingPeer != null)
            {
                this.client.LoadBalancingPeer.Disconnect();
                this.client.LoadBalancingPeer.StopThread();
            }
            this.client.Dispose();
            SupportClass.StopAllBackgroundCalls();
        }

        protected virtual Speaker InstantiateSpeakerForRemoteVoice(int playerId, byte voiceId, object userData)
        {
            throw new Exception("FindSpeakerByUserData: VoiceConnection does not provide userData linkage");
        }

        /// <summary>
        /// Instantiates <see cref="SpeakerPrefab"/>, optionally attaches it to the provided parent.
        /// </summary>
        /// <remarks>
        /// VoiceConnection manages the instantiated object (destroys on OnRemoteVoiceRemoveAction).
        /// </remarks>
        /// <param name="parent">The object to attach Steaker to.</param>
        /// <param name="destroyOnRemove">Automatically destroy instantiated prefab when remote voice is removed (the caller does not manages the instance).</param>
        /// <returns>Instantiated Speaker or null.</returns>
        public Speaker InstantiateSpeakerPrefab(GameObject parent, bool destroyOnRemove)
        {
            if (this.SpeakerPrefab == null)
            {
                this.Logger.LogError("SpeakerPrefab is not set.");
                return null;
            }

            var go = Instantiate(this.SpeakerPrefab);
            Speaker[] speakers = go.GetComponentsInChildren<Speaker>(true);
            if (speakers.Length > 0)
            {
                if (speakers.Length > 1)
                {
                    this.Logger.LogWarning("Multiple Speaker components found attached to the GameObject (VoiceConnection.SpeakerPrefab) or its children. Using the first one we found.");
                }
                if (destroyOnRemove)
                {
                    speakers[0].OnRemoteVoiceRemoveAction += (s) =>
                    {
                        this.Logger.LogInfo("OnRemoteVoiceRemoveAction: destroying VoiceConnection.SpeakerPrefab instance [{0}]", go.name);
                        Destroy(go);
                    };
                }
                if (parent != null)
                {
                    go.transform.SetParent(parent.transform);
                }
                this.Logger.LogInfo("Instance of VoiceConnection.SpeakerPrefab instantiated.");
                return speakers[0];
            }
            else
            {
                this.Logger.LogError("SpeakerPrefab does not have a component of type Speaker in its hierarchy.");
                Destroy(go);
                return null;
            }
        }

        private void OnRemoteVoiceInfo(int channelId, int playerId, byte voiceId, VoiceInfo voiceInfo, ref RemoteVoiceOptions options)
        {
            if (voiceInfo.Codec != Codec.AudioOpus)
            {
                this.Logger.LogInfo("OnRemoteVoiceInfo: Skipped as codec is not Opus, [p#{0} v#{1} c#{2} i:{{{3}}}]", playerId, voiceId, channelId, voiceInfo);
                return;
            }

            RemoteVoiceLink remoteVoice = new RemoteVoiceLink(voiceInfo, playerId, voiceId, channelId, ref options);
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
#if !UNITY_2021_2_OR_NEWER // opus lib requires Emscripten 2.0.19
                this.Logger.LogError("Remote voice Opus decoder requies Unity 2021.2 or newer for WebGL");
                options.Decoder = null; // null Opus decoder set by RemoteVoiceLink
#endif
            }

            this.Logger.LogInfo("OnRemoteVoiceInfo:  {0}", remoteVoice);
            this.cachedRemoteVoices.Add(remoteVoice);
            if (RemoteVoiceAdded != null)
            {
                RemoteVoiceAdded(remoteVoice);
            }
            remoteVoice.RemoteVoiceRemoved += () =>
            {
                this.Logger.LogInfo("OnRemoteVoiceInfo: RemoteVoiceRemoved {0}", remoteVoice);
                this.cachedRemoteVoices.Remove(remoteVoice);
            };
            var speaker = this.InstantiateSpeakerForRemoteVoice(playerId, voiceId, voiceInfo.UserData);
            if (speaker == null)
            {
                this.Logger.LogDebug("OnRemoteVoiceInfo: Remote GameObject not found or does not have a Speaker {0}", remoteVoice);
            }
            else
            {
                speaker.Name = string.Format("Remote p#{0} v#{1}", playerId, voiceId);
                this.LinkSpeaker(speaker, remoteVoice);
            }
        }

        protected virtual void OnVoiceStateChanged(ClientState fromState, ClientState toState)
        {
            this.Logger.LogInfo("OnVoiceStateChanged from {0} to {1}", fromState, toState);
            if (fromState == ClientState.Joined)
            {
                for (int i = 0; i < this.recorders.Count; i++)
                {
                    Recorder rec = this.recorders[i];
                    if (rec.RecordWhenJoined)
                    {
                        rec.RecordingEnabled = false;
                    }
                }
                this.cachedRemoteVoices.Clear();
            }
            switch (toState)
            {
                case ClientState.ConnectedToMasterServer:
                {
                    if (this.Client.RegionHandler != null)
                    {
                        if (this.Settings != null)
                        {
                            this.Settings.BestRegionSummaryFromStorage = this.Client.RegionHandler.SummaryToCache;
                        }
                        this.BestRegionSummaryInPreferences = this.Client.RegionHandler.SummaryToCache;
                    }
                    break;
                }
                case ClientState.Joined:
                {
                    for (int i = 0; i < this.recorders.Count; i++)
                    {
                        Recorder rec = this.recorders[i];
                        if (rec.RecordWhenJoined)
                        {
                            rec.RecordingEnabled = true;
                        }
                    }
                    break;
                }
            }
        }

        protected void CalcStatistics()
        {
            float now = Time.time;
            int recv = this.VoiceClient.FramesReceived - this.referenceFramesReceived;
            int lost = this.VoiceClient.FramesLost - this.referenceFramesLost;
            float t = now - this.statsReferenceTime;

            if (t > 0f)
            {
                if (recv + lost > 0)
                {
                    this.FramesReceivedPerSecond = recv / t;
                    this.FramesLostPerSecond = lost / t;
                    this.FramesLostPercent = 100f * lost / (recv + lost);
                }
                else
                {
                    this.FramesReceivedPerSecond = 0f;
                    this.FramesLostPerSecond = 0f;
                    this.FramesLostPercent = 0f;
                }
            }

            this.referenceFramesReceived = this.VoiceClient.FramesReceived;
            this.referenceFramesLost = this.VoiceClient.FramesLost;
            this.statsReferenceTime = now;
        }

        private void LinkSpeaker(Speaker speaker, RemoteVoiceLink remoteVoice)
        {
#if UNITY_PS4 || UNITY_SHARLIN
                speaker.PlayStationUserID = this.PlayStationUserID;
#endif
            if (speaker.Link(remoteVoice))
            {
                this.Logger.LogInfo("Speaker linked with remote voice {0}", remoteVoice);
                this.linkedSpeakers.Add(speaker);
                remoteVoice.RemoteVoiceRemoved += () =>
                {
                    this.linkedSpeakers.Remove(speaker);
                };
                if (SpeakerLinked != null)
                {
                    SpeakerLinked(speaker);
                }
            }
        }

        public bool AddRecorder(Recorder rec)
        {
            if (!this.recorders.Contains(rec))
            {
                if (rec.Init(this))
                {
                    this.recorders.Add(rec);
                    return true;
                }
                else
                {
                    this.Logger.LogWarning("AddRecorder: failed to init recorder {0}.", rec);
                }
            }
            else
            {
                this.Logger.LogError("AddRecorder: recorder {0} already added.", rec);
            }

            return false;
        }

        public void RemoveRecorder(Recorder rec)
        {
            if (rec != null)
            {
                rec.Deinit(this);
                this.recorders.Remove(rec);
            }
        }

        protected virtual void OnOperationResponseReceived(OperationResponse operationResponse)
        {
            if (operationResponse.ReturnCode != ErrorCode.Ok && (operationResponse.OperationCode != OperationCode.JoinRandomGame || operationResponse.ReturnCode == ErrorCode.NoRandomMatchFound))
            {
                this.Logger.LogError("Operation {0} response error code {1} message {2}", operationResponse.OperationCode, operationResponse.ReturnCode, operationResponse.DebugMessage);
            }
        }

#endregion
    }
}
