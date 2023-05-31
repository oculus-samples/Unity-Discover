// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using TMPro;
using UnityEngine;

namespace Discover.UI.Taskbar
{
    public class TimeTextUpdater : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_timeText;

        private DateTime m_currentTime;
        private int m_currentMin;

        private void Start()
        {
            UpdateTime(DateTime.Now);
        }

        private void Update()
        {
            if (m_currentMin == DateTime.Now.Minute)
            {
                return;
            }
            UpdateTime(DateTime.Now);
        }

        private void UpdateTime(DateTime updateTime)
        {
            if (m_timeText == null)
            {
                return;
            }
            m_currentTime = updateTime;
            m_currentMin = m_currentTime.Minute;
            m_timeText.SetText(Get12HourTimeString(m_currentTime));
        }

        private string Get12HourTimeString(DateTime time)
        {
            return time.Hour < 12 ? string.Format("{0:hh:mm} AM", time) : string.Format("{0:hh:mm} PM", time);
        }
    }
}
