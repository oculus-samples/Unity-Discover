/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
class MetaXRAudioPluginUpdater
{
    private static bool restartPending = false;
    private static bool unityRunningInBatchmode = false;
    private const string rootDir = "Assets/Meta/XR/Audio";
    private const string binaryName = "MetaXRAudioUnity.dll";

    private static System.Version invalidVersion = new System.Version("0.0.0");

    static MetaXRAudioPluginUpdater()
    {
        EditorApplication.delayCall += OnDelayCall;
    }

    static void OnDelayCall()
    {
        if (System.Environment.CommandLine.Contains("-batchmode"))
        {
            unityRunningInBatchmode = true;
        }

        if (ShouldAttemptPluginUpdate())
        {
            AttemptSpatializerPluginUpdate(true);
        }
    }

    private static string GetCurrentProjectPath()
    {
        return Directory.GetParent(Application.dataPath).FullName;
    }

    private static string GetUtilitiesRootPath()
    {
        var so = ScriptableObject.CreateInstance(typeof(MetaXRAudioPluginUpdaterStub));
        var script = MonoScript.FromScriptableObject(so);
        string assetPath = AssetDatabase.GetAssetPath(script);
        string editorDir = Directory.GetParent(assetPath).FullName;
        string ovrDir = Directory.GetParent(editorDir).FullName;

        return ovrDir;
    }

    public static string GetVersionDescription(System.Version version)
    {
        bool isVersionValid = (version != invalidVersion);
        return isVersionValid ? version.ToString() : "(Unknown)";
    }

    private static bool ShouldAttemptPluginUpdate()
    {
        if (unityRunningInBatchmode)
        {
            return false;
        }
        else
        {
            return (autoUpdateEnabled && !restartPending && !Application.isPlaying);
        }
    }

    private static readonly string autoUpdateEnabledKey = "Oculus_Utilities_MetaXRAudioPluginUpdater_AutoUpdate_" + 1.0;//PASOVRManager.utilitiesVersion;
    private static bool autoUpdateEnabled
    {
        get
        {
            return PlayerPrefs.GetInt(autoUpdateEnabledKey, 1) == 1;
        }

        set
        {
            PlayerPrefs.SetInt(autoUpdateEnabledKey, value ? 1 : 0);
        }
    }

    [MenuItem("Meta/Tools/Update XR Audio Plugin")]
    private static void RunSpatializerPluginUpdate()
    {
        autoUpdateEnabled = true;
        AttemptSpatializerPluginUpdate(false);
    }

    // Separate entry point needed since "-executeMethod" does not support parameters or default parameter values
    private static void BatchmodePluginUpdate()
    {
        OnDelayCall(); // manually invoke when running editor in batchmode
        AttemptSpatializerPluginUpdate(false);
    }

    private static string GetSpatializerPluginsRootPath()
    {
        string ovrPath = GetUtilitiesRootPath();
        string spatializerPluginsPath = Path.GetFullPath(Path.Combine(ovrPath, "./Plugins"));
        return spatializerPluginsPath;
    }

    private static bool RenameSpatializerPluginToOld(string currentPluginPath)
    {
        if (File.Exists(currentPluginPath))
        {
            int index = 0;
            string targetPluginPath;
            string targetPluginMetaPath;
            for (; ; )
            {
                targetPluginPath = currentPluginPath + ".old" + index.ToString();
                targetPluginMetaPath = targetPluginPath + ".meta";
                if (!File.Exists(targetPluginPath) && !File.Exists(targetPluginPath))
                    break;
                ++index;
            }
            try
            {
                File.Move(currentPluginPath, targetPluginPath);
                File.Move(currentPluginPath + ".meta", targetPluginMetaPath);
                UnityEngine.Debug.LogFormat("Meta XR Audio plugin renamed: {0} to {1}", currentPluginPath, targetPluginPath);
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarningFormat("Unable to rename Meta XR Audio plugin: {0}, exception {1}", currentPluginPath, e.Message);
                return false;
            }
        }
        return false;
    }

    private const string PluginName = "Meta XR Audio";

