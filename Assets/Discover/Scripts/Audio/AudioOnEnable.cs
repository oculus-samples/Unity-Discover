// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using UnityEngine;

namespace Discover.Audio
{
    public class AudioOnEnable : MonoBehaviour
    {
        [Tooltip("Audio clip to play on Gameobject enable, can be a loop or not")]
        [SerializeField] private AudioSource m_onEnableAudio;

        [Tooltip("Fade in time in seconds")]
        [SerializeField] private float m_fadeIn = 1.0f;

        [Tooltip("Volume, overrides volume set in audiosource for EnableAudio")]
        [SerializeField] private float m_volume = 1.0f;

        private void OnEnable()
        {
            if (m_onEnableAudio == null)
            {
                return;
            }

            if (m_fadeIn > 0)
            {
                m_onEnableAudio.volume = 0;
                _ = StartCoroutine(FadeVolume(0, 1, m_fadeIn));
            }
            m_onEnableAudio.Play();
        }

        private IEnumerator FadeVolume(float start, float end, float duration)
        {
            float vol;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var normalizedTime = t / duration;
                vol = Mathf.Lerp(start, end, normalizedTime);
                m_onEnableAudio.volume = m_volume * vol;
                yield return null;
            }
            vol = end;
            m_onEnableAudio.volume = m_volume * vol;
            if (vol == 0)
            {
                m_onEnableAudio.Stop();
            }
        }
    }
}
