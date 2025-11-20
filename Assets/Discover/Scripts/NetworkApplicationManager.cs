// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Discover.Configs;
using Discover.Icons;
using Discover.Utilities;
using Fusion;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover
{
    [MetaCodeSample("Discover")]
    public class NetworkApplicationManager : NetworkSingleton<NetworkApplicationManager>
    {
        public static Action OnInstanceCreated;

        [SerializeField] private AppList m_appList;

        public NetworkApplicationContainer CurrentApplication { get; private set; }

        public Action<AppManifest> OnAppStarted;
        public Action OnAppClosed;

        protected override void InternalAwake()
        {
            OnInstanceCreated?.Invoke();
        }

        public string GetCurrentAppDisplayName()
        {
            if (CurrentApplication == null)
            {
                return string.Empty;
            }

            var manifest = m_appList.GetManifestFromName(CurrentApplication.AppName);
            return manifest.DisplayName;
        }

        public void LaunchApplication(string appName, Transform appAnchor)
        {
            var manifest = m_appList.GetManifestFromName(appName);
            LaunchApplication(manifest, appAnchor);
        }

        public void LaunchApplication(AppManifest appManifest, Transform appAnchor)
        {
            if (HasStateAuthority)
            {
                LaunchApplication(appManifest, appAnchor.position, appAnchor.rotation);
            }
            else
            {
                LaunchApplicationOnServerRPC(appManifest.UniqueName, appAnchor.position, appAnchor.rotation);
            }
        }

        public void CloseApplication()
        {
            if (CurrentApplication != null)
            {
                if (HasStateAuthority)
                {
                    StopApplication();
                }
                else
                {
                    StopApplicationOnServerRPC();
                }
            }
        }

        public void OnApplicationStart(NetworkApplicationContainer applicationContainer)
        {
            if (CurrentApplication != null)
            {
                Debug.LogError("There is already an application running");
            }

            CurrentApplication = applicationContainer;

            IconsManager.Instance.DisableIcons();

            OnAppStarted?.Invoke(m_appList.GetManifestFromName(applicationContainer.AppName));
        }

        public void OnApplicationClosed(NetworkApplicationContainer applicationContainer)
        {
            if (CurrentApplication != applicationContainer)
            {
                Debug.LogError("Trying to close a different application than the current one");
            }

            CurrentApplication = null;

            IconsManager.Instance.EnableIcons();
            OnAppClosed?.Invoke();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void LaunchApplicationOnServerRPC(string appName, Vector3 position, Quaternion rotation)
        {
            var manifest = m_appList.GetManifestFromName(appName);
            LaunchApplication(manifest, position, rotation);
        }

        private void LaunchApplication(AppManifest appManifest, Vector3 position, Quaternion rotation)
        {
            if (CurrentApplication != null)
            {
                Debug.LogError($"An Application ({CurrentApplication.AppName}) is already running! " +
                               $"Not starting ({appManifest.DisplayName}) a new one!");
                return;
            }
            _ = Runner.Spawn(appManifest.AppPrefab, position, rotation,
                onBeforeSpawned: (_, obj) =>
                {
                    var app = obj.GetComponent<NetworkApplicationContainer>();
                    app.AppName = appManifest.UniqueName;
                });
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void StopApplicationOnServerRPC()
        {
            StopApplication();
        }

        private void StopApplication()
        {
            if (CurrentApplication != null)
            {
                CurrentApplication.Shutdown();
            }
        }

        [ContextMenu("Launch App 0")]
        private void TestLaunchApp0() => LaunchApplication(m_appList.AppManifests[0], transform);

        [ContextMenu("Launch App 1")]
        private void TestLaunchApp1() => LaunchApplication(m_appList.AppManifests[1], transform);

        [ContextMenu("Launch App 2")]
        private void TestLaunchApp2() => LaunchApplication(m_appList.AppManifests[2], transform);

        [ContextMenu("Close App")]
        private void TestCloseApp() => CloseApplication();
    }
}