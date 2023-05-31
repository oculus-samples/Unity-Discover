// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Discover.UI;
using UnityEngine;
using UnityEngine.Assertions;

namespace Discover.Menus
{
    public class AppListMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject m_appTilePrefab;

        [SerializeField] private GameObject m_appMRTitle;
        [SerializeField] private GameObject m_appVRTitle;
        [SerializeField] private Transform m_appListMR;
        [SerializeField] private Transform m_appListVR;

        private Dictionary<string, GameObject> m_appTiles = new();

        private void Awake()
        {
            Assert.IsNotNull(m_appTilePrefab, $"{nameof(m_appTilePrefab)} cannot be null.");
            Assert.IsNotNull(m_appListMR, $"{nameof(m_appListMR)} cannot be null.");
            Assert.IsNotNull(m_appListVR, $"{nameof(m_appListVR)} cannot be null.");
            m_appMRTitle.SetActive(m_appListMR.childCount > 0);
            m_appVRTitle.SetActive(m_appListVR.childCount > 0);
        }

        public TileButton AddApp(string appID, string appDisplayName, Sprite appIcon, AppType appType)
        {
            Debug.Log($"Creating tile for {(appType == AppType.AR ? "MR" : "VR")} app: '{appDisplayName}'");

            var appList = appType == AppType.AR ? m_appListMR : m_appListVR;
            var appListTitle = appType == AppType.AR ? m_appMRTitle : m_appVRTitle;
            appListTitle.SetActive(true);
            var tileButtonGO = Instantiate(m_appTilePrefab, appList);
            m_appTiles.Add(appID, tileButtonGO);

            var tileButton = tileButtonGO.GetComponentInChildren<TileButton>();
            Assert.IsNotNull(tileButton, $"{m_appTilePrefab.name} must have a component of type {nameof(TileButton)} in its hierarchy.");

            tileButton.Title = appDisplayName;
            tileButton.SourceImage = appIcon;

            return tileButton;
        }
    }
}
