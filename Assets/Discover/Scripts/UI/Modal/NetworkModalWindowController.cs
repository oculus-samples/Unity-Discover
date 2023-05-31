// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using Meta.Utilities;
using UnityEngine;

namespace Discover.UI.Modal
{
    public class NetworkModalWindowController : Singleton<NetworkModalWindowController>
    {
        [SerializeField] private GameObject m_uiParent;
        [SerializeField] private ModalMessage m_message;
        [SerializeField] private NetworkSelectionMenu m_networkSelectionMenu;
        [SerializeField] private ModalAppSettingsMenu m_settingsPage;

        private bool m_messageActive;
        private bool m_otherActive;

        public void ShowMessage(string text, float hideTime = 3.0f)
        {
            m_message.SetText(text);
            m_messageActive = true;
            m_uiParent.SetActive(true);
            m_message.gameObject.SetActive(true);
            StopCoroutine("HideWindow");
            _ = StartCoroutine(HideWindow(hideTime));
        }

        public void ShowNetworkSelectionMenu(
            Action<string> hostAction, // roomName
            Action<string, bool> joinAction, // roomName, isRemote
            Action<string> onRegionSelected,
            string defaultRoomName = null
        )
        {
            m_networkSelectionMenu
              .Initialize(hostAction, joinAction, ShowSettingsPage, defaultRoomName);
            m_settingsPage.OnNetworkRegionSelected = onRegionSelected;
            m_otherActive = true;
            m_uiParent.SetActive(true);
            m_networkSelectionMenu.gameObject.SetActive(true);
        }

        public void OnSettingsPageClosedButton()
        {
            // we go back to the networkSelection
            m_otherActive = true;
            m_settingsPage.gameObject.SetActive(false);
            m_uiParent.SetActive(true);
            m_networkSelectionMenu.gameObject.SetActive(true);
        }

        public void Hide()
        {
            m_messageActive = false;
            m_otherActive = false;
            m_uiParent.SetActive(false);
            m_message.gameObject.SetActive(false);
            m_networkSelectionMenu.gameObject.SetActive(false);
            m_settingsPage.gameObject.SetActive(false);
        }

        private void ShowSettingsPage()
        {
            m_otherActive = true;
            m_networkSelectionMenu.gameObject.SetActive(false);
            m_uiParent.SetActive(true);
            m_settingsPage.gameObject.SetActive(true);
        }


        private IEnumerator HideWindow(float hideTime)
        {
            yield return new WaitForSeconds(hideTime);
            if (!m_otherActive)
            {
                Hide();
            }
            else if (m_messageActive)
            {
                m_messageActive = false;
                m_message.gameObject.SetActive(false);
            }
        }
    }
}
