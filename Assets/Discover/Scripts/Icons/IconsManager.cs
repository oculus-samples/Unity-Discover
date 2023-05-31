// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Concurrent;
using UnityEngine;

namespace Discover.Icons
{
    public class IconsManager
    {
        private static IconsManager s_instance;

        private ConcurrentDictionary<string, GameObject> m_iconMap;

        public static IconsManager Instance => s_instance ??= new IconsManager();

        private bool m_iconsEnabled = true;

        private IconsManager() => m_iconMap = new ConcurrentDictionary<string, GameObject>();

        public void RegisterIcon(string appName, GameObject iconObject)
        {
            m_iconMap[appName] = iconObject;
            iconObject.SetActive(m_iconsEnabled);
        }

        public void DeregisterIcon(string appName)
        {
            _ = m_iconMap.TryRemove(appName, out _);
        }

        public bool TryGetIconObject(string appName, out GameObject iconObj)
        {
            return m_iconMap.TryGetValue(appName, out iconObj);
        }

        public void EnableIcons()
        {
            ToggleIcons(true);
        }

        public void DisableIcons()
        {
            ToggleIcons(false);
        }

        private void ToggleIcons(bool enable)
        {
            m_iconsEnabled = enable;
            foreach (var kvp in m_iconMap)
            {
                kvp.Value.SetActive(enable);
            }
        }
    }
}