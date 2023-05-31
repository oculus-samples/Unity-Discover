using System;
using UnityEngine;

namespace Oculus.Avatar2
{
    public static class PlatformHelperUtils
    {
        private const string DefaultScope = "ovrAvatar2.platformUtils";

        public static class AndroidSysProperties
        {
            public static readonly string ExperimentalFeatures = "persist.avatar.perf_test.expfeatures";
        }

        public static string GetAndroidSysProp(string propName, string logScope = DefaultScope)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var sysprops = new AndroidJavaClass("android.os.SystemProperties");
                var val = sysprops.CallStatic<string>("get", propName);
                OvrAvatarLog.LogInfo($"System property {propName} = {val}", logScope);
                return val;
            }
            catch (System.Exception e)
            {
                OvrAvatarLog.LogError($"An exception occured while reading system property: {e}", logScope);
            }
#endif
            return string.Empty;
        }

        public static int GetAndroidIntSysProp(string propName, int defaultValue, string logScope = DefaultScope)
        {
            var value = defaultValue;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                value = int.Parse(GetAndroidSysProp(propName, logScope));
            }
            catch (System.FormatException)
            {
                OvrAvatarLog.LogError($"Unable to parse {propName}", logScope);
            }
#endif
            return value;
        }

    }
}
