// Copyright (c) Meta Platforms, Inc. and affiliates.

using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

namespace Discover.DroneRage.UI
{
    /// <summary>
    /// Animates the ellipsis of the given text. Uses maxVisibleCharacters to control the ellipsis visibility instead of appending and removing characters.
    /// Usage: Attach to your text or assign the target text in the inspector. Include the ellipsis in your text.
    /// </summary>
    public class AnimateEllipsis : MonoBehaviour
    {


        [SerializeField]
        private TMP_Text m_text;

        [SerializeField]
        private float m_delay = 0.75f;

        [SerializeField]
        private int m_ellipsisLength = 3;

        private float m_deltaTime = 0.0f;

        private void OnEnable()
        {
            if (m_text.text.Length < m_ellipsisLength)
            {
                enabled = false;
                Assert.IsTrue(false, "Ellipsis length must be less than or equal to the text length.");
                return;
            }
            m_text.maxVisibleCharacters = m_text.text.Length - m_ellipsisLength;
        }

        private void Update()
        {
            m_deltaTime += Time.deltaTime;
            if (m_deltaTime >= m_delay)
            {
                m_deltaTime = 0.0f;
                if (m_text.maxVisibleCharacters == m_text.text.Length)
                {
                    m_text.maxVisibleCharacters -= m_ellipsisLength;
                }
                else
                {
                    m_text.maxVisibleCharacters += 1;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_text == null)
            {
                m_text = GetComponentInChildren<TMP_Text>();
            }
        }
#endif
    }
}
