// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.NUX;
using UnityEngine;

namespace Discover.Menus
{
    public class DebugWindow : MonoBehaviour
    {
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
    }
}