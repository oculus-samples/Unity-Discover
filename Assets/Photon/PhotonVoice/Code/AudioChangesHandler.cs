namespace Photon.Voice.Unity
{
    using Voice;
    using UnityEngine;

    /// <summary>
    /// This component is useful to handle audio device and config changes.
    /// </summary>
    [RequireComponent(typeof(Recorder))]
    [AddComponentMenu("Photon Voice/Audio Changes Handler")]
    [DisallowMultipleComponent]
    public class AudioChangesHandler : VoiceComponent
    {
        private IAudioInChangeNotifier photonMicChangeNotifier;
        private Recorder recorder;

        /// <summary>
        /// Try to react to device change notification when Recorder is started.
        /// </summary>
        [Tooltip("React to device change notification when Recorder is started.")]
        public bool HandleDeviceChange = true;
        /// <summary>
        /// iOS: Try to react to device change notification when Recorder is started.
        /// </summary>
        [Tooltip("iOS: React to device change notification when Recorder is started.")]
        public bool HandleDeviceChangeIOS;
        /// <summary>
        /// Android: Try to react to device change notification when Recorder is started.
        /// </summary>
        [Tooltip("Android: React to device change notification when Recorder is started.")]
        public bool HandleDeviceChangeAndroid;

        protected override void Awake()
        {
            base.Awake();
            this.recorder = this.GetComponent<Recorder>();
            this.Logger.LogInfo("Subscribing to system (audio) changes.");
            this.photonMicChangeNotifier = Platform.CreateAudioInChangeNotifier(this.PhotonMicrophoneChangeDetected, this.Logger);
            if (this.photonMicChangeNotifier.IsSupported) // OSX, iOS, Switch
            {
                if (this.photonMicChangeNotifier.Error == null)
                {
                    this.Logger.LogInfo("Subscribed to audio in change notifications via Photon plugin.");
                }
                else
                {
                    this.Logger.LogError("Error creating instance of photonMicChangeNotifier: {0}", this.photonMicChangeNotifier.Error);
                }
            }
            else
            {
                this.Logger.LogInfo("Skipped subscribing to audio change notifications via Photon's AudioInChangeNotifier as not supported on current platform: {0}", Application.platform);
                // TODO: according to documentation, OnAudioConfigurationChanged fires on output device change, so in theory it will not work if only a microphone is added or removed.
                AudioSettings.OnAudioConfigurationChanged += this.OnAudioConfigChanged;
                this.Logger.LogInfo("Subscribed to audio configuration changes via Unity OnAudioConfigurationChanged callback.");
            }
        }

        private void OnDestroy()
        {
            if (this.photonMicChangeNotifier != null)
            {
                this.photonMicChangeNotifier.Dispose();
                this.photonMicChangeNotifier = null;
                this.Logger.LogInfo("Unsubscribed from audio in change notifications via Photon plugin.");
            }
            AudioSettings.OnAudioConfigurationChanged -= this.OnAudioConfigChanged;
            this.Logger.LogInfo("Unsubscribed from audio in change notifications via Unity OnAudioConfigurationChanged callback.");
        }

        private void PhotonMicrophoneChangeDetected()
        {
            this.Logger.LogInfo("Microphones change detected by Photon native plugin.");
            this.OnDeviceChange();
        }

        private void OnDeviceChange()
        {
            bool handle = false;
            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    handle = this.HandleDeviceChangeIOS;
                    break;
                case RuntimePlatform.Android:
                    handle = this.HandleDeviceChangeAndroid;
                    break;
                default:
                    handle = this.HandleDeviceChange;
                    break;
            }
            if (handle)
            {
                this.recorder.MicrophoneDeviceChangeDetected();
                this.Logger.LogInfo("Device change detected and the recording will be restarted.");
            }
            else
            {
                this.Logger.LogInfo("Device change detected but its handling is disabled.");
            }
        }

        private void OnAudioConfigChanged(bool deviceWasChanged)
        {
            this.Logger.LogInfo("OnAudioConfigurationChanged: {0}", deviceWasChanged ? "Device was changed." : "AudioSettings.Reset was called.");
            this.OnDeviceChange();
        }
    }
}