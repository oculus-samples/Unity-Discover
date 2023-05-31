// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Discover
{
    public class MRSceneLoader : MonoBehaviour
    {
        [SerializeField] private OVRSceneManager m_ovrSceneManager;
        // fake room prefab loaded in editor mode
        [SerializeField] private GameObject m_fakeRoomPrefab;

        private UniTaskCompletionSource<bool> m_sceneLoadingTask;
        private bool m_sceneLoaded;
        public async UniTask<bool> LoadScene()
        {
            if (!m_sceneLoaded)
            {
                m_sceneLoadingTask = new();

#if UNITY_EDITOR
                if (OVRPlugin.hmdPresent && !Utilities.XRSimulatorInfo.IsSimulatorActivated())
                {
                    LoadOVRSceneManager();
                }
                else
                {
                    LoadFakeRoom();
                }
#else
                LoadOVRSceneManager();
#endif
            }

            var task = m_sceneLoadingTask?.Task ?? UniTask.FromResult(false);
            var (timedOut, result) = await task.TimeoutWithoutException(TimeSpan.FromSeconds(5));
            return !timedOut && result;
        }

        public void OnSceneLoadedSuccess()
        {
            m_sceneLoaded = true;
            _ = m_sceneLoadingTask?.TrySetResult(m_sceneLoaded);
        }

        private void LoadOVRSceneManager()
        {
            if (m_ovrSceneManager != null)
            {
                m_ovrSceneManager.SceneModelLoadedSuccessfully += OnSceneLoadedSuccess;
                m_ovrSceneManager.gameObject.SetActive(true);
            }
        }

        private void LoadFakeRoom()
        {
            _ = Instantiate(m_fakeRoomPrefab);
            OnSceneLoadedSuccess();
        }
    }
}