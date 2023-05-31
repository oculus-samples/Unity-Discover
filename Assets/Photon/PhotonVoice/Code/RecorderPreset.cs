using System;
using UnityEngine;
using static Photon.Voice.Unity.Recorder;

namespace Photon.Voice.Unity
{
    [AddComponentMenu("Photon Voice/Recorder Preset")]
    public class RecorderPreset : VoiceComponent
    {
        [Serializable]
        public struct DSP
        {
            [Tooltip("Acoustic Echo Cancellation")]
            public bool AEC;
            [Tooltip("Voice Activity Detection")]
            public bool VAD;
        }

        [Tooltip("On which platform to apply the filter.")]
        public RuntimePlatform Platform;
        [Tooltip("Which microphone API to use when the Source is set to Microphone.")]
        [Header("Overrides:")]
        public MicType MicrophoneType;
        [Tooltip("Enable WebRtcAudioDsp component.")]
        public bool DSPEnabled;
        public DSP DSPSettings;

        protected override void Awake()
        {
            base.Awake();
            if (enabled)
            {
                var rec = GetComponent<Recorder>();
                var dsp = GetComponent<WebRtcAudioDsp>();
                if (Application.platform == Platform)
                {
                    if (rec == null)
                    {
                        Logger.LogError("Can't find Recorder component");
                    }
                    else
                    {
                        Logger.LogInfo("Updating from preset for platform '{0}': Microphone Type = {1}, DSP Enabled = {2}", Application.platform, MicrophoneType, DSPEnabled);
                        rec.MicrophoneType = MicrophoneType;
                        if (dsp == null)
                        {
                            Logger.LogError("Can't find WebRtcAudioDsp component");
                        }
                        else
                        {
                            dsp.enabled = DSPEnabled;
                            if (DSPEnabled)
                            {
                                Logger.LogInfo("Updating from preset for platform '{0}': DSP.AEC = {1}, DSP.VAD = {2}", Application.platform, DSPSettings.AEC, DSPSettings.VAD);
                                dsp.AEC = DSPSettings.AEC;
                                dsp.VAD = DSPSettings.VAD;
                            }
                        }
                    }
                }
            }
        }

        void Update()
        {
        }
    }
}
