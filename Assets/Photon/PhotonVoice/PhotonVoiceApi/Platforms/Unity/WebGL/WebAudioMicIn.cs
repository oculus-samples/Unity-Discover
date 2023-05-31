#if UNITY_WEBGL && UNITY_2021_2_OR_NEWER // requires ES6
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Photon.Voice.Unity
{
    public class WebAudioMicIn : IAudioPusher<float>
    {
        const string lib_name = "__Internal";

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebAudioMicIn_Start(int handle, Action<int, int, int, int> createCallbackStatic, Action<int, IntPtr, int> dataCallbackStatic, int callIntervalMs);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void PhotonVoice_WebAudioMicIn_Stop(int hanle);

        ILogger logger;
        int handle;

        static Dictionary<int, WebAudioMicIn> handles = new Dictionary<int, WebAudioMicIn>();
        static int handleCnt;

        [AOT.MonoPInvokeCallbackAttribute(typeof(Action))]
        static void createCallbackStatic(int handle, int err, int samplingRate, int channels)
        {
            handles[handle].createCallback(err, samplingRate, channels);
        }

        void createCallback(int err, int samplingRate, int channels)
        {
            if (err != 0)
            {
                Error = "Can't create MediaRecorder: " + err;
                logger.LogError("[PV] WebAudioMicIn: " + Error);
            }
            else
            {
                sourceSamplingRate = samplingRate;
                logger.LogInfo("[PV] WebAudioMicIn: microphone initialized, handle = {0}, frequency = {1}, channels = {2}", handle, samplingRate, channels);
            }
        }

        [AOT.MonoPInvokeCallbackAttribute(typeof(Action<int, IntPtr, int>))]
        static void dataCallbackStatic(int handle, IntPtr p, int countFloat)
        {
            handles[handle].dataCallback(p, countFloat);
        }

        void dataCallback(IntPtr p, int countFloat)
        {
            if (pushCallback != null)
            {
                if (bufSource == null || bufSource.Length < countFloat)
                {
                    bufSource = new float[countFloat];
                    bufTarget = new float[countFloat * SamplingRate / sourceSamplingRate];
                }
                Marshal.Copy(p, bufSource, 0, countFloat);
                var countTarget = countFloat * SamplingRate / sourceSamplingRate;
                AudioUtil.Resample<float>(bufSource, 0, countFloat, bufTarget, 0, countTarget, 1);
                pushCallback(bufTarget);
            }
        }

        // WebAudio audio source sampling rate and channels count are not known at creation time due to asynchronicity.
        // To make SamplingRate and Channels properties available right after the creation, WebAudioMicIn returns suggested parameters
        // instead of what WebAudio source actually produces. The audio stream is resampled.
        // Make sure that suggestedFrequency is equal to encoder frequency to avoid double reasampling.
        public WebAudioMicIn(int suggestedFrequency, int suggestedChannels, ILogger logger)
        {
            this.SamplingRate = suggestedFrequency;
            // only mono is supported for now
            this.Channels = 1;
            this.logger = logger;
            handleCnt++;
            this.handle = handleCnt;
            handles[handle] = this;
            PhotonVoice_WebAudioMicIn_Start(handle, createCallbackStatic, dataCallbackStatic, 30);
        }

        private float[] bufSource;
        private float[] bufTarget;
        private int sourceSamplingRate;
        private Action<float[]> pushCallback;

        public void SetCallback(Action<float[]> callback, ObjectFactory<float[], int> bufferFactory)
        {
            this.pushCallback = callback;
        }

        bool disposed;
        public void Dispose()
        {
            lock (this) // Dispose() can be called twice
            {
                if (!disposed)
                {
                    PhotonVoice_WebAudioMicIn_Stop(handle);
                    disposed = true;
                }
            }
        }

        public string Error { get; private set; }

        public int SamplingRate { get; }

        public int Channels { get; }

    }
}
#endif
