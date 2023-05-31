using System;

using UnityEditor;

namespace Oculus.Avatar2
{
    public static class OvrLogSettings
    {
        private const string OVRAVATAR_LOG_ENABLE_CONDITIONAL = "OVRAVATAR_LOG_ENABLE_CONDITIONAL";
        private const string OVRAVATAR_ASSERT_ENABLE_CONDITIONAL = "OVRAVATAR_ASSERT_ENABLE_CONDITIONAL";

        [MenuItem("AvatarSDK2/Debug/Enable Logs")]
        private static void EnableLogs()
        {
            SetLogsEnabled(true);
        }
        [MenuItem("AvatarSDK2/Debug/Enable Logs", true)]
        private static bool CheckIfEnableLogsIsValid()
        {
            return AreAnyLogsDisabled();
        }

        [MenuItem("AvatarSDK2/Debug/Disable Logs")]
        private static void DisableLogs()
        {
            SetLogsEnabled(false);
        }
        [MenuItem("AvatarSDK2/Debug/Disable Logs", true)]
        private static bool CheckIfDisableLogsIsValid()
        {
            return AreAnyLogsEnabled();
        }

        [MenuItem("AvatarSDK2/Debug/Enable Asserts")]
        private static void EnableAsserts()
        {
            SetAssertsEnabled(true);
        }
        [MenuItem("AvatarSDK2/Debug/Enable Asserts", true)]
        private static bool CheckIfEnableAssertsIsValid()
        {
            return AreAnyAssertsDisabled();
        }

        [MenuItem("AvatarSDK2/Debug/Disable Asserts")]
        private static void DisableAsserts()
        {
            SetAssertsEnabled(false);
        }
        [MenuItem("AvatarSDK2/Debug/Disable Asserts", true)]
        private static bool CheckIfDisableAssertsIsValid()
        {
            return AreAnyAssertsEnabled();
        }

        private static void SetLogsEnabled(bool enableLogs)
        {
            ConfigureDefines(OVRAVATAR_LOG_ENABLE_CONDITIONAL, OvrAvatarLog.OVRAVATAR_LOG_FORCE_ENABLE, enableLogs);
        }
        private static void SetAssertsEnabled(bool enableAsserts)
        {
            ConfigureDefines(OVRAVATAR_ASSERT_ENABLE_CONDITIONAL, OvrAvatarLog.OVRAVATAR_ASSERT_FORCE_ENABLE, enableAsserts);
        }

        private static void ConfigureDefines(string enableConditional, string forceEnableDefine, bool enableLogs)
        {
            var logChange = enableLogs ? "enabling" : "disabling";
            var conditionalDefineWithSemicolon = enableConditional + ';';
            var forceEnableDefineWithSemicolon = forceEnableDefine + ';';
            foreach (BuildTargetGroup target in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (!IsAvatarTarget(target)) { continue; }

                bool definesDidChange = false;

                var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
                if (!defines.Contains(enableConditional))
                {
                    UnityEngine.Debug.LogWarning($"Enabling conditional logging for {Enum.GetName(typeof(BuildTargetGroup), target)}");

                    defines = conditionalDefineWithSemicolon + defines;
                    definesDidChange = true;
                }

                if (defines.Contains(forceEnableDefine) != enableLogs)
                {
                    UnityEngine.Debug.Log($"Updating log settings for {Enum.GetName(typeof(BuildTargetGroup), target)} - {logChange}");

                    if (enableLogs)
                    {
                        defines = forceEnableDefineWithSemicolon + defines;
                    }
                    else
                    {
                        defines = defines.Replace(forceEnableDefineWithSemicolon, string.Empty);
                        defines = defines.Replace(forceEnableDefine, string.Empty);
                    }
                    definesDidChange = true;
                }

                if (definesDidChange)
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
                }
            }
        }

        private static bool AreAnyLogsEnabled()
            => AreSymbolsEnabled(OVRAVATAR_LOG_ENABLE_CONDITIONAL, OvrAvatarLog.OVRAVATAR_LOG_FORCE_ENABLE);
        private static bool AreAnyLogsDisabled()
            => AreSymbolsDisabled(OVRAVATAR_LOG_ENABLE_CONDITIONAL, OvrAvatarLog.OVRAVATAR_LOG_FORCE_ENABLE);

        private static bool AreAnyAssertsEnabled()
            => AreSymbolsEnabled(OVRAVATAR_ASSERT_ENABLE_CONDITIONAL, OvrAvatarLog.OVRAVATAR_ASSERT_FORCE_ENABLE);
        private static bool AreAnyAssertsDisabled()
            => AreSymbolsDisabled(OVRAVATAR_ASSERT_ENABLE_CONDITIONAL, OvrAvatarLog.OVRAVATAR_ASSERT_FORCE_ENABLE);


        private static bool AreSymbolsEnabled(string enableConditional, string forceEnable)
            => !IsDefineSetOnAnyPlatform(enableConditional) || IsDefineSetOnAllPlatforms(forceEnable);

        private static bool AreSymbolsDisabled(string enableConditional, string forceEnable)
            => IsDefineSetOnAnyPlatform(enableConditional) && !IsDefineSetOnAllPlatforms(forceEnable);

        private static readonly BuildTargetGroup[] SupportedPlatforms =
        {
                BuildTargetGroup.Standalone,
                BuildTargetGroup.Android,
                BuildTargetGroup.iOS,
        };
        private static bool IsAvatarTarget(BuildTargetGroup target) => SupportedPlatforms.Contains(target);

        private static bool IsDefineSetOnAnyPlatform(string define) => IsDefineSet(define, true);
        private static bool IsDefineSetOnAllPlatforms(string define) => IsDefineSet(define, false);
        private static bool IsDefineSet(string define, bool matchAnyPlatform)
        {
            foreach (BuildTargetGroup target in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (!IsAvatarTarget(target)) { continue; }

                var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
                if (defines.Contains(define) == matchAnyPlatform)
                {
                    return matchAnyPlatform;
                }
            }
            return !matchAnyPlatform;
        }
    }
}
