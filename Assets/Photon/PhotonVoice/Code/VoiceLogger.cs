// ----------------------------------------------------------------------------
// <copyright file="VoiceLogger.cs" company="Exit Games GmbH">
//   Photon Voice for Unity - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>
// Logger for voice components.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------


namespace Photon.Voice.Unity
{
    using ExitGames.Client.Photon;
    using UnityEngine;

    [AddComponentMenu("Photon Voice/Voice Logger")]
    [DisallowMultipleComponent]
    public class VoiceLogger : MonoBehaviour
    {
        public DebugLevel LogLevel = DebugLevel.WARNING;

        // required for the MonoBehaviour to have the 'enabled' checkbox
        private void Start()
        {
        }

        static public VoiceLogger FindLogger(GameObject gameObject)
        {
            // serach through the hierarchy
            for (var go = gameObject; go != null; go = go.transform.parent == null ? null : go.transform.parent.gameObject)
            {
                var vl = go.GetComponent<VoiceLogger>();
                if (vl != null && vl.enabled)
                {
                    return vl;
                }
            }

            // look for VoiceLogger at the root
            VoiceLogger vlRoot = null;
            foreach (var vl in Object.FindObjectsOfType<VoiceLogger>())
            {
                if (vl.transform.parent == null && vl.enabled)
                {
                    if (vlRoot != null)
                    {
                        UnityLogger.Log(DebugLevel.INFO, vl, "LOGGER", vlRoot.name, "Disabling VoiceLogger duplicates at the scene root.");
                        vl.enabled = false;
                    }
                    else
                    {
                        vlRoot = vl;
                    }
                }
            }
            return vlRoot;
        }

        static public VoiceLogger CreateRootLogger()
        {
            var logObject = new GameObject("VoiceLogger");
            return logObject.AddComponent<VoiceLogger>();
        }

#if UNITY_EDITOR
        static public void EditorVoiceLoggerOnInspectorGUI(GameObject gameObject)
        {
            var vl = FindLogger(gameObject);
            if (vl == null)
            {
                vl = CreateRootLogger();
                // vl.gameObject.hideFlags = HideFlags.HideInHierarchy; //NotEditable HideInInspector HideInHierarchy
                // Let the Editor know that the scene has been updated
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(vl.gameObject.scene);
            }

            vl.LogLevel = (DebugLevel)UnityEditor.EditorGUILayout.EnumPopup("Log Level", vl.LogLevel);
        }
#endif
    }

    public static class UnityLogger
    {
        public static void Log(DebugLevel level, Object obj, string tag, string objName, string fmt, params object[] args)
        {
            // obj.name is available only on the main thread, so we pass objName here
            fmt = GetFormatString(level, tag, objName, fmt);
            if (obj == null)
            {
                switch (level)
                {
                    case DebugLevel.ERROR: Debug.LogErrorFormat(fmt, args); break;
                    case DebugLevel.WARNING: Debug.LogWarningFormat(fmt, args); break;
                    case DebugLevel.INFO: Debug.LogFormat(fmt, args); break;
                    case DebugLevel.ALL: Debug.LogFormat(fmt, args); break;
                }
            }
            else
            {
                switch (level)
                {
                    case DebugLevel.ERROR: Debug.LogErrorFormat(obj, fmt, args); break;
                    case DebugLevel.WARNING: Debug.LogWarningFormat(obj, fmt, args); break;
                    case DebugLevel.INFO: Debug.LogFormat(obj, fmt, args); break;
                    case DebugLevel.ALL: Debug.LogFormat(obj, fmt, args); break;
                }
            }
        }
        private static string GetFormatString(DebugLevel level, string tag, string objName,  string fmt)
        {
            return string.Format("[{0}] [{1}] [{2}] [{3}] {4}", GetTimestamp(), level, tag, objName, fmt);
        }

        private static string GetTimestamp()
        {
            return System.DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss", new System.Globalization.CultureInfo("en-US"));
        }
    }
}
