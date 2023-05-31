// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;

namespace Discover.UI
{
    public class TabGroup : MonoBehaviour
    {
        [SerializeField] private Color m_tabIdleColor = new Color32(166, 64, 64, 255);
        [SerializeField] private Color m_tabHoverColor = new Color32(166, 17, 17, 255);
        [SerializeField] private Color m_tabActiveColor = new Color32(166, 17, 17, 255);

        private List<TabButton> m_tabButtons;

        private TabButton m_selectedTab;

        public void Subscribe(TabButton button)
        {
            m_tabButtons ??= new List<TabButton>();

            m_tabButtons.Add(button);
            button.SetColor(m_tabIdleColor);
        }

        public void Unsubscribe(TabButton button)
        {
            _ = m_tabButtons?.Remove(button);
        }

        public void OnTabEnter(TabButton button)
        {
            ResetTabs();
            if (m_selectedTab != null || button != m_selectedTab)
            {
                button.SetColor(m_tabHoverColor);
            }
        }

        public void OnTabExit(TabButton button)
        {
            ResetTabs();
        }

        public void OnTabSelected(TabButton button)
        {
            if (m_selectedTab != null)
            {
                m_selectedTab.Deselect();
            }
            m_selectedTab = button;

            m_selectedTab.Select();

            ResetTabs();
            button.SetColor(m_tabActiveColor);
        }

        private void ResetTabs()
        {
            foreach (var button in m_tabButtons)
            {
                if (m_selectedTab != null && button == m_selectedTab)
                {
                    continue;
                }
                button.SetColor(m_tabIdleColor);
            }
        }
    }
}
