// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Configs;
using Fusion;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover.Icons
{
    [MetaCodeSample("Discover")]
    public class IconAnchorNetworked : NetworkBehaviour
    {
        [SerializeField] private AppList m_appList;

        [Networked] public string AppName { get; set; }

        private AppManifest m_appManifest;

        public override void Spawned()
        {
            m_appManifest = m_appList.GetManifestFromName(AppName);

            if (m_appManifest == null)
            {
                Debug.LogError($"No Manifest found for app {AppName}");
                return;
            }

            var iconController = Instantiate(
                m_appManifest.IconPrefab,
                transform
            );

            iconController.SetApp(AppName);
        }
    }
}