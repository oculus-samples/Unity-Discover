namespace Photon.Voice.Unity.UtilityScripts
{
    public class MicAmplifierShort : IProcessor<short>
    {
        public float AmplificationFactor { get; set; }

        public bool Disabled { get; set; }

        public MicAmplifierShort(float amplificationFactor)
        {
            this.AmplificationFactor = amplificationFactor;
        }

        public short[] Process(short[] buf)
        {
            if (this.Disabled)
            {
                return buf;
            }
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = (short)(buf[i] * this.AmplificationFactor);
            }
            return buf;
        }

        public void Dispose()
        {

        }
    }
}