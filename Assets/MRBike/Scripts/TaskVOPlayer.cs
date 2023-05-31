// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MRBike
{
    public class TaskVOPlayer : MonoBehaviour
    {
        [SerializeField] private float m_volume = 1.0f;
        [SerializeField] private AudioClip[] m_clips;
        [SerializeField] private AudioSource m_audioSource;

        [SerializeField] private AudioClip m_finalClip;
        [SerializeField] private float m_finalClipDelay = 1;

        [SerializeField] private int[] m_groupIndexes;
        [SerializeField] private int m_groupFinishedClip;

        [SerializeField] private bool m_playOnAwake = false;
        [SerializeField] private UnityEvent m_onGroupComplete;

        [SerializeField] private TMP_Text m_debugText;
        [SerializeField] private float m_voDelay = 0.5f;
        [SerializeField] private TaskHandler m_taskHandler;

        private bool[] m_played;
        private int m_clipsPlayed = 0;

        public void Play(int clip)
        {
            if (m_audioSource.isPlaying)
            {
                m_audioSource.Stop();
            }

            m_audioSource.PlayOneShot(m_clips[clip], m_volume);
        }

        public void PlayOnce(int clip)
        {
            if (clip is not 0 and not 1)
            {
                if (!m_played[1])
                {
                    m_played[1] = true;
                    m_clipsPlayed++;
                }
            }

            if (clip == 0)
            {
                _ = StartCoroutine(PlayDelayed(m_clips[0].length, 1));
            }

            if (m_played[clip]) return;

            var delay = m_clips[clip].length;

            if (m_audioSource)
            {
                if (m_audioSource.isPlaying)
                {
                    m_audioSource.Stop();
                }

                m_audioSource.PlayOneShot(m_clips[clip], m_volume);
                m_played[clip] = true;
                m_clipsPlayed++;
            }

            if (CheckForGroupComplete(clip))
            {
                delay += m_clips[m_groupFinishedClip].length;
            }

            if (m_clipsPlayed == m_clips.Length)
            {
                _ = StartCoroutine(PlayFinal(delay + m_finalClipDelay));
            }
            if (m_debugText)
                m_debugText.text = m_clipsPlayed.ToString();
        }

        private bool CheckForGroupComplete(int clip)
        {
            if (m_groupIndexes.Length == 0) return false;

            if (!m_played[m_groupIndexes[0]] || !m_played[m_groupIndexes[1]]) return false;

            if (!m_played[m_groupFinishedClip])
            {
                m_onGroupComplete.Invoke();
                _ = StartCoroutine(PlayDelayed(m_clips[clip].length, m_groupFinishedClip));
                return true;
            }
            return false;
        }

        public void PlayOnceDelayed(int clip)
        {
            if (m_played[clip]) return;
            _ = StartCoroutine(PlayDelayed(m_voDelay, clip));
        }

        private IEnumerator PlayDelayed(float delay, int clip)
        {
            float timer = 0;
            while (timer < delay)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (m_taskHandler)
            {
                m_taskHandler.TaskComplete(clip);
            }

            PlayOnce(clip);
        }

        private IEnumerator PlayFinal(float delay)
        {
            float timer = 0;
            while (timer < delay)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            m_audioSource.PlayOneShot(m_finalClip, m_volume);
        }

        private void Awake()
        {
            m_played = new bool[m_clips.Length];
            for (var x = 0; x < m_clips.Length; x++)
            {
                m_played[x] = false;
            }

            if (m_playOnAwake)
            {
                PlayOnce(0);

                if (m_taskHandler)
                {
                    m_taskHandler.TaskComplete(0);
                }
                _ = StartCoroutine(PlayDelayed(m_clips[0].length, 1));
            }
        }

        private bool CheckForComplete()
        {
            return m_clipsPlayed >= m_clips.Length;
        }
    }
}
