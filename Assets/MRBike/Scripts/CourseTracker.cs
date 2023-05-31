// Copyright (c) Meta Platforms, Inc. and affiliates.

using Fusion;
using Meta.Utilities;
using UnityEngine;

namespace MRBike
{
    public class CourseTracker : NetworkBehaviour
    {
        [SerializeField] private AffordanceFX[] m_markers;
        [SerializeField] private Color m_onColor = new(0.514122f, 0.972549f, 0.5098039f, 0.0f);
        [SerializeField] private float m_onIntensity = 0.373f;
        [AutoSet]
        [SerializeField] private AffordanceFX m_affordanceFX;

        private int m_progress = 0;

        [Capacity(20)] // Capacity needs to change if m_markers is larger
        [Networked]
        private NetworkArray<NetworkBool> TasksCompleted { get; }

        private bool[] m_localTaskCompleted;

        public override void Spawned()
        {
            Debug.Assert(m_markers.Length <= TasksCompleted.Length);
            for (var i = 0; i < m_markers.Length; ++i)
            {
                m_localTaskCompleted[i] = TasksCompleted[i];
                if (TasksCompleted[i])
                {
                    m_markers[i].SetColor(m_onColor);
                    m_markers[i].SetIntensity(m_onIntensity);
                    m_progress++;
                }
            }
        }

        public void CompleteTask(int taskNum)
        {
            if (m_localTaskCompleted[taskNum])
            {
                return;
            }

            m_localTaskCompleted[taskNum] = true;
            _ = TasksCompleted.Set(taskNum, true);

            var marker = m_markers[taskNum];
            marker.SetColor(m_onColor);
            marker.TriggerEffect(m_onIntensity);
            m_progress++;
            if (m_progress >= m_markers.Length)
            {
                ProgramComplete();
            }
        }

        private void ProgramComplete()
        {
            m_affordanceFX.TriggerEffect(m_onIntensity);
        }

        private void Awake()
        {
            m_localTaskCompleted = new bool[m_markers.Length];
        }
    }
}
