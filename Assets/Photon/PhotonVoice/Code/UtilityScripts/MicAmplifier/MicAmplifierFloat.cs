namespace Photon.Voice.Unity.UtilityScripts
{

    public class MicAmplifierFloat : IProcessor<float>
    {
        public float AmplificationFactor { get; set; }

        public bool Disabled { get; set; }

        public MicAmplifierFloat(float amplificationFactor)
        {
            this.AmplificationFactor = amplificationFactor;
        }

        public float[] Process(float[] buf)
        {
            if (this.Disabled)
            {
                return buf;
            }
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] *= this.AmplificationFactor;
            }
            return buf;
        }

        public void Dispose()
        {
        }
    }
}