    private static void OverrideSpatializerPlugin()
    {
        string[] pluginNames = AudioSettings.GetSpatializerPluginNames();
        if (!pluginNames.Contains(PluginName))
        {
            string pluginsPath = GetSpatializerPluginsRootPath();
            #if UNITY_EDITOR_64
            string pluginPath = Path.GetFullPath(Path.Combine(pluginsPath, "x86_64/" + binaryName));
            #else
            string pluginPath = Path.GetFullPath(Path.Combine(pluginsPath, "x86/" + binaryName));
            #endif
            if (File.Exists(pluginPath))
            {
                Debug.LogError("Meta XR Audio plugin not available");
            }
            return;
        }
        const string AudioManagerAssetPath = "ProjectSettings/AudioManager.asset";
        SerializedObject audioManager = new SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath(AudioManagerAssetPath)[0]);

        SerializedProperty spatializerPluginProperty = audioManager.FindProperty ("m_SpatializerPlugin");
        if (spatializerPluginProperty.stringValue != PluginName)
        {
            if (spatializerPluginProperty.stringValue == "")
            {
                Debug.Log($"Overriding Project Settings Spatializer Plugin with {PluginName}");
                spatializerPluginProperty.stringValue = PluginName;
            }
            else
            {
                Debug.LogWarning($"Meta XR Audio disabled, Spatializer Plugin set to {spatializerPluginProperty.stringValue}");
            }
        }

        SerializedProperty ambisonicDecoderProperty = audioManager.FindProperty("m_AmbisonicDecoderPlugin");
        if (ambisonicDecoderProperty.stringValue != PluginName)
        {
            if (ambisonicDecoderProperty.stringValue == "")
            {
                Debug.Log($"Overriding Project Settings Ambisonic Decoder Plugin with {PluginName}");
                ambisonicDecoderProperty.stringValue = PluginName;
            }
            else
            {
                Debug.LogWarning($"Meta XR Audio disabled, Ambisonic Decoder Plugin set to {ambisonicDecoderProperty.stringValue}");
            }
        }

