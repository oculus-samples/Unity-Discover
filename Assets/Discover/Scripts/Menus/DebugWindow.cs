// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.NUX;
using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.UI;

namespace Discover.Menus
{
    [MetaCodeSample("Discover")]
    public class DebugWindow : MonoBehaviour
    {
        [SerializeField] private Toggle m_showPlayerIdToggle;

        private void OnEnable()
        {
            m_showPlayerIdToggle.isOn = DiscoverAppController.Instance.ShowPlayerId;
        }

        public void OnClearIconDataClicked()
        {
            AppsManager.Instance.ClearIconsData();
        }

        public void ResetNUX()
        {
            NUXManager.Instance.ResetAllNuxes();
        }

        public void LeaveRoom()
        {
            MainMenuController.Instance.CloseMenu();
            DiscoverAppController.Instance.DisconnectFromRoom();
        }

        public void OnShowPlayerIdChanged(bool value)
        {
            DiscoverAppController.Instance.ShowPlayerId = value;
        }
    }
}