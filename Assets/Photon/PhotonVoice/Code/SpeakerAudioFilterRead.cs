using UnityEngine;

namespace Photon.Voice.Unity
{
    // Sends audio to Unity in OnAudioFilterRead() call.
    // TODO: in case we will introduce factories as in Speaker, it makes sense to use an interface extended from IAudioOut<T> with Read() methos added for 'outBuffer' instead of concrete AudioSyncBuffer<float> type
    // This will reqiure Core modifications since AudioSyncBuffer<float> is defined there
    [AddComponentMenu("Photon Voice/Speaker AudioFilterRead")]
    public class SpeakerAudioFilterRead : Speaker
    {
        // points to the same object as audioOutput but is of extended type
        private AudioSyncBuffer<float> outBuffer;
        private int outputSampleRate;

        protected override IAudioOut<float> CreateAudioOut()
        {
            // default implementation
            this.outBuffer = new AudioSyncBuffer<float>(this.playDelayConfig.Low, this.Logger, string.Empty, true);
            this.outputSampleRate = AudioSettings.outputSampleRate;
            return this.outBuffer;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (this.outBuffer != null)
            {
                this.outBuffer.Read(data, channels, this.outputSampleRate);
            }
        }
    }
}