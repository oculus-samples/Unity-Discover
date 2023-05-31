#if FUSION_WEAVER
namespace Photon.Voice.Fusion
{
    using global::Fusion;
    using Unity;
    using UnityEngine;

    [AddComponentMenu("Photon Voice/Fusion/Voice Network Object")]
    public class VoiceNetworkObject : NetworkBehaviour
    {
#region Private Fields

        // VoiceComponentImpl instance instead if VoiceComponent inheritance
        private VoiceComponentImpl voiceComponentImpl = new VoiceComponentImpl();

        private VoiceConnection voiceConnection;

#endregion
#region Properties

        protected Voice.ILogger Logger => voiceComponentImpl.Logger;

        // to set logging level from code
        public VoiceLogger VoiceLogger => voiceComponentImpl.VoiceLogger;

        /// <summary> The Recorder component currently used by this VoiceNetworkObject </summary>
        public Recorder RecorderInUse { get; private set; }

        /// <summary> The Speaker component currently used by this VoiceNetworkObject </summary>
        public Speaker SpeakerInUse { get; private set; }

        /// <summary> If true, this VoiceNetworkObject has a Speaker that is currently playing received audio frames from remote audio source </summary>
        public bool IsSpeaking => this.SpeakerInUse != null && this.SpeakerInUse.IsPlaying;

        /// <summary> If true, this VoiceNetworkObject has a Recorder that is currently transmitting audio stream from local audio source </summary>
        public bool IsRecording => this.RecorderInUse != null && this.RecorderInUse.IsCurrentlyTransmitting;


        public bool IsLocal => Runner.Topology == SimulationConfig.Topologies.Shared ? this.Object.HasStateAuthority : this.Object.HasInputAuthority;
#endregion

#region Private Methods

        private void SetupRecorder()
        {
            Recorder recorder = null;

            Recorder[] recorders = this.GetComponentsInChildren<Recorder>();
            if (recorders.Length > 0)
            {
                if (recorders.Length > 1)
                {
                    this.Logger.LogWarning("Multiple Recorder components found attached to the GameObject or its children.");
                }
                recorder = recorders[0];
            }

            if (null == recorder && null != this.voiceConnection.PrimaryRecorder)
            {
                recorder = this.voiceConnection.PrimaryRecorder;
            }

            if (null == recorder)
            {
                this.Logger.LogWarning("Cannot find Recorder. Assign a Recorder to VoiceNetworkObject object or set up FusionVoiceClient.PrimaryRecorder.");
            }
            else
            {
                recorder.UserData = this.GetUserData();
                this.voiceConnection.AddRecorder(recorder);
            }
            this.RecorderInUse = recorder;
        }

        private void SetupSpeaker()
        {
            Speaker speaker = null;

            Speaker[] speakers = this.GetComponentsInChildren<Speaker>(true);
            if (speakers.Length > 0)
            {
                speaker = speakers[0];
                if (speakers.Length > 1)
                {
                    this.Logger.LogWarning("Multiple Speaker components found attached to the GameObject or its children. Using the first one we found.");
                }
            }

            if (null == speaker && null != this.voiceConnection.SpeakerPrefab)
            {
                speaker = this.voiceConnection.InstantiateSpeakerPrefab(this.gameObject, false);
            }

            if (null == speaker)
            {
                this.Logger.LogError("No Speaker component or prefab found. Assign a Speaker to VoiceNetworkObject object or set up FusionVoiceClient.SpeakerPrefab.");
            }
            else
            {
                this.Logger.LogInfo("Speaker instantiated.");
            }
            this.SpeakerInUse = speaker;
        }

        private object GetUserData()
        {
            return this.Object.Id;
        }

        public override void Spawned()
        {
            voiceComponentImpl.Awake(this);

            this.voiceConnection = this.Runner.GetComponent<VoiceConnection>();

            if (this.IsLocal)
            {
                this.SetupRecorder();
                if (this.RecorderInUse == null)
                {
                    this.Logger.LogWarning("Recorder not setup for VoiceNetworkObject: playback may not work properly.");
                }
                else
                {
                    if (!this.RecorderInUse.TransmitEnabled)
                    {
                        this.Logger.LogWarning("VoiceNetworkObject.RecorderInUse.TransmitEnabled is false, don't forget to set it to true to enable transmission.");
                    }
                    if (!this.RecorderInUse.isActiveAndEnabled)
                    {
                        this.Logger.LogWarning("VoiceNetworkObject.RecorderInUse may not work properly as RecordOnlyWhenEnabled is set to true and recorder is disabled or attached to an inactive GameObject.");
                    }
                }
            }

            this.SetupSpeaker();
            if (this.SpeakerInUse == null)
            {
                this.Logger.LogWarning("Speaker not setup for VoiceNetworkObject: voice chat will not work.");
            }
            else
            {
                this.voiceConnection.AddSpeaker(this.SpeakerInUse, this.GetUserData());
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            this.voiceConnection.RemoveRecorder(this.RecorderInUse);
        }

        #endregion
    }
}
#endif