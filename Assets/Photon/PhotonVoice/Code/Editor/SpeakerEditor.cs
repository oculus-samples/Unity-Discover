namespace Photon.Voice.Unity.Editor
{
    using UnityEngine;
    using UnityEditor;
    using Unity;

    [CustomEditor(typeof(Speaker), true)]
    public class SpeakerEditor : Editor
    {
        private Speaker speaker;

        #region AnimationCurve

        private AudioSource audioSource;
        private float[] spectrum;
        private void DrawAnimationCurve()
        {
            if (spectrum == null)
            {
                spectrum = new float[128];
            }
            this.audioSource.GetSpectrumData(this.spectrum, 0, FFTWindow.Hanning);
            var curve = new AnimationCurve();

            for (var i = 0; i < this.spectrum.Length; i++)
            {
                curve.AddKey(1.0f / this.spectrum.Length * i, this.spectrum[i]);
            }
            EditorGUILayout.CurveField(curve, Color.green, new Rect(0, 0, 1.0f, 0.1f), GUILayout.Height(64));
        }

        #endregion

        private void OnEnable()
        {
            this.speaker = this.target as Speaker;
            this.audioSource = this.speaker.GetComponent<AudioSource>();
        }

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        public override void OnInspectorGUI()
        {
            this.serializedObject.UpdateIfRequiredOrScript();

            EditorGUI.BeginChangeCheck();

            speaker.PlayDelay = EditorGUILayout.IntField(new GUIContent("Play Delay", "Remote audio stream play delay to compensate packets latency variations."), speaker.PlayDelay);
            speaker.RestartOnDeviceChange = EditorGUILayout.Toggle(new GUIContent("Restart On Device Change", "Restart the Speaker whenever the global audio settings are changed."), speaker.RestartOnDeviceChange);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(this.speaker);
                this.serializedObject.ApplyModifiedProperties();
            }

            if (PhotonVoiceEditorUtils.IsInTheSceneInPlayMode(this.speaker.gameObject))
            {
                EditorGUILayout.LabelField(string.Format("Current Buffer Lag: {0}", this.speaker.Lag));
                this.DrawAnimationCurve();
            }
        }
    }
}