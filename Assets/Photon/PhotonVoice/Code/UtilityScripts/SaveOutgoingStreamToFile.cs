namespace Photon.Voice.Unity.UtilityScripts
{
    using UnityEngine;
    using System.IO;

    [RequireComponent(typeof(Recorder))]
    [DisallowMultipleComponent]
    public class SaveOutgoingStreamToFile : VoiceComponent
    {
        private WaveWriter wavWriter;

        private void PhotonVoiceCreated(PhotonVoiceCreatedParams photonVoiceCreatedParams)
        {
            VoiceInfo voiceInfo = photonVoiceCreatedParams.Voice.Info;
            string filePath = this.GetFilePath();

            if (photonVoiceCreatedParams.Voice is LocalVoiceAudioFloat)
            {
                this.wavWriter = new WaveWriter(filePath, voiceInfo.SamplingRate, 32, voiceInfo.Channels);
                this.Logger.LogInfo("Outgoing 32 bit stream {0}, output file path: {1}", voiceInfo, filePath);
                LocalVoiceAudioFloat localVoiceAudioFloat = photonVoiceCreatedParams.Voice as LocalVoiceAudioFloat;
                localVoiceAudioFloat.AddPostProcessor(new OutgoingStreamSaverFloat(this.wavWriter));
            }
            else if (photonVoiceCreatedParams.Voice is LocalVoiceAudioShort)
            {
                this.wavWriter = new WaveWriter(filePath, voiceInfo.SamplingRate, 16, voiceInfo.Channels);
                this.Logger.LogInfo("Outgoing 16 bit stream {0}, output file path: {1}", voiceInfo, filePath);
                LocalVoiceAudioShort localVoiceAudioShort = photonVoiceCreatedParams.Voice as LocalVoiceAudioShort;
                localVoiceAudioShort.AddPostProcessor(new OutgoingStreamSaverShort(this.wavWriter));
            }
        }

        private string GetFilePath()
        {
            string filename = string.Format("out_{0}_{1}.wav", System.DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-ffff"), Random.Range(0, 1000));
            return Path.Combine(Application.persistentDataPath, filename);
        }

        private void PhotonVoiceRemoved()
        {
            this.wavWriter.Dispose();
            this.Logger.LogInfo("Recording stopped: Saving wav file.");
        }

        class OutgoingStreamSaverFloat : IProcessor<float>
        {
            private WaveWriter wavWriter;

            public OutgoingStreamSaverFloat(WaveWriter waveWriter)
            {
                this.wavWriter = waveWriter;
            }

            public float[] Process(float[] buf)
            {
                this.wavWriter.WriteSamples(buf, 0, buf.Length);
                return buf;
            }

            public void Dispose()
            {
                this.wavWriter.Dispose();
            }
        }

        class OutgoingStreamSaverShort : IProcessor<short>
        {
            private WaveWriter wavWriter;

            public OutgoingStreamSaverShort(WaveWriter waveWriter)
            {
                this.wavWriter = waveWriter;
            }

            public short[] Process(short[] buf)
            {
                for (int i = 0; i < buf.Length; i++)
                {
                    this.wavWriter.Write(buf[i]);
                }
                return buf;
            }

            public void Dispose()
            {
                this.wavWriter.Dispose();
            }
        }
    }
}