        audioManager.ApplyModifiedProperties();
    }

    private static void AttemptSpatializerPluginUpdate(bool triggeredByAutoUpdate)
    {
        OverrideSpatializerPlugin();

        // We use a simplified path to update spatializer plugins:
        // If there is a new MetaXRAudioUnity.dll.new, we'll rename the original one to .old, and the new one to .dll, and restart the editor
        string pluginsPath = GetSpatializerPluginsRootPath();
        string newX86PluginPath = Path.GetFullPath(Path.Combine(pluginsPath, "x86/" + binaryName + ".new"));
        string newX64PluginPath = Path.GetFullPath(Path.Combine(pluginsPath, "x86_64/" + binaryName + ".new"));

        if (File.Exists(newX86PluginPath) || File.Exists(newX64PluginPath))
        {
            bool userAcceptsUpdate = false;

            if (unityRunningInBatchmode)
            {
                userAcceptsUpdate = true;
            }
            else
            {
                int dialogResult = EditorUtility.DisplayDialogComplex("Update Meta XR Audio Plugins",
                    "New Meta XR Audio plugin found. Do you want to upgrade? If you choose 'Upgrade', the old plugin will be renamed to " + binaryName + ".old",
                    "Upgrade", "Don't upgrade", "Delete new plugin");
                if (dialogResult == 0)
                {
                    userAcceptsUpdate = true;
                }
                else if (dialogResult == 1)
                {
                    // do nothing
                }
                else if (dialogResult == 2)
                {
                    string[] deletePaths = new[]
                        {newX86PluginPath, newX86PluginPath + ".meta", newX64PluginPath, newX64PluginPath + ".meta"};
                    foreach (string path in deletePaths)
                    {
                        try
                        {
                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }
                        }
                        catch (Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Exception happened when deleting new Meta XR Audio plugin: " + e.Message);
                        }
                    }
                }
            }

            if (userAcceptsUpdate)
            {
                bool upgradeDone = false;
                string curX86PluginPath = Path.Combine(pluginsPath, "x86/" + binaryName);
                if (File.Exists(newX86PluginPath))
                {
                    RenameSpatializerPluginToOld(curX86PluginPath);
                    try
                    {
                        File.Move(newX86PluginPath, curX86PluginPath);
                        File.Move(newX86PluginPath + ".meta", curX86PluginPath + ".meta");

                        // fix the platform
                        string curX86PluginPathRel = rootDir + "/Plugins/x86/" + binaryName;
                        UnityEngine.Debug.Log("path = " + curX86PluginPathRel);
                        AssetDatabase.ImportAsset(curX86PluginPathRel, ImportAssetOptions.ForceUpdate);
                        PluginImporter pi = PluginImporter.GetAtPath(curX86PluginPathRel) as PluginImporter;
                        pi.SetCompatibleWithEditor(false);
                        pi.SetCompatibleWithAnyPlatform(false);
                        pi.SetCompatibleWithPlatform(BuildTarget.Android, false);
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
#if UNITY_2017_3_OR_NEWER
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
#else
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXUniversal, false);
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXIntel, false);
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXIntel64, false);
#endif
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, true);
                        pi.SetCompatibleWithEditor(true);
                        pi.SetEditorData("CPU", "X86");
                        pi.SetEditorData("OS", "Windows");
                        pi.SetPlatformData("Editor", "CPU", "X86");
                        pi.SetPlatformData("Editor", "OS", "Windows");
                        pi.isPreloaded = true; // Audio plugin must be preloaded!

                        AssetDatabase.ImportAsset(curX86PluginPathRel, ImportAssetOptions.ForceUpdate);
                        AssetDatabase.Refresh();
                        AssetDatabase.SaveAssets();

                        upgradeDone = true;
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Unable to rename the new Meta XR Audio plugin: " + e.Message);
                    }
                }
                string curX64PluginPath = Path.Combine(pluginsPath, "x86_64/" + binaryName);
                if (File.Exists(newX64PluginPath))
                {
                    RenameSpatializerPluginToOld(curX64PluginPath);
                    try
                    {
                        File.Move(newX64PluginPath, curX64PluginPath);
                        File.Move(newX64PluginPath + ".meta", curX64PluginPath + ".meta");

                        // fix the platform
                        string curX64PluginPathRel = rootDir + "/Plugins/x86_64/" + binaryName;
                        UnityEngine.Debug.Log("path = " + curX64PluginPathRel);
                        AssetDatabase.ImportAsset(curX64PluginPathRel, ImportAssetOptions.ForceUpdate);
                        PluginImporter pi = PluginImporter.GetAtPath(curX64PluginPathRel) as PluginImporter;
                        pi.SetCompatibleWithEditor(false);
                        pi.SetCompatibleWithAnyPlatform(false);
                        pi.SetCompatibleWithPlatform(BuildTarget.Android, false);
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
#if UNITY_2017_3_OR_NEWER
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
#else
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXUniversal, false);
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXIntel, false);
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneOSXIntel64, false);
#endif
                        pi.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
                        pi.SetCompatibleWithEditor(true);
                        pi.SetEditorData("CPU", "X86_64");
                        pi.SetEditorData("OS", "Windows");
                        pi.SetPlatformData("Editor", "CPU", "X86_64");
                        pi.SetPlatformData("Editor", "OS", "Windows");
                        pi.isPreloaded = true; // Audio plugin must be preloaded!

                        AssetDatabase.ImportAsset(curX64PluginPathRel, ImportAssetOptions.ForceUpdate);
                        AssetDatabase.Refresh();
                        AssetDatabase.SaveAssets();

                        upgradeDone = true;
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Unable to rename the new Meta XR Audio plugin: " + e.Message);
                    }
                }

                if (upgradeDone)
                {
                    if (unityRunningInBatchmode
                        || EditorUtility.DisplayDialog("Restart Unity",
                            "Meta XR Audio plugins has been upgraded."
                                + "\n\nPlease restart the Unity Editor to complete the update process."
#if !UNITY_2017_1_OR_NEWER
 + " You may need to manually relaunch Unity if you are using Unity 5.6 and higher."
#endif
,
                            "Restart",
                            "Not Now"))
                    {
                        RestartUnityEditor();
                    }
                }
            }
        }
    }

    private static void RestartUnityEditor()
    {
        if (unityRunningInBatchmode)
        {
            EditorApplication.Exit(0);
        }
        else
        {
            restartPending = true;
            EditorApplication.OpenProject(GetCurrentProjectPath());
        }
    }
}
