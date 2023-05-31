// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using TMPro;
using UnityEngine;

namespace Discover.UI.Modal
{
    public class NetworkSelectionMenu : MonoBehaviour
    {
        [SerializeField] private TMP_InputField m_inputField;

        private Action<string> m_hostAction; // roomName
        private Action<string, bool> m_joinAction; // roomName, isRemote
        private Action m_settingAction;

        public void Initialize(
            Action<string> hostAction, // roomName
            Action<string, bool> joinAction, // roomName, isRemote
            Action settingAction,
            string defaultRoomName = null)
        {
            if (!string.IsNullOrWhiteSpace(defaultRoomName))
            {
                m_inputField.text = defaultRoomName;
            }

            m_hostAction = hostAction;
            m_joinAction = joinAction;
            m_settingAction = settingAction;
        }

        public void OnHostClicked()
        {
            m_hostAction?.Invoke(m_inputField.text);
        }

        public void OnJoinClicked(bool remote)
        {
            m_joinAction?.Invoke(m_inputField.text, remote);
        }

        public void OnSettingsClicked()
        {
            m_settingAction?.Invoke();
        }
    }
}
