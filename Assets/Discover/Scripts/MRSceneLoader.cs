// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Cysharp.Threading.Tasks;
using Meta.XR.MRUtilityKit;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover
{
    [MetaCodeSample("Discover")]
    public class MRSceneLoader : MonoBehaviour
    {
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
                if (OVRPlugin.hmdPresent && (!Utilities.XRSimulatorInfo.IsSimulatorActivated() || Utilities.XRSimulatorInfo.IsSynthEnvActivated()))
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

        public void OnSceneLoadedSuccess(MRUKRoom room)
        {
            OnSceneLoadedSuccess();
        }
        
        private void OnSceneLoadedSuccess()
        {
            m_sceneLoaded = true;
            _ = m_sceneLoadingTask?.TrySetResult(m_sceneLoaded);
        }

        private void LoadOVRSceneManager()
        {
            MRUK.Instance.RoomCreatedEvent.AddListener(OnSceneLoadedSuccess);
            MRUK.Instance.LoadSceneFromDevice();
        }

        private void LoadFakeRoom()
        {
            _ = Instantiate(m_fakeRoomPrefab);
            OnSceneLoadedSuccess();
        }
    }
}