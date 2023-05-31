// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

namespace Discover.Menus
{
    public class AppControlsMenuObjects : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_appTitle;
        [SerializeField] private TextMeshProUGUI m_appSubtitle;
        [SerializeField] private HapticButton m_closeButton;

        public HapticButton CloseButton => m_closeButton;

        private void Awake()
        {
            Assert.IsNotNull(m_appTitle, $"{nameof(m_appTitle)} cannot be null.");
            Assert.IsNotNull(m_appSubtitle, $"{nameof(m_appSubtitle)} cannot be null.");
            Assert.IsNotNull(m_closeButton, $"{nameof(m_closeButton)} cannot be null.");
        }

        public void SetAppInfo(string title, string subtitle)
        {
            m_appTitle.text = title;
            m_appSubtitle.text = subtitle;
        }
    }
}
