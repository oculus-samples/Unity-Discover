namespace Photon.Voice
{
    using System;
    using System.IO;
    using Photon.Realtime;
    using UnityEngine;

    /// <summary>
    /// Collection of connection-relevant settings, used internally by PhotonNetwork.ConnectUsingSettings.
    /// </summary>
    /// <remarks>
    /// Includes the AppSettings class from the Realtime APIs plus some other, PUN-relevant, settings.</remarks>
    [Serializable]
    public class PhotonAppSettings : ScriptableObject
    {
        [Tooltip("Core Photon Server/Cloud settings.")]
        public AppSettings AppSettings;

        /// <summary>Sets appid and region code in the AppSettings. Used in Editor.</summary>
        public void UseCloud(string cloudAppid, string code = "")
        {
            this.AppSettings.AppIdRealtime = cloudAppid;
            this.AppSettings.Server = null;
            this.AppSettings.FixedRegion = string.IsNullOrEmpty(code) ? null : code;
        }

        static private PhotonAppSettings instance;

        static public PhotonAppSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    LoadOrCreateSettings();
                }
                return instance;
            }
        }

        const string SettingsFileName = "VoiceAppSettings";
        const string PhotonVoiceFolderGUID = "d3a9df3027b4a45679a2a3e978dde78e";

        public static void LoadOrCreateSettings()
        {

            // try to load the resource / asset
            instance = (PhotonAppSettings)Resources.Load(SettingsFileName, typeof(PhotonAppSettings));
            if (instance != null)
            {
                return;
            }

            // create the ScriptableObject if it could not be loaded
            if (instance == null)
            {
                instance = (PhotonAppSettings)ScriptableObject.CreateInstance(typeof(PhotonAppSettings));
                if (instance == null)
                {
                    Debug.LogError("Failed to create ServerSettings. PUN is unable to run this way. If you deleted it from the project, reload the Editor.");
                    return;
                }
            }


#if UNITY_EDITOR
            // in the editor, store the settings file as it could not be loaded
            // don't save the settings while Unity still imports assets
            if (UnityEditor.EditorApplication.isUpdating)
            {
                UnityEditor.EditorApplication.delayCall += delegate { LoadOrCreateSettings(); };
                return;
            }

            var voicePath = UnityEditor.AssetDatabase.GUIDToAssetPath(PhotonVoiceFolderGUID);
            if (voicePath == null || voicePath == "")
            {
                voicePath = "Assets/Photon/PhotonVoice";
            }

            string path = Path.Combine(voicePath, "Resources", SettingsFileName + ".asset");
            string dir = Path.GetDirectoryName(path);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                UnityEditor.AssetDatabase.ImportAsset(dir);
            }

            if (!File.Exists(path))
            {
                UnityEditor.AssetDatabase.CreateAsset(instance, path);
            }
            UnityEditor.AssetDatabase.SaveAssets();

            // if the project does not have PhotonServerSettings yet, enable "Development Build" to use the Dev Region.
            UnityEditor.EditorUserBuildSettings.development = true;
#endif
        }

        /// <summary>String summary of the AppSettings.</summary>
        public override string ToString()
        {
            return "VoiceAppSettings: " + this.AppSettings.ToStringFull();
        }
    }
}