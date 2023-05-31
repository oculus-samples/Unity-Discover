// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Discover.DroneRage.Audio
{
    public class DroneRageAudioManager : MonoBehaviour
    {

        [Range(0, 100)]
        public int HealthTest = 100;
        public bool StartStopMusic = false;
        private bool m_lastStartStopMusic;
        public static DroneRageAudioManager Instance { get; private set; }

        public AnimationCurve HeartLoopCurve;
        public AnimationCurve HeartLoopPitchCurve;

        public AnimationCurve VolumeCurve;

        public AudioSource[] GameStartSfx;
        public AudioSource HeartLoopA;
        public float HeartLoopVolume = 0.7f;
        public AudioSource MusicLoop;
        public float MusicLoopVolume = 0.1f;
        public AudioSource[] EndSfx;
        public AudioSource HealSfx;
        public AudioSource PlayerDeathSfx;

        private Dictionary<AudioSource, float> m_srcVolumes;
        private bool m_isHeartLoopStopped = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            m_srcVolumes = new Dictionary<AudioSource, float>
            {
                { MusicLoop, MusicLoopVolume },
                { HeartLoopA, HeartLoopVolume }
            };

            m_isHeartLoopStopped = false;
            SetHealth(100);
            HeartLoopA.Play();
            StartGameMusic();
        }

        private void OnDisable()
        {
            HeartLoopA.Stop();
            MusicLoop.Stop();
            m_srcVolumes = null;
        }

        public void SetHealth(int health)
        {
            if (health > 0)
            {
                if (!HeartLoopA.isPlaying && isActiveAndEnabled)
                {
                    HeartLoopA.Play();
                    m_isHeartLoopStopped = false;
                }
                HeartLoopA.volume = HeartLoopCurve.Evaluate(health) * m_srcVolumes[HeartLoopA];
                HeartLoopA.pitch = HeartLoopPitchCurve.Evaluate(health);
            }
            else if (!m_isHeartLoopStopped)
            {
                PlayerDeathSfx.Play();
                m_isHeartLoopStopped = true;
                _ = StartCoroutine(Fade(0.2f, HeartLoopA.volume, 0, HeartLoopA));
            }
        }

        public void StartGameMusic()
        {
            foreach (var src in GameStartSfx)
            {
                src.Play();
            }
            _ = StartCoroutine(Fade(5, 0, 1, MusicLoop));
            MusicLoop.Play();
        }

        public void EndGameMusic()
        {
            _ = StartCoroutine(Fade(5, 1, 0, MusicLoop));

            foreach (var src in EndSfx)
            {
                src.Play();
            }
        }

        private IEnumerator Fade(float fadeTime, float startVal, float endVal, AudioSource src)
        {
            var elapsedTime = 0f;
            while (elapsedTime < fadeTime)
            {
                elapsedTime += Time.deltaTime;
                var vol = Mathf.Lerp(startVal, endVal, elapsedTime / fadeTime);
                src.volume = VolumeCurve.Evaluate(vol) * m_srcVolumes[src];
                yield return null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.IsPlaying(this))
            {
                return;
            }

            SetHealth(HealthTest);

            if (StartStopMusic != m_lastStartStopMusic)
            {
                if (StartStopMusic)
                {
                    StartGameMusic();
                }
                else
                {
                    EndGameMusic();
                }
            }
            m_lastStartStopMusic = StartStopMusic;
        }
#endif
    }
}
