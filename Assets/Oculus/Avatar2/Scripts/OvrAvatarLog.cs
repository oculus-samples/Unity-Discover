// If logs enabled, always enable special case handling for now
#define OVRAVATAR_HANDLE_SPECIAL_CASE

// Assert throw is only active when every mechanism for asserts is enabled
// By default, all of these conditions fail
#if OVRAVATAR_ASSERT_ENABLE_CONDITIONAL && OVRAVATAR_ASSERT_FORCE_ENABLE && UNITY_ASSERTIONS
#define OVRAVATAR_ASSERT_THROW
#endif

using System;
using UnityEngine;

using Conditional = System.Diagnostics.ConditionalAttribute;

namespace Oculus.Avatar2
{

    public static class OvrAvatarLog
    {
        public enum ELogLevel
        {
            Verbose,
            Debug,
            Info,
            Warn,
            Error
        }

        public static ELogLevel logLevel = ELogLevel.Info;

        public delegate void LogDelegate(ELogLevel level, string scope, string msg);

        public static event LogDelegate CustomLogger;

        public const bool enabled =
#if !OVRAVATAR_LOG_ENABLE_CONDITIONAL || OVRAVATAR_LOG_FORCE_ENABLE
            true;
#else
            false;
#endif

        // When Logging Conditional is enabled, logs will default to off
        // - this enables them when conditionals are active
        public const string OVRAVATAR_LOG_FORCE_ENABLE = "OVRAVATAR_LOG_FORCE_ENABLE";
        // Analagous to `OVRAVATAR_LOG_FORCE_ENABLE` but for asserts
        public const string OVRAVATAR_ASSERT_FORCE_ENABLE = "OVRAVATAR_ASSERT_FORCE_ENABLE";

        internal static ELogLevel GetLogLevel(CAPI.ovrAvatar2LogLevel priority)
        {
            switch (priority)
            {
                case CAPI.ovrAvatar2LogLevel.Unknown:
                    return ELogLevel.Info;
                case CAPI.ovrAvatar2LogLevel.Default:
                    return ELogLevel.Info;
                case CAPI.ovrAvatar2LogLevel.Verbose:
                    return ELogLevel.Verbose;
                case CAPI.ovrAvatar2LogLevel.Debug:
                    return ELogLevel.Debug;
                case CAPI.ovrAvatar2LogLevel.Info:
                    return ELogLevel.Info;
                case CAPI.ovrAvatar2LogLevel.Warn:
                    return ELogLevel.Warn;
                case CAPI.ovrAvatar2LogLevel.Error:
                    return ELogLevel.Error;
                case CAPI.ovrAvatar2LogLevel.Fatal:
                    return ELogLevel.Error;
                case CAPI.ovrAvatar2LogLevel.Silent:
                    return ELogLevel.Verbose;
                default:
                    throw new ArgumentOutOfRangeException(nameof(priority), priority, null);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(CAPI.LoggingDelegate))]
        internal static void LogCallBack(CAPI.ovrAvatar2LogLevel priority, string msg, IntPtr context)
        {
            if (priority == CAPI.ovrAvatar2LogLevel.Silent) return;
            bool specialCase = false;
            HandleSpecialCaseLogs(msg, ref specialCase);
            if (!specialCase)
            {
                Log(GetLogLevel(priority), msg, "native");
            }
        }

        private static string GetScopePrefix(string scope)
        {
            return String.IsNullOrEmpty(scope) ? "[ovrAvatar2]" : $"[ovrAvatar2 {scope}]";
        }

#if OVRAVATAR_LOG_ENABLE_CONDITIONAL
        [Conditional(OVRAVATAR_LOG_FORCE_ENABLE)]
#endif
        public static void Log(ELogLevel level, string msg, string scope = "", UnityEngine.Object context = null)
        {
            if (CustomLogger != null)
            {
                CustomLogger(level, scope, msg);
            }
            else if (level >= logLevel)
            {
                string prefix = GetScopePrefix(scope);
                if (level >= ELogLevel.Error)
                {
                    Debug.LogError($"{prefix} {msg}", context);
                }
                else if (level >= ELogLevel.Warn)
                {
                    Debug.LogWarning($"{prefix} {msg}", context);
                }
                else if (level >= ELogLevel.Info)
                {
                    Debug.Log($"{prefix} {msg}", context);
                }
                else if (level >= ELogLevel.Debug)
                {
                    Debug.Log($"{prefix}[Debug] {msg}", context);
                }
                else
                {
                    Debug.Log($"{prefix}[Verbose] {msg}", context);
                }
            }
        }

