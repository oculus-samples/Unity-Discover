// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Meta.XR.Samples;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Discover.UI.Taskbar
{
    [MetaCodeSample("Discover")]
    public class BatteryStatusUpdater : MonoBehaviour
    {
        [Serializable]
        private struct LevelData
        {
            public Sprite Icon;
            public Color IconColor;
            [Range(0, 100)]
            public int MaxPercentage;
        }

        [SerializeField] private Image m_iconImage;
        [SerializeField] private TMP_Text m_percentageText;

        [SerializeField] private float m_updateFrequencySec;

        [SerializeField] private List<LevelData> m_levelData;

        private int m_currentLevel = -1;

        private float m_timer;

        private void Awake()
        {
            OrderLevelData();
            UpdateBatteryStatus();
        }

        private void OnEnable()
        {
            UpdateBatteryStatus();
        }

        private void Update()
        {
            if (m_updateFrequencySec <= 0)
            {
                return;
            }

            m_timer += Time.deltaTime;
            if (m_timer >= m_updateFrequencySec)
            {
                UpdateBatteryStatus();
            }
        }

        private void UpdateBatteryStatus()
        {
            // batteryLevel is float [0-1]
            var batteryLevel = Mathf.RoundToInt(SystemInfo.batteryLevel * 100);
            if (m_percentageText != null)
            {
                m_percentageText.text = $"{batteryLevel}%";
            }

            // find the current level. m_levelData is sorted in the awake function
            for (var i = 0; i < m_levelData.Count; ++i)
            {
                var levelData = m_levelData[i];
                if (batteryLevel <= levelData.MaxPercentage)
                {
                    if (i != m_currentLevel)
                    {
                        ApplyLevelData(levelData);
                        m_currentLevel = i;
                        break;
                    }
                }
            }

            m_timer = 0;
        }

        private void ApplyLevelData(LevelData levelData)
        {
            if (m_iconImage != null)
            {
                m_iconImage.sprite = levelData.Icon;
                m_iconImage.color = levelData.IconColor;
            }
        }

        private static int OrderLevelData(LevelData level1, LevelData level2)
        {
            return level1.MaxPercentage - level2.MaxPercentage;
        }

        [ContextMenu("Order Level Data")]
        private void OrderLevelData()
        {
            m_levelData.Sort(OrderLevelData);
        }
    }
}