#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_ANDROID || UNITY_WSA
#define PLATFORM_IS_SUPPORTED
#endif

using System.Collections.Generic;
using UnityEngine;

namespace Photon.Voice.Unity
{
    [RequireComponent(typeof(Recorder))]
    [AddComponentMenu("Photon Voice/WebRTC Audio DSP")]
    [DisallowMultipleComponent]
    // Set enabled = false to prevent the processor from being added to the audio pipelene during voice creation.
    public class WebRtcAudioDsp : VoiceComponent
    {
        #region Private Fields

        [SerializeField]
        private bool aec = true;

        [SerializeField]
        private bool aecHighPass;

        [SerializeField]
        private bool agc = true;

        [SerializeField]
        [Range(0, 90)]
        private int agcCompressionGain = 9;

        [SerializeField]
        [Range(0, 31)]
        private int agcTargetLevel = 3;

        [SerializeField]
        private bool vad = true;

        [SerializeField]
        private bool highPass;

        // do not serialize, may be set to true only in runtime
        private bool bypass;

        [SerializeField]
        private bool noiseSuppression = true;

        [SerializeField]
        private int reverseStreamDelayMs = 120;

        private int reverseChannels;
        private WebRTCAudioProcessor proc;

        private static readonly Dictionary<AudioSpeakerMode, int> channelsMap = new Dictionary<AudioSpeakerMode, int>
        {
            #if !UNITY_2019_2_OR_NEWER
            {AudioSpeakerMode.Raw, 0},
            #endif
            {AudioSpeakerMode.Mono, 1},
            {AudioSpeakerMode.Stereo, 2},
            {AudioSpeakerMode.Quad, 4},
            {AudioSpeakerMode.Surround, 5},
            {AudioSpeakerMode.Mode5point1, 6},
            {AudioSpeakerMode.Mode7point1, 8},
            {AudioSpeakerMode.Prologic, 2}
        };

        private LocalVoiceAudioShort localVoice;
        private int outputSampleRate;

        #endregion

        #region Properties

        public bool AEC
        {
            get { return this.aec; }
            set
            {
                if (value != this.aec)
                {
                    this.aec = value;
                    this.applyToProc();
                }
            }
        }

        public bool AecHighPass
        {
            get { return this.aecHighPass; }
            set
            {
                if (value != this.aecHighPass)
                {
                    this.aecHighPass = value;
                    this.applyToProc();
                }
            }
        }

        public int ReverseStreamDelayMs
        {
            get { return this.reverseStreamDelayMs; }
            set
            {
                if (value != this.reverseStreamDelayMs)
                {
                    this.reverseStreamDelayMs = value;
                    this.applyToProc();
                }
            }
        }

        public bool NoiseSuppression
        {
            get { return this.noiseSuppression; }
            set
            {
                if (value != this.noiseSuppression)
                {
                    this.noiseSuppression = value;
                    this.applyToProc();
                    Restart();
                }
            }
        }

        public bool HighPass
        {
            get { return this.highPass; }
            set
            {
                if (value != this.highPass)
                {
                    this.highPass = value;
                    this.applyToProc();
                }
            }
        }

        public bool Bypass
        {
            get { return this.bypass; }
            set
            {
                if (value != this.bypass)
                {
                    this.bypass = value;
                    this.applyToProc();
                }
            }
        }

        public bool AGC
        {
            get { return this.agc; }
            set
            {
                if (value != this.agc)
                {
                    this.agc = value;
                    this.applyToProc();
                }
            }
        }

        public int AgcCompressionGain
        {
            get { return this.agcCompressionGain; }
            set
            {
                if (value != this.agcCompressionGain)
                {
                    this.agcCompressionGain = value;
                    this.applyToProc();
                }
            }
        }

        public int AgcTargetLevel
        {
            get { return this.agcTargetLevel; }
            set
            {
                if (value != this.agcTargetLevel)
                {
                    this.agcTargetLevel = value;
                    this.applyToProc();
                }
            }
        }

        public bool VAD
        {
            get { return this.vad; }
            set
            {
                if (value != this.vad)
                {
                    this.vad = value;
                    this.applyToProc();
                }
            }
        }

        #endregion

        #region Private Methods

        protected override void Awake()
        {
            base.Awake();
            if (this.IsSupported)
            {
                AudioSettings.OnAudioConfigurationChanged += this.OnAudioConfigurationChanged;
            }
            else
            {
                this.Logger.LogWarning("WebRtcAudioDsp is not supported on this platform {0}. The component will be disabled.", Application.platform);
            }
        }

        // required for the MonoBehaviour to have the 'enabled' checkbox
        private void Start()
        {
        }

