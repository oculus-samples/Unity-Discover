// Copyright (c) Meta Platforms, Inc. and affiliates.

using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

namespace Discover.DroneRage.UI.WaveCompletionUI
{
    public class TextTypewriterEffect : MonoBehaviour
    {


        [SerializeField]
        private float m_effectTime = 1.0f;

        [SerializeField]
        private float m_startDelay = 0.0f;

        [SerializeField]
        private TMP_Text m_text = null;

        private float m_elapsedTime = 0.0f;
        private float m_totalEffectTime = 0.0f;

        private void Awake()
        {
            Assert.IsNotNull(m_text, $"{m_text} cannot be null.");
            m_totalEffectTime = m_effectTime + m_startDelay;
        }

        private void OnEnable()
        {
            m_text.ForceMeshUpdate();
            m_elapsedTime = 0.0f;
        }

        private void OnDisable()
        {
            m_text.ForceMeshUpdate();
            m_text.maxVisibleCharacters = int.MaxValue;
        }

        private void Update()
        {
            if (m_elapsedTime >= m_totalEffectTime)
            {
                return;
            }

            var dt = Time.deltaTime;
            m_elapsedTime += dt;

            if (m_elapsedTime >= m_totalEffectTime)
            {
                m_text.maxVisibleCharacters = int.MaxValue;
                return;
            }

            var visibleCharacters = Mathf.RoundToInt(Mathf.Max(0.0f, m_elapsedTime - m_startDelay) / m_effectTime * m_text.GetParsedText().Length);
            m_text.maxVisibleCharacters = visibleCharacters;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_text == null)
            {
                m_text = GetComponent<TMP_Text>();
            }
        }
#endif
    }
}
