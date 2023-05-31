using System;
using Fusion;
using Fusion.Editor;
using UnityEditor;

namespace Photon.Voice.Fusion.Editor
{
    [InitializeOnLoad]
    public class VoiceEditorHelper
    {
        public const string VOICE_FUSION_INTEGRATION_ASMDEF_NAME = "PhotonVoice.Fusion";

        static VoiceEditorHelper()
        {
            AddVoiceAsmdef();
        }

        private static void AddVoiceAsmdef()
        {
            string[] current = NetworkProjectConfig.Global.AssembliesToWeave;
            if (Array.IndexOf(current, VOICE_FUSION_INTEGRATION_ASMDEF_NAME) < 0)
            {
                NetworkProjectConfig.Global.AssembliesToWeave = new string[current.Length + 1];
                for (int i = 0; i < current.Length; i++)
                {
                    NetworkProjectConfig.Global.AssembliesToWeave[i] = current[i];
                }
                NetworkProjectConfig.Global.AssembliesToWeave[current.Length] = VOICE_FUSION_INTEGRATION_ASMDEF_NAME;
                NetworkProjectConfigUtilities.SaveGlobalConfig();
            }
        }
    }
}