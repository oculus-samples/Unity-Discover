#if PHOTON_VOICE_FMOD_ENABLE
using UnityEngine;

namespace Photon.Voice.Unity.FMOD
{
    [AddComponentMenu("Photon Voice/FMOD/Speaker FMOD")]
    public class SpeakerFMOD : Speaker
    {
        [SerializeField]
        private bool useEvent;
        [SerializeField]
        private FMODUnity.EventReference eventReference; // todo: expose as a property to make it possible to switch event at runtime & re initialize Speaker

        protected override IAudioOut<float> CreateAudioOut()
        {
            if (this.useEvent)
            {
                var instance = FMODUnity.RuntimeManager.CreateInstance(this.eventReference);
                return new Voice.FMOD.AudioOutEvent<float>(FMODUnity.RuntimeManager.CoreSystem, instance, this.playDelayConfig, this.Logger, string.Empty, true);
            }
            else
            {
                return new Voice.FMOD.AudioOut<float>(FMODUnity.RuntimeManager.CoreSystem, this.playDelayConfig, this.Logger, string.Empty, true);
            }
        }
    }
}
#endif