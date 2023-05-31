#if USING_XR_MANAGEMENT && USING_XR_SDK_OCULUS && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Oculus.Avatar2
{
    /// <summary>
    /// This editor script adds a preprocess build step that copies the Avatars SDK streaming assets that are required
    /// on the current target platform to the project's StreamingAssets folder. The copied assets are cleaned up after
    /// the build finishes.
    /// Run this manually from the AvatarSDK2 > Streaming Assets menu.
    /// </summary>
    public class StreamingAssetsPlatformIncluder : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static char s = Path.DirectorySeparatorChar;

        private static readonly string[] UniversalPaths =
        {
            $"Oculus{s}OvrAvatar2Assets.zip",
            $"SampleAssets{s}PresetAvatars_Fastload.zip",
        };

        private static readonly string[] RiftPaths =
        {
            $"SampleAssets{s}PresetAvatars_Rift.zip",
        };

        private static readonly string[] QuestPaths =
        {
            $"SampleAssets{s}PresetAvatars_Quest.zip",
        };


        private static readonly List<string> AssetsToCopy = new List<string>();

        public int callbackOrder => default;

        public void OnPreprocessBuild(BuildReport report)
        {
            Application.logMessageReceived += OnBuildError; // Start listening for errors
            CopyStreamingAssets();
        }

        private void OnBuildError(string condition, string stacktrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                Application.logMessageReceived -= OnBuildError; // Stop listening for errors
                EditorApplication.update += CleanUpStreamingAssets; // Clean up after build stops
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            Application.logMessageReceived -= OnBuildError; // Stop listening for errors
            CleanUpStreamingAssets();
        }

        [MenuItem("AvatarSDK2/Streaming Assets/Copy assets for current platform")]
        private static void CopyStreamingAssets()
        {
            AssetsToCopy.Clear();
            AssetsToCopy.AddRange(UniversalPaths);

#if USING_XR_SDK
            bool isBuildingXR = true;
#else
            bool isBuildingXR = false;
#endif

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                AssetsToCopy.AddRange(QuestPaths);
            }
            else
            {
                AssetsToCopy.AddRange(RiftPaths);
            }

            OvrAvatarLog.LogInfo("Copying required Avatar2 assets to StreamingAssets",
                nameof(StreamingAssetsPlatformIncluder));

            CopyFiles(AssetsToCopy);
            AssetDatabase.Refresh();
        }

        private void CleanUpStreamingAssets()
        {
            EditorApplication.update -= CleanUpStreamingAssets;
            OvrAvatarLog.LogInfo("Cleaning up Avatar2 streaming assets", nameof(StreamingAssetsPlatformIncluder));

            // Clean up the files that were copied
            DeleteFiles(AssetsToCopy);

            AssetDatabase.Refresh();
        }

        private static void CopyFiles(List<string> paths)
        {
            foreach (var path in paths)
            {
                var source = GetSourcePath(path);
                var destination = GetDestinationPath(path);

                if (!File.Exists(source))
                {
                    OvrAvatarLog.LogWarning("Trying to copy an asset that doesn't exist: " + source,
                        nameof(StreamingAssetsPlatformIncluder));
                    continue;
                }

                if (File.Exists(destination))
                {
                    OvrAvatarLog.LogWarning(
                        $"Asset at path {destination} already exists and will be overwritten. Fix this using AvatarSDK2 > Streaming Assets > Clean up",
                        nameof(StreamingAssetsPlatformIncluder));
                }

                try
                {
                    var destinationDirectory = Path.GetDirectoryName(destination);
                    Directory.CreateDirectory(destinationDirectory ?? throw new InvalidOperationException("Bad destination file path"));
                    File.Copy(source, destination, true);
                }
                catch (IOException e)
                {
                    OvrAvatarLog.LogException("Copy StreamingAssets", e, nameof(StreamingAssetsPlatformIncluder));
                }
            }
        }

        private static void DeleteFiles(List<string> paths)
        {
            var directories = new HashSet<string>();

            foreach (var path in paths)
            {
                try
                {
                    var destinationPath = GetDestinationPath(path);
                    directories.Add(Path.GetDirectoryName(destinationPath));

                    File.Delete(destinationPath);
                    File.Delete(destinationPath + ".meta");
                }
                catch (IOException e)
                {
                    OvrAvatarLog.LogException("Clean up StreamingAssets", e, nameof(StreamingAssetsPlatformIncluder));
                }
            }

            // Clean up empty directories too
            foreach (var directory in directories)
            {
                try
                {
                    bool isDirectoryEmpty;
                    using (var enumerator = Directory.EnumerateFileSystemEntries(directory).GetEnumerator())
                    {
                        isDirectoryEmpty = !enumerator.MoveNext();
                    }

                    if (isDirectoryEmpty)
                    {
                        Directory.Delete(directory);
                        File.Delete(directory + ".meta");
                    }
                }
                catch (IOException e)
                {
                    OvrAvatarLog.LogException("Clean up StreamingAssets", e, nameof(StreamingAssetsPlatformIncluder));
                }
            }
        }

        private static string GetSourcePath(string file)
        {
            return Path.Combine(Application.dataPath, "Oculus", "Avatar2", "StreamingAssets", file);
        }

        private static string GetDestinationPath(string file)
        {
            return Path.Combine(Application.streamingAssetsPath, file);
        }

        [InitializeOnLoad]
        private static class UpgradeCheck
        {
            static UpgradeCheck()
            {
                if (!SessionState.GetBool("AvatarStreamingAssetsUpgradeCheckRanOnce", false))
                {
                    EditorApplication.update += RunOnce;
                }
            }

            [MenuItem("AvatarSDK2/Streaming Assets/Clean up")]
            private static void RunOnce()
            {
                EditorApplication.update -= RunOnce;
                if (DoesProjectNeedUpgrade())
                {
                    SessionState.SetBool("AvatarStreamingAssetsUpgradeCheckRanOnce", true);
                    if (EditorUtility.DisplayDialog("Avatars SDK Upgrade",
                            "A previous version of Avatars SDK copied assets to this project's StreamingAssets folder during setup. The current version of Avatars SDK no longer needs these extra assets.\nWould you like to clean up this project's StreamingAssets folder?\n\nYou can also do this later using AvatarSDK2 > Streaming Assets > Clean up.",
                            "Clean up", "Ignore"))
                    {
                        CleanUpOldStreamingAssets();
                    }
                }
            }

            private static bool DoesProjectNeedUpgrade()
            {
                var oldFilePath = Path.Combine(Application.streamingAssetsPath, "Oculus", "OvrAvatar2Assets.zip");
                return File.Exists(oldFilePath);
            }

            private static void CleanUpOldStreamingAssets()
            {
                var allStreamingAssets = new List<string>();
                allStreamingAssets.AddRange(UniversalPaths);
                allStreamingAssets.AddRange(RiftPaths);
                allStreamingAssets.AddRange(QuestPaths);

                DeleteFiles(allStreamingAssets);

                AssetDatabase.Refresh();
            }
        }
    }
}
