// Copyright (c) Meta Platforms, Inc. and affiliates.

using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

namespace Discover.DroneRage.UI.WaveCompletionUI
{
    public class TextSync : MonoBehaviour
    {


        [SerializeField]
        private TMP_Text m_source = null;

        [SerializeField]
        private TMP_Text m_destination = null;

        private void Awake()
        {
            Assert.IsNotNull(m_source, $"{m_source} cannot be null.");
            Assert.IsNotNull(m_destination, $"{m_destination} cannot be null.");
        }

        private void OnEnable()
        {
            UpdateText();
        }

        private void LateUpdate()
        {
            UpdateText();
        }

        private void UpdateText()
        {
            if (m_destination.text != m_source.text)
            {
                m_destination.text = m_source.text;
            }
            if (m_destination.maxVisibleCharacters != m_source.maxVisibleCharacters)
            {
                m_destination.maxVisibleCharacters = m_source.maxVisibleCharacters;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_source == null)
            {
                m_source = GetComponent<TMP_Text>();
            }
        }
#endif
    }
}
