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

using UnityEditor;
using UnityEngine;
using System.Runtime.InteropServices;

public class MetaXRAudioReflectionCustomGUI : IAudioEffectPluginGUI
{
    private const string earlyReflectionEnabledParameterName = "Refl. Enable";
    private const string reverbEnabledParameterName = "Reverb Enable";
    private const string reverbLevelParameterName = "Reverb Level";
    private const string voiceLimitParameterName = "Voice limit";

    private bool showRoomAcoustics = true;
    private bool showConfiguration = true;

    public override string Name
    {
        get { return "Meta XR Audio Reflection"; }
    }

    public override string Description
    {
        get { return "Reflection parameters for Meta XR Audio"; }
    }

    public override string Vendor
    {
        get { return "Meta"; }
    }

    public override bool OnGUI(IAudioEffectPlugin plugin)
    {
        showRoomAcoustics = EditorGUILayout.Foldout(showRoomAcoustics, "Room Acoustics");
        if (showRoomAcoustics)
        {
            float fEarlyReflectionsEnabled;
            plugin.GetFloatParameter(earlyReflectionEnabledParameterName, out fEarlyReflectionsEnabled);
            bool bEarlyRelfectionsEnabled = EditorGUILayout.Toggle(
                new GUIContent("Early Reflections Enabled",
                    "When enabled, all XR Audio Sources with Early Reflections enabled will have audible reflections"),
                fEarlyReflectionsEnabled != 0.0f);
            plugin.SetFloatParameter(earlyReflectionEnabledParameterName, bEarlyRelfectionsEnabled ? 1.0f : 0.0f);

            float fReverbEnabled;
            plugin.GetFloatParameter(reverbEnabledParameterName, out fReverbEnabled);
            bool bReverbEnabled = EditorGUILayout.Toggle(
                new GUIContent("Reverb Enabled",
                    "When enabled, all XR Audio Sources with Reverb enabled will have audible reverb"),
                fReverbEnabled != 0.0f);
            plugin.SetFloatParameter(reverbEnabledParameterName, bReverbEnabled ? 1.0f : 0.0f);

            EditorGUILayout.Space();

            float reverbLevel;
            plugin.GetFloatParameter(reverbLevelParameterName, out reverbLevel);
            plugin.SetFloatParameter(reverbLevelParameterName,
                EditorGUILayout.Slider(
                    new GUIContent("Reverb Level (dB)",
                        "Increases the reverb level of all sound sources in the scene that have reverb enabled"),
                    reverbLevel, -60.0f, 20.0f));
        }

        showConfiguration = EditorGUILayout.Foldout(showConfiguration, "Configuration");
        if (showConfiguration)
        {
            MetaXRAudioSettings.Instance.voiceLimit = EditorGUILayout.IntField(
                new GUIContent(voiceLimitParameterName,
                    "Max number of spatialized voices. Must be larger than the total number of spatialized sounds that can play concurrently"),
                MetaXRAudioSettings.Instance.voiceLimit);
        }

        if (GUI.changed)
        {
            GUI.changed = false;
            EditorUtility.SetDirty(MetaXRAudioSettings.Instance);
        }

        // We will override the controls with our own, so return false
        return false;
    }
}
