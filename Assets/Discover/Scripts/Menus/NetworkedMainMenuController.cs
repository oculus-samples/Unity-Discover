// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Linq;
using Discover.Networking;
using Fusion;
using Meta.XR.Samples;
using TMPro;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace Discover.Menus
{
    [MetaCodeSample("Discover")]
    public class NetworkedMainMenuController : MonoBehaviour
    {
        private const string HOST_ONLY_CONTROL_MESSAGE =
          "Only the host can control the app settings. \n To toggle functionality on or off, ask the host.";

        [Tooltip("A panel shown to block interacting with the settings UI on clients other than the host.")]
        [SerializeField] private GameObject m_settingsCoverPanel;
        [SerializeField] private TextMeshProUGUI m_settingsCoverMessageText;

        public void Awake()
        {
            Assert.IsNotNull(m_settingsCoverPanel, $"{nameof(m_settingsCoverPanel)} cannot be null.");
            Assert.IsNotNull(m_settingsCoverMessageText, $"{nameof(m_settingsCoverMessageText)} cannot be null.");
        }

        public void OnEnable()
        {
            // disable settings on client headset
            var networkRunner = NetworkRunner.Instances?.FirstOrDefault();
            if (networkRunner == null || !networkRunner.IsMasterClient())
            {
                Debug.Log($"{nameof(NetworkedMainMenuController)}: Turning off settings page for client");
                m_settingsCoverPanel.SetActive(true);
                m_settingsCoverMessageText.text = HOST_ONLY_CONTROL_MESSAGE;
            }
            else
            {
                Debug.Log($"{nameof(NetworkedMainMenuController)}: Host client, settings enabled and cover page hidden");
                m_settingsCoverPanel.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_settingsCoverMessageText == null && m_settingsCoverPanel != null)
            {
                m_settingsCoverMessageText = m_settingsCoverPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }
#endif
    }
}