        [Conditional(OVRAVATAR_LOG_FORCE_ENABLE)]
#if !OVRAVATAR_LOG_ENABLE_CONDITIONAL
         [Conditional("DEVELOPMENT_BUILD")
         , Conditional("UNITY_EDITOR")]
#endif
        public static void LogVerbose(string msg, string scope = "", UnityEngine.Object context = null)
        {
            Log(ELogLevel.Verbose, msg, scope, context);
        }

#if OVRAVATAR_LOG_ENABLE_CONDITIONAL
        [Conditional(OVRAVATAR_LOG_FORCE_ENABLE)]
#endif
        public static void LogDebug(string msg, string scope = "", UnityEngine.Object context = null)
        {
            Log(ELogLevel.Debug, msg, scope, context);
        }

#if OVRAVATAR_LOG_ENABLE_CONDITIONAL
        [Conditional(OVRAVATAR_LOG_FORCE_ENABLE)]
#endif
        public static void LogInfo(string msg, string scope = "", UnityEngine.Object context = null)
        {
            Log(ELogLevel.Info, msg, scope, context);
        }

#if OVRAVATAR_LOG_ENABLE_CONDITIONAL
        [Conditional(OVRAVATAR_LOG_FORCE_ENABLE)]
#endif
        public static void LogWarning(string msg, string scope = "", UnityEngine.Object context = null)
        {
            Log(ELogLevel.Warn, msg, scope, context);
        }

#if OVRAVATAR_LOG_ENABLE_CONDITIONAL
        [Conditional(OVRAVATAR_LOG_FORCE_ENABLE)]
#endif
        public static void LogError(string msg, string scope = "", UnityEngine.Object context = null)
        {
            Log(ELogLevel.Error, msg, scope, context);
        }

#if OVRAVATAR_LOG_ENABLE_CONDITIONAL
        [Conditional(OVRAVATAR_LOG_FORCE_ENABLE)]
#endif
        public static void LogException(string operationName, Exception exception, string scope = "", UnityEngine.Object context = null)
        {
            LogError($"Exception during {operationName} - {exception}", scope, context);
        }

        public const string DEFAULT_ASSERT_MESSAGE = "condition false";
        public const string DEFAULT_ASSERT_SCOPE = "OvrAssert";
#if OVRAVATAR_LOG_ENABLE_CONDITIONAL
        [Conditional(OVRAVATAR_LOG_FORCE_ENABLE)]
#endif
        public static void LogAssert(string message = DEFAULT_ASSERT_MESSAGE, string scope = DEFAULT_ASSERT_SCOPE, UnityEngine.Object context = null)
        {
            LogError($"Assertion failed: {message ?? DEFAULT_ASSERT_MESSAGE}", scope ?? DEFAULT_ASSERT_SCOPE, context);
            Debug.Assert(false, $"ASSERT FAILED - [ovrAvatar2 {scope}] - {message}", context);

            // Do not enable OVRAVATAR_ASSERT_THROW unless you want failure (ie: testing)
            // - This will lead to catastrophic failure in many places in some applications
            // -- In theory, those are the real logical failures.
#if OVRAVATAR_ASSERT_THROW
            throw new InvalidOperationException(message);
#endif
        }

#if OVRAVATAR_ASSERT_ENABLE_CONDITIONAL || !UNITY_ASSERTIONS
        [Conditional(OVRAVATAR_ASSERT_FORCE_ENABLE)]
#endif
        internal static void Assert(bool condition, string scope = DEFAULT_ASSERT_SCOPE, UnityEngine.Object context = null)
        {
            AssertConstMessage(condition, "condition false", scope, context);
        }

#if OVRAVATAR_ASSERT_ENABLE_CONDITIONAL || !UNITY_ASSERTIONS
        [Conditional(OVRAVATAR_ASSERT_FORCE_ENABLE)]
#endif
        internal static void AssertConstMessage(bool condition, string message = null, string scope = DEFAULT_ASSERT_SCOPE, UnityEngine.Object context = null)
        {
            if (!condition)
            {
                LogAssert(message, scope, context);
            }
        }

