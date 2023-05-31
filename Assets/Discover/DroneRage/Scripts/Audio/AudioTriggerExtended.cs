// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Discover.DroneRage.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioTriggerExtended : MonoBehaviour
    {
        [Range(1, 20)]
        public int NumVoices = 1;

        private int m_currentVoice = 0;
        private AudioSource m_audioSource = null;
        private List<AudioClip> m_randomAudioClipPool = new();
        private AudioClip m_previousAudioClip = null;
        private List<AudioSource> m_sourceVoices = new();

        // Serialized

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

        [Tooltip("Pitch set here will override the volume set on the attached sound source component.")]
        [SerializeField]
        [Range(-3f, 3f)]
        [Space(10)]
        private float m_pitch = 1f;

        [Tooltip("Check the 'Use Random Range' bool to and adjust the min and max slider values for randomized volume level playback.")]
        [SerializeField]
        private MinMaxPair m_pitchRandomization;

        [Tooltip("True by default. Set to false for sounds to bypass the spatializer plugin. Will override settings on attached audio source.")]
        [SerializeField]
        private MinMaxPair m_startDelayRandomization;

        [Tooltip("start delay set here will randomize the start time in milleseconds between these values")]
        [SerializeField]
        [Space(10)]
        private bool m_spatialize = true;

        [Tooltip("False by default. Set to true to enable looping on this sound. Will override settings on attached audio source.")]
        [SerializeField]
        private bool m_loop = false;

        [Tooltip("100% by default. Sets likelyhood sample will actually play when called")]
        [SerializeField]
        private float m_chanceToPlay = 100;

        [Tooltip("If enabled, audio will play automatically when this gameobject is enabled")]
        [SerializeField]
        private bool m_playOnStart = false;

        protected virtual void Start()
        {
            m_audioSource = gameObject.GetComponent<AudioSource>();

            // Validate that we have audio to play
            if (m_audioClips.Length == 0)
                Debug.LogError($"{this} has no audio clips.", this);

            // Add all audio clips in the populated array into an audio clip list for randomization purposes
            m_randomAudioClipPool.AddRange(m_audioClips);

            // Copy over values from the audio trigger to the audio source
            m_audioSource.volume = m_volume;
            m_audioSource.pitch = m_pitch;
            m_audioSource.spatialize = m_spatialize;
            m_audioSource.loop = m_loop;
            m_audioSource.playOnAwake = false;

            m_sourceVoices.Add(m_audioSource);
            // create duplicate audio sources for polyphony
            // if it's a loop, skip it to avoid stuck voices for now
            if (NumVoices > 1 && m_audioSource.loop == false)
            {
                for (var i = 1; i < NumVoices; i++)
                {
                    var srcToAdd = gameObject.AddComponent<AudioSource>();
                    srcToAdd.volume = m_volume;
                    srcToAdd.pitch = m_pitch;
                    srcToAdd.spatialize = m_spatialize;
                    srcToAdd.spatialBlend = 1;
                    srcToAdd.loop = m_loop;
                    srcToAdd.rolloffMode = m_audioSource.rolloffMode;
                    srcToAdd.minDistance = m_audioSource.minDistance;
                    srcToAdd.maxDistance = m_audioSource.maxDistance;
                    srcToAdd.spatialBlend = m_audioSource.spatialBlend;
                    srcToAdd.playOnAwake = false;
                    srcToAdd.outputAudioMixerGroup = m_audioSource.outputAudioMixerGroup;
                    m_sourceVoices.Add(srcToAdd);
                }
            }

            // Play audio on start if enabled
            if (m_playOnStart)
            {
                PlayAudio();
            }
        }

        public void PlayAudio()
        {
            // Early out if our audio source is disabled
            if (m_currentVoice >= m_sourceVoices.Count || !m_sourceVoices[m_currentVoice].isActiveAndEnabled)
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
                m_sourceVoices[m_currentVoice].volume = Random.Range(m_volumeRandomization.Min, m_volumeRandomization.Max);
            }

            // Check if pitch randomization is set
            if (m_pitchRandomization.UseRandomRange)
            {
                m_sourceVoices[m_currentVoice].pitch = Random.Range(m_pitchRandomization.Min, m_pitchRandomization.Max);
            }

            // If the audio trigger has one clip, play it. Otherwise play a random without repeat clip
            var clipToPlay = m_audioClips.Length == 1 ? m_audioClips[0] : RandomClipWithoutRepeat();
            m_sourceVoices[m_currentVoice].clip = clipToPlay;
            // Check if pitch randomization is set
            float startDelayTime = 0;
            if (m_startDelayRandomization.UseRandomRange)
            {
                startDelayTime = Random.Range(m_startDelayRandomization.Min, m_startDelayRandomization.Max);
            }

            // Play the audio
            m_sourceVoices[m_currentVoice].PlayDelayed(startDelayTime);

            m_currentVoice++;
            m_currentVoice %= NumVoices;
        }

        public void StopAudio()
        {
            foreach (var src in m_sourceVoices)
            {
                if (src.isPlaying && src.loop)
                {
                    src.Stop();
                }
            }
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

    [Serializable]
    public struct MinMaxPair
    {
        [SerializeField]
        private bool m_useRandomRange;

        [SerializeField]
        private float m_min;

        [SerializeField]
        private float m_max;

        public bool UseRandomRange => m_useRandomRange;
        public float Min => m_min;
        public float Max => m_max;
    }
}