// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Discover.DroneRage.Audio
{
    public class DroneRageMusic : MonoBehaviour
    {
        public float MaxVol = 1;
        public AnimationCurve VolumeCurve;
        public AudioSource AmbientLoop;
        public AudioSource ActiveLoop;
        public AudioSource EndSting;

        public bool StartStopMusic;

        public bool IsActive;

        private bool m_lastStartStopMusic;
        private bool m_lastIsActive;

        private Dictionary<AudioSource, float> m_srcVolumes = new();

        private void Start()
        {
            AmbientLoop.volume = 0;
            ActiveLoop.volume = 0;
            EndSting.volume = MaxVol;
            AmbientLoop.Play();
            ActiveLoop.Play();
            m_srcVolumes.Add(AmbientLoop, 0);
            m_srcVolumes.Add(ActiveLoop, 0);
            m_srcVolumes.Add(EndSting, MaxVol);
        }

        // Update is called once per frame
        private void Update()
        {
            if (IsActive != m_lastIsActive)
            {
                if (IsActive && StartStopMusic)
                {
                    SwitchToActive();
                }
                else if (!IsActive && StartStopMusic)
                {
                    SwitchToAmbient();
                }
            }

            if (StartStopMusic != m_lastStartStopMusic)
            {
                if (StartStopMusic)
                {
                    StartMusic();
                }
                else
                {
                    EndMusic();
                }
            }
            m_lastIsActive = IsActive;
            m_lastStartStopMusic = StartStopMusic;
        }

        public void StartMusic()
        {
            StopAllCoroutines();
            _ = StartCoroutine(Fade(4, m_srcVolumes[AmbientLoop], 1, AmbientLoop));
        }

        public void SwitchToAmbient()
        {
            StopAllCoroutines();
            _ = StartCoroutine(Fade(2, m_srcVolumes[AmbientLoop], 1, AmbientLoop));
            _ = StartCoroutine(Fade(4, m_srcVolumes[ActiveLoop], 0, ActiveLoop));
        }

        public void SwitchToActive()
        {
            StopAllCoroutines();
            _ = StartCoroutine(Fade(4, m_srcVolumes[AmbientLoop], 0, AmbientLoop));
            _ = StartCoroutine(Fade(2, m_srcVolumes[ActiveLoop], 1, ActiveLoop));
        }

        public void EndMusic()
        {
            StopAllCoroutines();
            Debug.Log("ending music");
            _ = StartCoroutine(Fade(0.2f, m_srcVolumes[AmbientLoop], 0, AmbientLoop));
            _ = StartCoroutine(Fade(2, m_srcVolumes[ActiveLoop], 0, ActiveLoop));
            EndSting.Play();
            Debug.Log("playing ending");
        }

        private IEnumerator Fade(float fadeTime, float startVal, float endVal, AudioSource src)
        {
            var elapsedTime = 0f;
            while (elapsedTime < fadeTime)
            {
                elapsedTime += Time.deltaTime;
                var vol = Mathf.Lerp(startVal, endVal, elapsedTime / fadeTime);
                src.volume = VolumeCurve.Evaluate(vol) * MaxVol;
                m_srcVolumes[src] = vol;
                yield return null;
            }
        }
    }
}