        public delegate string AssertStaticMessageBuilder();
#if OVRAVATAR_ASSERT_ENABLE_CONDITIONAL || !UNITY_ASSERTIONS
        [Conditional(OVRAVATAR_ASSERT_FORCE_ENABLE)]
#endif
        internal static void AssertStaticBuilder(bool condition, AssertStaticMessageBuilder builder, string scope = DEFAULT_ASSERT_SCOPE, UnityEngine.Object context = null)
        {
            if (!condition)
            {
                LogAssert(builder(), scope, context);
            }
        }

        public delegate string AssertMessageBuilder<T>(in T param);
#if OVRAVATAR_ASSERT_ENABLE_CONDITIONAL || !UNITY_ASSERTIONS
        [Conditional(OVRAVATAR_ASSERT_FORCE_ENABLE)]
#endif
        internal static void AssertParam<T>(bool condition, in T buildParams, AssertMessageBuilder<T> builder
            , string scope = DEFAULT_ASSERT_SCOPE, UnityEngine.Object context = null)
        {
            // Should be a static method, otherwise wrap in conditional to avoid alloc
            Debug.Assert(builder.Target == null, "Instanced builder passed to AssertParam");
            if (!condition)
            {
                LogAssert(builder(in buildParams), scope, context);
            }
        }

        public delegate string AssertMessageBuilder<T0, T1>(in T0 param0, in T1 param1);
#if OVRAVATAR_ASSERT_ENABLE_CONDITIONAL || !UNITY_ASSERTIONS
        [Conditional(OVRAVATAR_ASSERT_FORCE_ENABLE)]
#endif
        internal static void AssertTwoParams<T0, T1>(bool condition, in T0 buildParam0, in T1 buildParam1, AssertMessageBuilder<T0, T1> builder
            , string scope = DEFAULT_ASSERT_SCOPE, UnityEngine.Object context = null)
        {
            // Should be a static method, otherwise wrap in conditional to avoid alloc
            Debug.Assert(builder.Target == null, "Instanced builder passed to AssertTwoParams");
            if (!condition)
            {
                LogAssert(builder(in buildParam0, in buildParam1), scope, context);
            }
        }

        public delegate string AssertLessThanMessageBuilder<T>(in T lhs, in T rhs);
#if OVRAVATAR_ASSERT_ENABLE_CONDITIONAL || !UNITY_ASSERTIONS
        [Conditional(OVRAVATAR_ASSERT_FORCE_ENABLE)]
#endif
        internal static void AssertLessThan(int lesser, int greater, AssertLessThanMessageBuilder<int> builder
            , string scope = DEFAULT_ASSERT_SCOPE, UnityEngine.Object context = null)
        {
            // Should be a static method, otherwise wrap in conditional to avoid alloc
            Debug.Assert(builder.Target == null);
            if (lesser >= greater)
            {
                LogAssert(builder(in lesser, in greater), scope, context);
            }
        }

        // HACK: TODO: Remove everything below here before release
        // Down-ranking specific native logs that often spam

        private const string _bodyApiMsg =
            "failed 'ovrBody_GetPose(context_, pose)' with 'An unknown error has occurred'(65537)";

        private const string gltfAttributeMsg =
            "gltfmeshprimitiveitem::Skipping vertex attribute in glTF mesh primitive:";

        private const string _ovrPluginInitMsg =
            "tracking::OVRPlugin not initialized";


        [Conditional("OVRAVATAR_HANDLE_SPECIAL_CASE")]
        private static void HandleSpecialCaseLogs(string message, ref bool handled)
        {
            handled = false;

            if (message.Contains(gltfAttributeMsg) || message.Contains(_ovrPluginInitMsg))
            {
                LogVerbose(message, "native");
                handled = true;
            }
            else if (message.Contains(_bodyApiMsg))
            {
                LogVerbose(
                    "tracking::Tracking failed 'ovrBody_GetPose(context_, pose)' with a bad input pose. Reusing pose from last frame",
                    "native");
                handled = true;
            }
        }
    }
}
