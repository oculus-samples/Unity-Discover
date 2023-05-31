// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Utilities;
using UnityEngine;

namespace MRBike
{
    public class TaskHandler : MonoBehaviour
    {
        [AutoSetFromParent(IncludeInactive = true)]
        [SerializeField] private NetworkTaskTracker m_taskTracker;

        private void Start()
        {
            if (m_taskTracker == null)
            {
                m_taskTracker = GetComponentInParent<NetworkTaskTracker>();
            }
        }

        public void TaskComplete(int taskNum)
        {
            if (m_taskTracker == null)
            {
                Debug.LogError($"No {nameof(NetworkTaskTracker)} found, this task on object '{name}' is incomplete");
                return;
            }

            m_taskTracker.TaskComplete(taskNum);
        }
    }
}
