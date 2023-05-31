namespace Photon.Voice.Unity.Editor
{
    using POpusCodec.Enums;
    using System;
    using Unity;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(Recorder))]
    public class RecorderEditor : Editor
    {
        private Recorder recorder;

        private SerializedProperty voiceDetectionSp;
        private SerializedProperty voiceDetectionThresholdSp;
        private SerializedProperty voiceDetectionDelayMsSp;
        private SerializedProperty interestGroupSp;
        private SerializedProperty debugEchoModeSp;
        private SerializedProperty reliableModeSp;
        private SerializedProperty encryptSp;
        private SerializedProperty transmitEnabledSp;
        private SerializedProperty samplingRateSp;
        private SerializedProperty frameDurationSp;
        private SerializedProperty bitrateSp;
        private SerializedProperty sourceTypeSp;
        private SerializedProperty microphoneTypeSp;
        private SerializedProperty audioClipSp;
        private SerializedProperty loopAudioClipSp;
        private SerializedProperty recordingEnabledSp;
        private SerializedProperty stopRecordingWhenPausedSp;
        private SerializedProperty useMicrophoneTypeFallbackSp;
        private SerializedProperty recordWhenJoinedSp;

        //#if UNITY_IOS
        private SerializedProperty audioSessionParametersSp;
        private SerializedProperty editorAudioSessionPresetSp;

        //#elif UNITY_ANDROID
        private SerializedProperty androidNativeMicrophoneSettingsSp;

        //#endif

        private void OnEnable()
        {
            this.recorder = this.target as Recorder;
            this.voiceDetectionSp = this.serializedObject.FindProperty("voiceDetection");
            this.voiceDetectionThresholdSp = this.serializedObject.FindProperty("voiceDetectionThreshold");
            this.voiceDetectionDelayMsSp = this.serializedObject.FindProperty("voiceDetectionDelayMs");
            this.interestGroupSp = this.serializedObject.FindProperty("interestGroup");
            this.debugEchoModeSp = this.serializedObject.FindProperty("debugEchoMode");
            this.reliableModeSp = this.serializedObject.FindProperty("reliableMode");
            this.encryptSp = this.serializedObject.FindProperty("encrypt");
            this.transmitEnabledSp = this.serializedObject.FindProperty("transmitEnabled");
            this.samplingRateSp = this.serializedObject.FindProperty("samplingRate");
            this.frameDurationSp = this.serializedObject.FindProperty("frameDuration");
            this.bitrateSp = this.serializedObject.FindProperty("bitrate");
            this.sourceTypeSp = this.serializedObject.FindProperty("sourceType");
            this.microphoneTypeSp = this.serializedObject.FindProperty("microphoneType");
            this.audioClipSp = this.serializedObject.FindProperty("audioClip");
            this.loopAudioClipSp = this.serializedObject.FindProperty("loopAudioClip");
            this.recordingEnabledSp = this.serializedObject.FindProperty("recordingEnabled");
            this.stopRecordingWhenPausedSp = this.serializedObject.FindProperty("stopRecordingWhenPaused");
            this.useMicrophoneTypeFallbackSp = this.serializedObject.FindProperty("useMicrophoneTypeFallback");
            this.recordWhenJoinedSp = this.serializedObject.FindProperty("recordWhenJoined");
            //#if UNITY_IOS
            this.editorAudioSessionPresetSp = this.serializedObject.FindProperty("editorAudioSessionPreset");
            this.audioSessionParametersSp = this.serializedObject.FindProperty("audioSessionParameters");
            //#elif UNITY_ANDROID
            this.androidNativeMicrophoneSettingsSp = this.serializedObject.FindProperty("androidNativeMicrophoneSettings");
            //#endif
        }

        private void OnDisable()
        {
        }

        public override bool RequiresConstantRepaint() { return true; }

        public override void OnInspectorGUI()
        {
            this.serializedObject.UpdateIfRequiredOrScript();
            //serializedObject.Update();
            WebRtcAudioDsp webRtcAudioDsp = this.recorder.GetComponent<WebRtcAudioDsp>();
            bool webRtcAudioDspAttached = webRtcAudioDsp && webRtcAudioDsp != null;
            bool webRtcAudioDspAvailable = webRtcAudioDspAttached && webRtcAudioDsp.enabled;
            AudioChangesHandler audioChangesHandler = this.recorder.GetComponent<AudioChangesHandler>();
            bool audioChangesHandlerAttached = null != audioChangesHandler && audioChangesHandler;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(this.recordingEnabledSp,
                new GUIContent("Recording Enabled", "If true, recording is started when Recorder is initialized."));
            EditorGUILayout.PropertyField(this.recordWhenJoinedSp,
                new GUIContent("Record When Joined",
                    "If true, recording can start only when client is joined to a room. Auto start is also delayed until client is joined to a room."));
            EditorGUILayout.PropertyField(this.stopRecordingWhenPausedSp,
                new GUIContent("Stop Recording When Paused",
                    "If true, stop recording when paused resume/restart when un-paused."));
            EditorGUILayout.PropertyField(this.transmitEnabledSp,
                new GUIContent("Transmit Enabled", "If true, audio transmission is enabled."));
            EditorGUILayout.PropertyField(this.interestGroupSp,
                new GUIContent("Interest Group", "Target interest group that will receive transmitted audio."));
            EditorGUILayout.PropertyField(this.debugEchoModeSp,
                new GUIContent("Debug Echo",
                    "If true, outgoing stream routed back to client via server same way as for remote client's streams."));
            EditorGUILayout.PropertyField(this.encryptSp,
                new GUIContent("Encrypt", "If true, voice stream is sent encrypted."));
            EditorGUILayout.PropertyField(this.reliableModeSp, new GUIContent("Reliable Mode",
                    "If true, stream data sent in reliable mode."));

            EditorGUILayout.LabelField("Codec Parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.frameDurationSp,
                new GUIContent("Frame Duration", "Outgoing audio stream encoder delay."));
            if (webRtcAudioDspAvailable)
            {
                OpusCodec.FrameDuration frameDuration = (OpusCodec.FrameDuration)Enum.GetValues(typeof(OpusCodec.FrameDuration)).GetValue(this.frameDurationSp.enumValueIndex);
                switch (frameDuration)
                {
                    case OpusCodec.FrameDuration.Frame2dot5ms:
                    case OpusCodec.FrameDuration.Frame5ms:
                        string warningMessage = string.Format("Frame duration requested ({0}ms) is not supported by WebRTC Audio DSP (it needs to be N x 10ms), switching to the closest supported value: 10ms.", (int)frameDuration / 1000);
                        EditorGUILayout.HelpBox(warningMessage, MessageType.Warning);
                        break;
                }
            }
            EditorGUILayout.PropertyField(this.samplingRateSp,
                new GUIContent("Sampling Rate", "Outgoing audio stream sampling rate."));
            if (webRtcAudioDspAvailable)
            {
                SamplingRate samplingRate = (SamplingRate)Enum.GetValues(typeof(SamplingRate)).GetValue(this.samplingRateSp.enumValueIndex);
                switch (samplingRate)
                {
                    case SamplingRate.Sampling12000:
                        string warningMessage = "Sampling rate requested (12kHz) is not supported by WebRTC Audio DSP. When recording starts, this will be automatically switched to the closest supported value: 16kHz.";
                        EditorGUILayout.HelpBox(warningMessage, MessageType.Warning);
                        break;
                    case SamplingRate.Sampling24000:
                        warningMessage = "Sampling rate requested (24kHz) is not supported by WebRTC Audio DSP. When recording starts, this will be automatically switched to the closest supported value: 48kHz.";
                        EditorGUILayout.HelpBox(warningMessage, MessageType.Warning);
                        break;
                }
            }
            EditorGUILayout.PropertyField(this.bitrateSp,
                new GUIContent("Bitrate", "Outgoing audio stream bitrate."));
            EditorGUILayout.LabelField("Audio Source Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.sourceTypeSp,
                new GUIContent("Input Source Type", "Input audio data source type"));
            EditorGUILayout.PropertyField(this.microphoneTypeSp, new GUIContent("Microphone Type",
                "Which microphone API to use when the Source is set to Microphone."));
            EditorGUILayout.PropertyField(this.useMicrophoneTypeFallbackSp, new GUIContent("Use Microphone Type Fallback", "If true, if recording fails to start with Unity microphone type, Photon microphone type is used -if available- as a fallback and vice versa."));

            EditorGUILayout.PropertyField(this.audioClipSp,
                new GUIContent("Audio Clip", "Source audio clip."));
            EditorGUILayout.PropertyField(this.loopAudioClipSp,
                new GUIContent("Loop", "Loop playback for audio clip sources."));
            if (webRtcAudioDspAvailable)
            {
                if (this.voiceDetectionSp.boolValue)
                {
                    EditorGUILayout.HelpBox("It's recommended to use VAD from WebRtcAudioDsp instead of built-in Recorder VAD.", MessageType.Info);
                    if (webRtcAudioDsp.VAD)
                    {
                        EditorGUILayout.HelpBox("WebRtcAudioDsp.VAD is already enabled no need to use the built-in Recorder VAD.", MessageType.Info);
                    }
                }
            }
            EditorGUILayout.PropertyField(this.voiceDetectionSp,
                new GUIContent("Voice Detection", "If true, voice detection enabled."));
            if (this.voiceDetectionSp.boolValue)
            {
                if (webRtcAudioDspAvailable && !webRtcAudioDsp.VAD && GUILayout.Button("Use WebRtcAudioDsp.VAD instead"))
                {
                    this.recorder.VoiceDetection = false;
                    webRtcAudioDsp.VAD = true;
                }
                this.voiceDetectionThresholdSp.floatValue = EditorGUILayout.Slider(
                        new GUIContent("Threshold", "Voice detection threshold (0..1, where 1 is full amplitude)."),
                        this.voiceDetectionThresholdSp.floatValue, 0f, 1f);
                this.voiceDetectionDelayMsSp.intValue =
                    EditorGUILayout.IntField(new GUIContent("Delay (ms)", "Keep detected state during this time after signal level dropped below threshold. Default is 500ms"), this.voiceDetectionDelayMsSp.intValue);
            }

            iosAudioSessionFoldout = EditorGUILayout.Foldout(iosAudioSessionFoldout, new GUIContent("iOS Audio Session"));
            if (iosAudioSessionFoldout)
            {
                //EditorGUILayout.LabelField("iOS Audio Session", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(this.editorAudioSessionPresetSp, new GUIContent("Preset"));
                var preset = (Recorder.EditorIosAudioSessionPreset)Enum.GetValues(typeof(Recorder.EditorIosAudioSessionPreset)).GetValue(this.editorAudioSessionPresetSp.enumValueIndex);
                var custom = preset == Recorder.EditorIosAudioSessionPreset.Custom;
                switch (preset)
                {
                    case Recorder.EditorIosAudioSessionPreset.Game:
                        this.recorder.SetIosAudioSessionParameters(IOS.AudioSessionParametersPresets.Game);
                        break;
                    case Recorder.EditorIosAudioSessionPreset.VoIP:
                        this.recorder.SetIosAudioSessionParameters(IOS.AudioSessionParametersPresets.VoIP);
                        break;
                }
                if (!custom) GUI.enabled = false;
                EditorGUILayout.PropertyField(this.audioSessionParametersSp);
                if (!custom) GUI.enabled = true;

                EditorGUI.indentLevel--;
            }
            //#elif UNITY_ANDROID
            EditorGUILayout.PropertyField(this.androidNativeMicrophoneSettingsSp);
            //#endif

            if (audioChangesHandlerAttached)
            {
                if (GUILayout.Button("Remove Audio Changes Handler component"))
                {
                    DestroyImmediate(audioChangesHandler, true);
                }
            }
            else
            {
                if (GUILayout.Button("Add Audio Changes Handler component"))
                {
                    this.recorder.gameObject.AddComponent<AudioChangesHandler>();
                }
            }

            if (webRtcAudioDspAttached)
            {
                if (GUILayout.Button("Remove WebRTC Audio DSP component"))
                {
                    DestroyImmediate(webRtcAudioDsp, true);
                }
            }
            else
            {
                if (GUILayout.Button("Add WebRTC Audio DSP component"))
                {
                    this.recorder.gameObject.AddComponent<WebRtcAudioDsp>();
                }
            }

            if (Application.isPlaying)
            {
                // Update Recorder in play mode. The values not having immediate effect (are not read repeatedly and do not redstart Recorder) are commented out.
                this.recorder.VoiceDetection = this.voiceDetectionSp.boolValue;
                this.recorder.VoiceDetectionThreshold = this.voiceDetectionThresholdSp.floatValue;
                this.recorder.VoiceDetectionDelayMs = this.voiceDetectionDelayMsSp.intValue;
                this.recorder.InterestGroup = (byte)this.interestGroupSp.intValue;
                this.recorder.DebugEchoMode = this.debugEchoModeSp.boolValue;
                this.recorder.ReliableMode = this.reliableModeSp.boolValue;
                this.recorder.Encrypt = this.encryptSp.boolValue;
                this.recorder.TransmitEnabled = this.transmitEnabledSp.boolValue;
                this.recorder.SamplingRate = GetEnumValueByIndex<SamplingRate>(this.samplingRateSp.enumValueIndex);
                this.recorder.FrameDuration = GetEnumValueByIndex<OpusCodec.FrameDuration>(this.frameDurationSp.enumValueIndex);
                this.recorder.Bitrate = this.bitrateSp.intValue;
                this.recorder.SourceType = GetEnumValueByIndex<Recorder.InputSourceType>(this.sourceTypeSp.enumValueIndex);
                this.recorder.MicrophoneType = GetEnumValueByIndex<Recorder.MicType>(this.microphoneTypeSp.enumValueIndex);
                this.recorder.AudioClip = (AudioClip)this.audioClipSp.objectReferenceValue;
                this.recorder.LoopAudioClip = this.loopAudioClipSp.boolValue;
                this.recorder.RecordingEnabled = this.recordingEnabledSp.boolValue;
                //this.recorder.StopRecordingWhenPaused = this.stopRecordingWhenPausedSp.boolValue;
                //this.recorder.UseMicrophoneTypeFallback = this.useMicrophoneTypeFallbackSp.boolValue;
                //this.recorder.RecordWhenJoined = this.recordWhenJoinedSp.boolValue;
            }

            if (EditorGUI.EndChangeCheck())
            {
                this.serializedObject.ApplyModifiedProperties();
            }
        }

        private static T GetEnumValueByIndex<T>(int i)
        {
            return (T) Enum.GetValues(typeof(T)).GetValue(i);
        }

        bool iosAudioSessionFoldout;
    }
}