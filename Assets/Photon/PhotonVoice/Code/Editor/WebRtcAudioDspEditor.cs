using UnityEngine;

namespace Photon.Voice.Unity.Editor
{
    using UnityEditor;
    using Unity;

    [CustomEditor(typeof(WebRtcAudioDsp))]
    public class WebRtcAudioDspEditor : Editor
    {
        private WebRtcAudioDsp processor;
        private Recorder recorder;

        private SerializedProperty aecSp;
        private SerializedProperty aecHighPassSp;
        private SerializedProperty agcSp;
        private SerializedProperty agcCompressionGainSp;
        private SerializedProperty agcTargetLevelSp;
        private SerializedProperty vadSp;
        private SerializedProperty highPassSp;
        private SerializedProperty noiseSuppressionSp;
        private SerializedProperty reverseStreamDelayMsSp;

        private void OnEnable()
        {
            this.processor = this.target as WebRtcAudioDsp;
            this.recorder = this.processor.GetComponent<Recorder>();
            this.aecSp = this.serializedObject.FindProperty("aec");
            this.aecHighPassSp = this.serializedObject.FindProperty("aecHighPass");
            this.agcSp = this.serializedObject.FindProperty("agc");
            this.agcCompressionGainSp = this.serializedObject.FindProperty("agcCompressionGain");
            this.agcTargetLevelSp = this.serializedObject.FindProperty("agcTargetLevel");
            this.vadSp = this.serializedObject.FindProperty("vad");
            this.highPassSp = this.serializedObject.FindProperty("highPass");
            this.noiseSuppressionSp = this.serializedObject.FindProperty("noiseSuppression");
            this.reverseStreamDelayMsSp = this.serializedObject.FindProperty("reverseStreamDelayMs");
        }

        public override void OnInspectorGUI()
        {
            this.serializedObject.UpdateIfRequiredOrScript();
            EditorGUI.BeginChangeCheck();
            bool isInSceneInPlayMode = PhotonVoiceEditorUtils.IsInTheSceneInPlayMode(this.processor.gameObject);

            EditorGUILayout.BeginHorizontal();
            this.processor.enabled = EditorGUILayout.Toggle(new GUIContent("Enabled", "Enable DSP"), this.processor.enabled);
            if (isInSceneInPlayMode)
            {
                this.processor.Bypass = EditorGUILayout.Toggle(new GUIContent("Bypass", "Bypass WebRTC Audio DSP"), this.processor.Bypass);
            }
            EditorGUILayout.EndHorizontal();

            if (isInSceneInPlayMode)
            {
                this.processor.AEC = EditorGUILayout.Toggle(new GUIContent("AEC", "Acoustic Echo Cancellation"), this.processor.AEC);
                EditorGUI.indentLevel++;
                this.processor.ReverseStreamDelayMs = EditorGUILayout.IntField(new GUIContent("ReverseStreamDelayMs", "Reverse stream delay (hint for AEC) in Milliseconds"), this.processor.ReverseStreamDelayMs);
                this.processor.AecHighPass = EditorGUILayout.Toggle(new GUIContent("AEC High Pass"), this.processor.AecHighPass);
                EditorGUI.indentLevel--;
                this.processor.AGC = EditorGUILayout.Toggle(new GUIContent("AGC", "Automatic Gain Control"), this.processor.AGC);
                EditorGUI.indentLevel++;
                this.processor.AgcCompressionGain = EditorGUILayout.IntSlider(new GUIContent("AGC Compression Gain"), this.processor.AgcCompressionGain, 0, 90);
                this.processor.AgcTargetLevel = EditorGUILayout.IntSlider(new GUIContent("AGC Target Level"), this.processor.AgcTargetLevel, 0, 31);
                EditorGUI.indentLevel--;
                this.processor.HighPass = EditorGUILayout.Toggle(new GUIContent("HighPass", "High Pass Filter"), this.processor.HighPass);
                this.processor.NoiseSuppression = EditorGUILayout.Toggle(new GUIContent("NoiseSuppression", "Noise Suppression"), this.processor.NoiseSuppression);

                if (this.processor.VAD && this.recorder.VoiceDetection)
                {
                    EditorGUILayout.HelpBox("You have enabled VAD here and in the associated Recorder. Please use only one Voice Detection algorithm.", MessageType.Warning);
                }
                this.processor.VAD = EditorGUILayout.Toggle(new GUIContent("VAD", "Voice Activity Detection"), this.processor.VAD);
            }
            else
            {
                EditorGUILayout.PropertyField(this.aecSp, new GUIContent("AEC", "Acoustic Echo Cancellation"));
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(this.reverseStreamDelayMsSp,
                    new GUIContent("ReverseStreamDelayMs", "Reverse stream delay (hint for AEC) in Milliseconds"));
                EditorGUILayout.PropertyField(this.aecHighPassSp, new GUIContent("AEC High Pass"));
                EditorGUI.indentLevel--;
                EditorGUILayout.PropertyField(this.agcSp, new GUIContent("AGC", "Automatic Gain Control"));
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(this.agcCompressionGainSp, new GUIContent("AGC Compression Gain"));
                EditorGUILayout.PropertyField(this.agcTargetLevelSp, new GUIContent("AGC Target Level"));
                EditorGUI.indentLevel--;
                if (this.vadSp.boolValue && this.recorder.VoiceDetection)
                {
                    EditorGUILayout.HelpBox("You have enabled VAD here and in the associated Recorder. Please use only one Voice Detection algorithm.", MessageType.Warning);
                }
                EditorGUILayout.PropertyField(this.highPassSp, new GUIContent("HighPass", "High Pass Filter"));
                EditorGUILayout.PropertyField(this.noiseSuppressionSp, new GUIContent("NoiseSuppression", "Noise Suppression"));
                EditorGUILayout.PropertyField(this.vadSp, new GUIContent("VAD", "Voice Activity Detection"));
                if (this.noiseSuppressionSp.boolValue)
                {
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                this.serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
