#if FUSION_WEAVER
using System;

namespace Photon.Voice.Fusion.Editor
{
    using Unity.Editor;
    using UnityEditor;
    using UnityEngine;
    using global::Fusion;

    [CustomEditor(typeof(FusionVoiceClient))]
    public class FusionVoiceClientEditor : VoiceConnectionEditor
    {
        private SerializedProperty useFusionAppSettingsSp;
        private SerializedProperty useFusionAuthValuesSp;

        protected override void OnEnable()
        {
            base.OnEnable();
            this.useFusionAppSettingsSp = this.serializedObject.FindProperty("UseFusionAppSettings");
            this.useFusionAuthValuesSp = this.serializedObject.FindProperty("UseFusionAuthValues");
        }

        protected override void DisplayAppSettings()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(this.useFusionAppSettingsSp, new GUIContent("Use Fusion App Settings", "Use App Settings From Fusion's PhotonServerSettings"));
            if (GUILayout.Button("FusionAppSettings", EditorStyles.miniButton, GUILayout.Width(150)))
            {
                Selection.objects = new Object[] { global::Fusion.Photon.Realtime.PhotonAppSettings.Instance };
                EditorGUIUtility.PingObject(global::Fusion.Photon.Realtime.PhotonAppSettings.Instance);
            }
            EditorGUILayout.EndHorizontal();
            if (!this.useFusionAppSettingsSp.boolValue)
            {
                EditorGUI.indentLevel++;
                base.DisplayAppSettings();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(this.useFusionAuthValuesSp, new GUIContent("Use Fusion Auth Values", "Use the same Authentication Values From PUN client"));
        }
    }
}
#endif