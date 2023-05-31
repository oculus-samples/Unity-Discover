#if PHOTON_VOICE_FMOD_ENABLE
using UnityEngine;

namespace Photon.Voice.Unity.FMOD
{
    [RequireComponent(typeof(Recorder))]
    [AddComponentMenu("Photon Voice/FMOD/FMOD Recorder Setup")]
    public class FMODRecorderSetup : VoiceComponent
    {
        protected override void Awake()
        {
            base.Awake();
            var recorder = this.GetComponent<Recorder>();
            recorder.SourceType = Recorder.InputSourceType.Factory;
            recorder.InputFactory = () =>
            {
                this.Logger.LogInfo("Setting recorder's source to FMOD factory with device={0}", recorder.MicrophoneDevice);
                return new Voice.FMOD.AudioInReader<short>(FMODUnity.RuntimeManager.CoreSystem, recorder.MicrophoneDevice.IDInt, (int)recorder.SamplingRate, this.Logger);
            };
        }
    }
}
#endif