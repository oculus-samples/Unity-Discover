using System.Linq;

namespace Photon.Voice.Unity
{
#if UNITY_WEBGL
    using System;
#endif
    using UnityEngine;

    /// <summary>A wrapper around UnityEngine.Microphone to be able to safely use Microphone and compile for WebGL.</summary>
    public static class UnityMicrophone
    {
#if UNITY_WEBGL
        private const string webglIsnotSupported = "Unity Microphone not supported on WebGL";
        private static readonly string[] _devices = new string[0];
#endif

        public static string[] devices
        {
            get
            {
#if UNITY_WEBGL
                return _devices;
#else
                return Microphone.devices;
#endif
            }
        }

        public static void End(string deviceName)
        {
#if UNITY_WEBGL
            throw new NotImplementedException(webglIsnotSupported);
#else
            Microphone.End(deviceName);
#endif
        }

        public static void GetDeviceCaps(string deviceName, out int minFreq, out int maxFreq)
        {
#if UNITY_WEBGL
            throw new NotImplementedException(webglIsnotSupported);
#else
            Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
#endif
        }

        public static int GetPosition(string deviceName)
        {
#if UNITY_WEBGL
            throw new NotImplementedException(webglIsnotSupported);
#else
            return Microphone.GetPosition(deviceName);
#endif
        }

        public static bool IsRecording(string deviceName)
        {
#if UNITY_WEBGL
            return false;
#else
            return Microphone.IsRecording(deviceName);
#endif
        }

        public static AudioClip Start(string deviceName, bool loop, int lengthSec, int frequency)
        {
#if UNITY_WEBGL
            throw new NotImplementedException(webglIsnotSupported);
#else
            return Microphone.Start(deviceName, loop, lengthSec, frequency);
#endif
        }

        public static string CheckDevice(Voice.ILogger logger, string logPref, string device, int suggestedFrequency, out int frequency)
        {
#if UNITY_WEBGL
            logger.LogError(logPref + webglIsnotSupported);
            frequency = 0;
            return webglIsnotSupported;
#else
            if (Microphone.devices.Length < 1)
            {
                var err = "No microphones found (Microphone.devices is empty)";
                logger.LogError(logPref + err);
                frequency = 0;
                return err;
            }
            if (!string.IsNullOrEmpty(device) && !Microphone.devices.Contains(device))
            {
                var err = string.Format("[PV] MicWrapper: \"{0}\" is not a valid Unity microphone device, falling back to default one", device);
                logger.LogError(logPref + err);
                frequency = 0;
                return err;
            }
            int minFreq;
            int maxFreq;
            logger.LogInfo("[PV] MicWrapper: initializing microphone '{0}', suggested frequency = {1}).", device, suggestedFrequency);
            Microphone.GetDeviceCaps(device, out minFreq, out maxFreq);
            frequency = suggestedFrequency;

            //        minFreq = maxFreq = 44100; // test like android client
            if (Application.platform == RuntimePlatform.PS4 || Application.platform == RuntimePlatform.PS5)
            {
                if (suggestedFrequency != minFreq && suggestedFrequency != maxFreq)
                {
                    int setFrequency = suggestedFrequency <= minFreq ? minFreq : maxFreq;
                    logger.LogWarning(logPref + "microphone does not support suggested frequency {0} (supported frequencies are: {1} and {2}). Setting to {3}",
                        suggestedFrequency, minFreq, maxFreq, setFrequency);
                    frequency = setFrequency;
                }
            }
            else
            {
                if (suggestedFrequency < minFreq || maxFreq != 0 && suggestedFrequency > maxFreq)
                {
                    logger.LogWarning(logPref + "microphone does not support suggested frequency {0} (min: {1}, max: {2}). Setting to {2}",
                        suggestedFrequency, minFreq, maxFreq);
                    frequency = maxFreq;
                }
            }

            return null;
#endif
        }
    }
}