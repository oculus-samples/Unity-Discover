// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Meta.Utilities;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

namespace MRBike
{
    [RequireComponent(typeof(AudioSource))]
    public class BikeAudioTrigger : MonoBehaviour
    {
        [System.Serializable]
        public struct MinMaxPair
        {
            [SerializeField] private bool m_useRandomRange;
            [SerializeField] private float m_min;
            [SerializeField] private float m_max;
            public bool UseRandomRange => m_useRandomRange;
            public float Min => m_min;
            public float Max => m_max;
        }

        [AutoSet]
        [SerializeField] private AudioSource m_audioSource = null;

        [Tooltip("Audio clip arrays with a value greater than 1 will have randomized playback.")]
        [SerializeField]
        private AudioClip[] m_audioClips;
        [Tooltip("Volume set here will override the volume set on the attached sound source component.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float m_volume = 0.7f;
        [Tooltip("Check the 'Use Random Range' bool to and adjust the min and max slider values for randomized volume level playback.")]
        [SerializeField]
        private MinMaxPair m_volumeRandomization;

        [Space(10)]
        [Tooltip("Pitch set here will override the volume set on the attached sound source component.")]
        [SerializeField]
        [Range(-3f, 3f)]
        private float m_pitch = 1f;
        [Tooltip("Check the 'Use Random Range' bool to and adjust the min and max slider values for randomized volume level playback.")]
        [SerializeField]
        private MinMaxPair m_pitchRandomization;

        [Space(10)]
        [Tooltip("True by default. Set to false for sounds to bypass the spatializer plugin. Will override settings on attached audio source.")]
        [SerializeField]
        private bool m_spatialize = true;
        [Tooltip("False by default. Set to true to enable looping on this sound. Will override settings on attached audio source.")]
        [SerializeField]
        private bool m_loop = false;
        [Tooltip("100% by default. Sets likelyhood sample will actually play when called")]
        [SerializeField]
        private float m_chanceToPlay = 100;
        [Tooltip("If enabled, audio will play automatically when this gameObject is enabled")]
        [SerializeField]
        private bool m_playOnStart = false;

        private List<AudioClip> m_randomAudioClipPool = new();
        private AudioClip m_previousAudioClip = null;

        protected virtual void Start()
        {
            // MG 12-17  Audiosource is now set in the editor.
            // _audioSource = gameObject.GetComponent<AudioSource>();
            // Validate that we have audio to play
            Assert.IsTrue(m_audioClips.Length > 0, "An AudioTrigger instance in the scene has no audio clips.");
            // Add all audio clips in the populated array into an audio clip list for randomization purposes
            for (var i = 0; i < m_audioClips.Length; i++)
            {
                m_randomAudioClipPool.Add(m_audioClips[i]);
            }
            // Copy over values from the audio trigger to the audio source
            m_audioSource.volume = m_volume;
            m_audioSource.pitch = m_pitch;
            m_audioSource.spatialize = m_spatialize;
            m_audioSource.loop = m_loop;
            Random.InitState((int)Time.time);
            // Play audio on start if enabled
            if (m_playOnStart)
            {
                PlayAudio();
            }
        }
        public void PlayAudio()
        {



            // Early out if our audio source is disabled
            if (!m_audioSource.isActiveAndEnabled)
            {
                return;
            }
            // Check if random chance is set
            var pick = Random.Range(0.0f, 100.0f);
            if (m_chanceToPlay < 100 && pick > m_chanceToPlay)
            {
                return;
            }
            // Check if volume randomization is set
            if (m_volumeRandomization.UseRandomRange)
            {
                m_audioSource.volume = Random.Range(m_volumeRandomization.Min, m_volumeRandomization.Max);
            }
            // Check if pitch randomization is set
            if (m_pitchRandomization.UseRandomRange)
            {
                m_audioSource.pitch = Random.Range(m_pitchRandomization.Min, m_pitchRandomization.Max);
            }
            // If the audio trigger has one clip, play it. Otherwise play a random without repeat clip
            var clipToPlay = m_audioClips.Length == 1 ? m_audioClips[0] : RandomClipWithoutRepeat();
            m_audioSource.clip = clipToPlay;
            // Play the audio
            m_audioSource.Play();
        }

        /// <summary>
        /// Choose a random clip without repeating the last clip
        /// </summary>
        private AudioClip RandomClipWithoutRepeat()
        {
            var randomIndex = Random.Range(0, m_randomAudioClipPool.Count);
            var randomClip = m_randomAudioClipPool[randomIndex];
            m_randomAudioClipPool.RemoveAt(randomIndex);
            if (m_previousAudioClip != null)
            {
                m_randomAudioClipPool.Add(m_previousAudioClip);
            }
            m_previousAudioClip = randomClip;
            return randomClip;
        }
    }
}