        public bool IsSupported =>
#if PLATFORM_IS_SUPPORTED
        true;
#else
        false;
#endif
        public void AdjustVoiceInfo(ref VoiceInfo voiceInfo, ref AudioSampleType st)
        {
            if (IsSupported && enabled)
            {
                st = AudioSampleType.Short;
                this.Logger.LogInfo("Type Conversion set to Short. Audio samples will be converted if source samples types differ.");
                // WebRTC DSP supports 8000 16000 [32000] 48000 Hz
                // TODO: correct, opus-independent parameters matching implementation.
                // The code below relies on the assumption that voiceInfo.SamplingRate is from POpusCodec.Enums.SamplingRate set.
                switch (voiceInfo.SamplingRate)
                {
                    case 12000:
                        this.Logger.LogWarning("Sampling rate requested (12kHz) is not supported by WebRtcAudioDsp, switching to the closest supported value: 16kHz.");
                        voiceInfo.SamplingRate = 16000;
                        break;
                    case 24000:
                        this.Logger.LogWarning("Sampling rate requested (24kHz) is not supported by WebRtcAudioDsp, switching to the closest supported value: 48kHz.");
                        voiceInfo.SamplingRate = 48000;
                        break;
                }
                if (voiceInfo.FrameDurationUs < 10000)
                {
                    this.Logger.LogWarning("Frame duration requested ({0}ms) is not supported by WebRtcAudioDsp (it needs to be N x 10ms), switching to the closest supported value: 10ms.", (int)voiceInfo.FrameDurationUs / 1000);
                    voiceInfo.FrameDurationUs = 10000;
                }
            }
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (this.outputSampleRate != AudioSettings.outputSampleRate)
            {
                this.Logger.LogInfo("AudioConfigChange: outputSampleRate from {0} to {1}. WebRtcAudioDsp will be restarted.", this.outputSampleRate, AudioSettings.outputSampleRate);
                this.outputSampleRate = AudioSettings.outputSampleRate;
                this.Restart();
            }
            if (this.reverseChannels != channelsMap[AudioSettings.speakerMode])
            {
                this.Logger.LogInfo("AudioConfigChange: speakerMode channels from {0} to {1}. WebRtcAudioDsp will be restarted.", this.reverseChannels, channelsMap[AudioSettings.speakerMode]);
                this.reverseChannels = channelsMap[AudioSettings.speakerMode];
                this.Restart();
            }
        }

        // triggered by OnAudioFilterRead which is called on a different thread from the main thread (namely the audio thread)
        // so calling into many Unity functions from this function is not allowed (if you try, a warning shows up at run time)
        private void OnAudioOutFrameFloat(float[] data, int outChannels)
        {
            if (outChannels != this.reverseChannels)
            {
                this.Logger.LogWarning("OnAudioOutFrame channel count {0} != initialized {1}.", outChannels, this.reverseChannels);
            }
            else
            {
                this.proc.OnAudioOutFrameFloat(data);
            }
        }

        // Unity message sent by Recorder
        private void PhotonVoiceCreated(PhotonVoiceCreatedParams p)
        {
            if (this.IsSupported && this.enabled)
            {
                if (p.Voice.Info.Channels != 1)
                {
                    this.Logger.LogError("Only mono audio signals supported. WebRtcAudioDsp component will be disabled.");
                    return;
                }
                if (p.Voice is LocalVoiceAudioShort voice)
                {
                    this.StartProc(voice);
                    this.localVoice = voice;
                }
                else
                {
                    this.Logger.LogError("Only short audio voice supported. WebRtcAudioDsp component will be disabled.");
                }
            }
        }

        // Unity message sent by Recorder
        private void PhotonVoiceRemoved()
        {
            this.StopProc(this.localVoice);
            this.localVoice = null;
        }

        private void OnDestroy()
        {
            this.StopProc(this.localVoice);
            AudioSettings.OnAudioConfigurationChanged -= this.OnAudioConfigurationChanged;
        }

        private void StartProc(LocalVoiceAudioShort v)
        {
            this.Logger.LogInfo("Start");
            this.reverseChannels = channelsMap[AudioSettings.speakerMode];
            this.outputSampleRate = AudioSettings.outputSampleRate;
            this.proc = new WebRTCAudioProcessor(this.Logger, v.Info.FrameSize, v.Info.SamplingRate, v.Info.Channels, this.outputSampleRate, this.reverseChannels);
            this.applyToProc();
            v.AddPostProcessor(this.proc);
        }

        private void StopProc(LocalVoiceAudioShort v)
        {
            this.Logger.LogInfo("Stop");
            this.setOutputListener(false);
            if (proc != null)
            {
                this.proc.Dispose();
            }
            if (v != null)
            {
                v.RemoveProcessor(this.proc);
            }
            // TODO: remove processor from local voice
        }

        private void Restart()
        {
            this.Logger.LogInfo("Restart");
            this.StopProc(this.localVoice);
            if (this.localVoice != null)
            {
                this.StartProc(this.localVoice);
            }
        }

        private void setOutputListener(bool set)
        {
            var audioListener = FindObjectOfType<AudioListener>();
            if (audioListener != null)
            {
                var ac = audioListener.gameObject.GetComponent<AudioOutCapture>();
                if (ac != null)
                {
                    ac.OnAudioFrame -= OnAudioOutFrameFloat;
                }
                if (set)
                {
                    if (ac == null)
                    {
                        ac = audioListener.gameObject.AddComponent<AudioOutCapture>();
                    }

                    ac.OnAudioFrame += OnAudioOutFrameFloat;
                }
            }
        }

        private void applyToProc()
        {
            if (proc != null)
            {
                proc.AEC = this.aec;
                proc.AECMobile = this.aec && Application.isMobilePlatform;
                setOutputListener(this.aec);
                proc.AECStreamDelayMs = this.reverseStreamDelayMs;
                proc.AECHighPass = this.aecHighPass;
                proc.HighPass = this.highPass;
                proc.NoiseSuppression = this.noiseSuppression;
                proc.AGC = this.agc;
                proc.AGCCompressionGain = this.agcCompressionGain;
                proc.AGCTargetLevel = this.agcTargetLevel;
                //proc.AGC2 = AGC2;
                proc.VAD = VAD;
                proc.Bypass = Bypass;
            }
        }
        #endregion
    }
}