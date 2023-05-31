// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

namespace Discover.DroneRage.UI.WaveCompletionUI
{
    public class TextGlitchEffect : MonoBehaviour
    {


        [SerializeField]
        private float m_effectTime = 1.0f;

        [SerializeField]
        private float m_startDelay = 0.0f;

        [SerializeField]
        private float m_effectCharacterDelay = 0.1f;

        [SerializeField]
        private float m_effectIntensity = 1.0f;

        [SerializeField]
        private string m_glitchCharacters = "!@#$%&[]_01";

        [SerializeField]
        private string m_ignoredCharacters = "> ";

        [SerializeField]
        private TMP_Text m_text = null;

        private string m_originalString = "";
        private string m_originalParsedString = "";
        private float m_totalTime = 0.0f;
        private float m_characterTime = 0.0f;

        private void Awake()
        {
            Assert.IsNotNull(m_text, $"{m_text} cannot be null.");
        }

        private void OnEnable()
        {
            m_originalString = m_text.text;
            m_text.ForceMeshUpdate();
            m_originalParsedString = m_text.GetParsedText();
            m_totalTime = 0.0f;
            m_characterTime = 0.0f;

            var sb = new StringBuilder(m_text.GetParsedText());

            for (var i = 0; i < m_originalParsedString.Length; ++i)
            {
                if (m_ignoredCharacters.Contains(m_originalParsedString[i]))
                {
                    continue;
                }

                var glitchIndex = Random.Range(0, m_originalParsedString.Length);
                var glitchCharacter = glitchIndex < m_glitchCharacters.Length ? m_glitchCharacters[glitchIndex] : m_originalParsedString[i];
                sb[i] = glitchCharacter;
            }

            // Preserve markup
            m_text.text = m_originalString.Replace(m_originalParsedString, sb.ToString());
        }

        private void OnDisable()
        {
            m_text.text = m_originalString;
        }

        private void Update()
        {
            if (m_totalTime >= m_effectTime + m_startDelay)
            {
                return;
            }

            var dt = Time.deltaTime;
            m_totalTime += dt;
            m_characterTime += dt;

            if (m_totalTime >= m_effectTime + m_startDelay)
            {
                m_text.text = m_originalString;
                return;
            }

            if (m_totalTime < m_startDelay)
            {
                return;
            }

            if (m_characterTime >= m_effectCharacterDelay)
            {
                UpdateText();
                m_characterTime = 0.0f;
            }
        }

        private void UpdateText()
        {
            var sb = new StringBuilder(m_text.GetParsedText());

            var totalEffectTime = m_startDelay + m_effectTime;

            var numFixedCharacters = Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(m_totalTime / totalEffectTime, m_effectIntensity) * m_originalParsedString.Length), 0, m_originalParsedString.Length);
            var numGlitchCharacters = Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(1.0f / m_totalTime / totalEffectTime, m_effectIntensity)), 0, m_originalParsedString.Length - numFixedCharacters);

            for (var i = 0; i < numGlitchCharacters; ++i)
            {
                var index = Random.Range(numFixedCharacters, m_originalParsedString.Length - 1);
                if (m_ignoredCharacters.Contains(m_originalParsedString[index]))
                {
                    continue;
                }

                var glitchIndex = Random.Range(0, m_originalParsedString.Length);
                var glitchCharacter = glitchIndex < m_glitchCharacters.Length ? m_glitchCharacters[glitchIndex] : m_originalParsedString[index];
                sb[index] = glitchCharacter;
            }

            for (var i = 0; i < numFixedCharacters; ++i)
            {
                sb[i] = m_originalParsedString[i];
            }

            // Preserve markup
            m_text.text = m_originalString.Replace(m_originalParsedString, sb.ToString());
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
