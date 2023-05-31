#if PHOTON_VOICE_FMOD_AVAILABLE
using Photon.Voice.Unity.Editor;
#endif
using Photon.Realtime;
using System;
using UnityEditor;

namespace Photon.Voice.Unity.FMOD.Editor
{
    [InitializeOnLoad] // calls static constructor when script is recompiled
    public static class FMODHelpersMenu
    {
#if PHOTON_VOICE_FMOD_AVAILABLE
        public const string PHOTON_VOICE_FMOD_DEFINE_SYMBOL = "PHOTON_VOICE_FMOD_ENABLE";
#endif
        public const string PHOTON_VOICE_FMOD_AVAILABLE_DEFINE_SYMBOL = "PHOTON_VOICE_FMOD_AVAILABLE";

        static FMODHelpersMenu()
        {
#if PHOTON_VOICE_FMOD_AVAILABLE
            if (!HasFMOD)
            {
                UnityEngine.Debug.Log("FMOD Unity plugin is no longer available: Photon Voice FMOD integration disabled");
                PhotonVoiceEditorUtils.RemoveScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VOICE_FMOD_AVAILABLE_DEFINE_SYMBOL);
                RemoveFMOD();
                TriggerRecompile();
            }
#else
            if (HasFMOD)
            {
                UnityEngine.Debug.Log("FMOD Unity plugin is now available: we can use Photon Voice FMOD integration");
                PhotonEditorUtils.AddScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VOICE_FMOD_AVAILABLE_DEFINE_SYMBOL);
                TriggerRecompile();
            }
#endif
        }

        // triggers this calss recompilation after define symbols change
        private static void TriggerRecompile()
        {
            AssetDatabase.ImportAsset("Assets/Photon/PhotonVoice/Code/FMOD/Editor/FMODHelpersMenu.cs");
        }

        public static bool HasFMOD
        {
            get
            {
                return Type.GetType("FMODUnity.RuntimeManager, FMODUnity") != null;
            }
        }

#if PHOTON_VOICE_FMOD_AVAILABLE

        [MenuItem("Window/Photon Voice/Enable FMOD Integration", true, 4)]
        private static bool AddFMODValidate()
        {
#if PHOTON_VOICE_FMOD_ENABLE
            return false;
#else
            return true;
#endif
        }

        [MenuItem("Window/Photon Voice/Enable FMOD Integration", false, 4)]
        private static void AddFMOD()
        {
            PhotonEditorUtils.AddScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VOICE_FMOD_DEFINE_SYMBOL);
            ToggleUnityAudio(true);
        }

        [MenuItem("Window/Photon Voice/Disable FMOD Integration", true, 4)]
        private static bool RemoveFMODValidate()
        {
#if PHOTON_VOICE_FMOD_ENABLE
            return true;
#else
            return false;
#endif
        }

        [MenuItem("Window/Photon Voice/Disable FMOD Integration", false, 4)]
        private static void RemoveFMOD()
        {
            PhotonVoiceEditorUtils.RemoveScriptingDefineSymbolToAllBuildTargetGroups(PHOTON_VOICE_FMOD_DEFINE_SYMBOL);
            ToggleUnityAudio(false);
        }

        private static void ToggleUnityAudio(bool on)
        {
            var audioManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0];
            var serializedManager = new SerializedObject(audioManager);
            var prop = serializedManager.FindProperty("m_DisableAudio");
            prop.boolValue = on;
            serializedManager.ApplyModifiedProperties();
        }

#endif
    }
}