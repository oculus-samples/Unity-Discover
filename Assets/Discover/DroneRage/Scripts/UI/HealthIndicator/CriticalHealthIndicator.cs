// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using Discover.DroneRage.Audio;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

namespace Discover.DroneRage.UI.HealthIndicator
{
    public class CriticalHealthIndicator : MonoBehaviour
    {
        [SerializeField]
        private int m_healthTriggerValue = 30;


        [SerializeField]
        private float m_fadeAlpha = 0.5f;

        [SerializeField]
        private float m_fadeFrequency = 0.5f;


        [SerializeField]
        private float m_deathAlpha = 0.2f;

        [SerializeField]
        private float m_deathFlashAlpha = 0.6f;

        [SerializeField]
        private Vector2 m_deathDelayRange = new(0.2f, 2.0f);

        [SerializeField]
        private Vector2 m_deathFlashRange = new(1, 2f);


        [SerializeField]
        private AudioTriggerExtended m_alertSound;


        [SerializeField]
        private SpriteRenderer m_spriteRenderer;


        [SerializeField]
        private Player.Player m_player;

        private Material m_spriteMaterial;
        private bool m_isPlayerDead = false;

        private Coroutine m_flashHealthCoroutine;

        private void Awake()
        {
            FindDependencies();
            Assert.IsNotNull(m_spriteRenderer, $"{nameof(m_spriteRenderer)} cannot be null.");
            m_spriteMaterial = m_spriteRenderer.material;
        }

        private void OnEnable()
        {
            if (m_player == null)
            {
                Debug.LogWarning($"Disabling {nameof(CriticalHealthIndicator)} because no player has been assigned.");
                enabled = false;
                return;
            }

            m_player.OnHpChange += OnHPChanged;
            m_spriteRenderer.enabled = false;
            OnHPChanged();
        }

        private void OnDisable()
        {
            StopFlashHealth();
            if (m_player != null)
            {
                m_player.OnHpChange -= OnHPChanged;
            }
        }

        private void OnHPChanged()
        {
            if (m_player.Health <= 0 && !m_isPlayerDead)
            {
                m_isPlayerDead = true;

                m_spriteRenderer.enabled = true;

                StartFlashHealthDeath();

                return;
            }

            var isHealthCritical = m_player.Health <= m_healthTriggerValue;
            if (isHealthCritical && !m_spriteRenderer.enabled)
            {
                m_spriteRenderer.enabled = true;
                StartFlashHealthCritical();
                if (m_alertSound != null)
                {
                    m_alertSound.PlayAudio();
                }
            }
            else if (!isHealthCritical && m_spriteRenderer.enabled)
            {
                StopFlashHealth();
                m_spriteRenderer.enabled = false;
            }
        }

        private void StopFlashHealth()
        {
            if (m_flashHealthCoroutine != null)
            {
                StopCoroutine(m_flashHealthCoroutine);
                m_flashHealthCoroutine = null;
            }
        }

        private void StartFlashHealthDeath()
        {
            StopFlashHealth();
            m_flashHealthCoroutine = StartCoroutine(
                FlashHealth(
                    m_deathAlpha, m_deathFlashAlpha, 0.15f,
                    Random.Range((int)m_deathFlashRange.x, (int)m_deathFlashRange.y), null,
                    Random.Range(m_deathDelayRange.x, m_deathDelayRange.y), StartFlashHealthDeath));
        }

        private void StartFlashHealthCritical()
        {
            StopFlashHealth();
            m_flashHealthCoroutine = StartCoroutine(
                FlashHealth(1, m_fadeAlpha, m_fadeFrequency, -1, SinEaseFunc));
        }

        private float SinEaseFunc(float value)
        {
            return -(Mathf.Cos(Mathf.PI * value) - 1) / 2;
        }

        private IEnumerator FlashHealth(float from, float to, float duration, int loops,
            Func<float, float> easeFunc = null, float delay = 0, Action callback = null)
        {
            if (delay > 0)
            {
                yield return new WaitForSeconds(delay);
            }

            var loopCount = 0;
            while (loopCount < loops || loops < 0)
            {
                var time = 0f;
                while (time < duration)
                {
                    time += Time.deltaTime;
                    var progress = Mathf.Clamp01(time / duration);
                    var ease = easeFunc == null ? progress : easeFunc(progress);
                    var value = Mathf.Lerp(from, to, ease);
                    var color = m_spriteMaterial.color;
                    color.a = value;
                    m_spriteMaterial.color = color;
                    yield return null;
                }
                loopCount++;
            }
            var finalEase = easeFunc == null ? 1 : easeFunc(1);
            var finalValue = Mathf.Lerp(from, to, finalEase);
            var finalColor = m_spriteMaterial.color;
            finalColor.a = finalValue;
            m_spriteMaterial.color = finalColor;
            callback?.Invoke();
        }

        private void FindDependencies()
        {
            if (m_spriteRenderer == null)
            {
                m_spriteRenderer = GetComponent<SpriteRenderer>();
            }
            m_player ??= GetComponentInParent<Player.Player>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            FindDependencies();
        }
#endif
    }
}
