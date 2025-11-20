// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Discover.Networking;
using Discover.NUX;
using Meta.XR.Samples;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Discover.UI.Modal
{
    [MetaCodeSample("Discover")]
    public class ModalAppSettingsMenu : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown m_regionDropdown;

        public Action<string> OnNetworkRegionSelected;

        public UnityEvent<bool> OnShowPlayerIdUpdated;

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

        public void OnShowPlayerIdChanged(bool value)
        {
            OnShowPlayerIdUpdated?.Invoke(value);
        }
    }
}