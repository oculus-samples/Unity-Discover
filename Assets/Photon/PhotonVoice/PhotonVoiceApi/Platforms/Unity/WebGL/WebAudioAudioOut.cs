#if UNITY_WEBGL && UNITY_2021_2_OR_NEWER // requires ES6
using System.Runtime.InteropServices;

namespace Photon.Voice.Unity
{
    public class WebAudioAudioOut : AudioOutDelayControl<float>
    {
        const string lib_name = "__Internal";
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebAudioAudioOut_ResumeAudioContext();

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebAudioAudioOut_Start(int handle, int frequency, int channels, int bufferSamples, double spatial);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebAudioAudioOut_GetOutPos(int handle);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void PhotonVoice_WebAudioAudioOut_Write(int handle, float[] data, int dataLength, int offsetSamples);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebAudioAudioOut_SetVolume(int handle, double x);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebAudioAudioOut_SetSpatialBlend(int handle, double x);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebAudioAudioOut_SetListenerPosition(int handle, double x, double y, double z);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebAudioAudioOut_SetListenerOrientation(int handle, double fx, double fy, double fz, double ux, double uy, double uz);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebAudioAudioOut_SetPosition(int handle, double x, double y, double z);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebAudioAudioOut_Stop(int handle);

        int handle;
        static int handleCnt;
        private double spatialBlend;
        private bool spatialBlendDynamic;

        // WebAudio graph is optimized when initialized for spatialBlend = 0 and spatialBlend = 1. In these cases, spatialBlend is constant.
        // Set AudioSource.spatialBlend to the value between 0 and 1 and recreate WebAudioAudioOut (e.g. call Speaker.RestartPlayback()) to be able adjust spatialBlend dynamically.
        // Set AudioSource.spatialBlend to 0 or 1 and recreate WebAudioAudioOut to swith to an optimized graph.
        public WebAudioAudioOut(PlayDelayConfig playDelayConfig, double spatialBlend, ILogger logger, string logPrefix, bool debugInfo)
        : base(true, playDelayConfig, logger, "[PV] [Unity] WebAudioAudioOut" + (logPrefix == "" ? "" : " " + logPrefix), debugInfo)
        {
            this.spatialBlend = spatialBlend;
            spatialBlendDynamic = spatialBlend > 0 && spatialBlend < 1;
        }

        // not part of interface
        public string Error { get; private set; }

        public override long OutPos => PhotonVoice_WebAudioAudioOut_GetOutPos(handle);

        public override void OutCreate(int frequency, int channels, int bufferSamples)
        {
        }

        public static void ResumeAudioContext()
        {
            PhotonVoice_WebAudioAudioOut_ResumeAudioContext();
        }
        
        public override void OutStart()
        {
            handleCnt++;
            this.handle = handleCnt;
            var err = PhotonVoice_WebAudioAudioOut_Start(handle, frequency, channels, bufferSamples, spatialBlend);

            if (err != 0)
            {
                Error = "Can't initialize: handle = " + handle + ", error = " + err;
                logger.LogError("[PV] WebAudioAudioOut: " + Error);
            }
            else
            {
                logger.LogInfo("[PV] WebAudioAudioOut initialized, handle = {0}, frequency = {1}, channels = {2}, bufferSamples = {3}", handle, frequency, channels, bufferSamples);
            }
        }

        public override void OutWrite(float[] data, int offsetSamples)
        {
            if (Error != null)
            {
                return;
            }
            PhotonVoice_WebAudioAudioOut_Write(handle, data, data.Length, offsetSamples);
        }

        public override void Stop()
        {
            base.Stop();
            PhotonVoice_WebAudioAudioOut_Stop(handle);
        }

        private double volume = 1; // default GainNode.gain
        public void SetVolume(double x)
        {
            if (volume != x)
            {
                if (PhotonVoice_WebAudioAudioOut_SetVolume(handle, x) == 0)
                {
                    volume = x;
                }
            }
        }

        public void SetSpatialBlend(double x)
        {
            if (spatialBlendDynamic && spatialBlend != x)
            {
                if (PhotonVoice_WebAudioAudioOut_SetSpatialBlend(handle, x) == 0)
                {
                    spatialBlend = x;
                }
            }
        }

        private double[] listPos = new double[] { 0, 0, 0 };
        public void SetListenerPosition(double x, double y, double z)
        {
            if (listPos[0] == x &&
                listPos[1] == y &&
                listPos[2] == z) return;

            if (PhotonVoice_WebAudioAudioOut_SetListenerPosition(handle, x, y, z) == 0)
            {
                listPos[0] = x;
                listPos[1] = y;
                listPos[2] = z;
            }
        }

        private double[] listOrient = new double[] { 0, 0, -1, 0, 1, 0 }; // defaults from AudioListener API
        public void SetListenerOrientation(double fx, double fy, double fz, double ux, double uy, double uz)
        {
            if (listOrient[0] == fx &&
                listOrient[1] == fy &&
                listOrient[2] == fz &&
                listOrient[3] == ux &&
                listOrient[4] == uy &&
                listOrient[5] == uz) return;

            if (PhotonVoice_WebAudioAudioOut_SetListenerOrientation(handle, fx, fy, fz, ux, uy, uz) == 0)
            {
                listOrient[0] = fx;
                listOrient[1] = fy;
                listOrient[2] = fz;
                listOrient[3] = ux;
                listOrient[4] = uy;
                listOrient[5] = uz;
            }
        }

        private double[] pos = new double[] { 0, 0, 0 };
        public void SetPosition(double x, double y, double z)
        {
            if (pos[0] == x &&
                pos[1] == y &&
                pos[2] == z) return;

            if (PhotonVoice_WebAudioAudioOut_SetPosition(handle, x, y, z) == 0)
            {
                pos[0] = x;
                pos[1] = y;
                pos[2] = z;
            }
        }
    }
}
#endif