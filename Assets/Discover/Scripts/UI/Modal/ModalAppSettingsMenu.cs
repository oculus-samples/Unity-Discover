// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Discover.Networking;
using Discover.NUX;
using TMPro;
using UnityEngine;

namespace Discover.UI.Modal
{
    public class ModalAppSettingsMenu : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown m_regionDropdown;

        public Action<string> OnNetworkRegionSelected;

        private void Awake()
        {
            var options = new List<TMP_Dropdown.OptionData>();
            for (RegionMapping.Regions region = 0; region < RegionMapping.Regions.COUNT; ++region)
            {
                options.Add(new TMP_Dropdown.OptionData(RegionMapping.RegionsToName[region]));
            }
            m_regionDropdown.options = options;
        }

        public void OnResetNuxClicked()
        {
            NUXManager.Instance.ResetAllNuxes();
            NetworkModalWindowController.Instance.Hide();
            DiscoverAppController.Instance.ShowNetworkNux();
        }

        public void OnRegionSelected(int selectionIndex)
        {
            var region = (RegionMapping.Regions)selectionIndex;
            OnNetworkRegionSelected?.Invoke(RegionMapping.RegionsToCode[region]);
        }
    }
}