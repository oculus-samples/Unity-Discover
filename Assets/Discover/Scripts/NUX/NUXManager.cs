// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Meta.Utilities;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover.NUX
{
    [MetaCodeSample("Discover")]
    public class NUXManager : Singleton<NUXManager>
    {
        [SerializeField] private NUXController[] m_nuxControllers;

        private Dictionary<string, NUXController> m_nuxControllerDict = new();

        protected override void InternalAwake()
        {
            foreach (var nuxController in m_nuxControllers)
            {
                m_nuxControllerDict.Add(nuxController.NuxKey, nuxController);
            }
        }

        public void StartNux(string nuxName, Action onNuxCompleted)
        {
            Debug.Log($"[NUXManager] Attempting to  start nux for {nuxName}");
            if (m_nuxControllerDict.TryGetValue(nuxName, out var nuxController))
            {
                Debug.Log($"[NUXManager] Found nux for {nuxName}");
                nuxController.OnNuxCompleted += onNuxCompleted;
                nuxController.StartNux();
            }
        }

        public bool CheckNuxCompleted(string nuxName)
        {
            if (m_nuxControllerDict.TryGetValue(nuxName, out var nuxController))
            {
                var isDone = nuxController.IsCompleted;
                Debug.Log($"[NUXManager] checking nux completed for {nuxName} : {isDone}");
                return isDone;
            }

            Debug.Log($"[NUXManager] nux not found");
            return true;
        }

        [ContextMenu("Reset All Nuxes")]
        public void ResetAllNuxes()
        {
            foreach (var nuxController in m_nuxControllers)
            {
                nuxController.ResetNux();
            }
        }
    }
}