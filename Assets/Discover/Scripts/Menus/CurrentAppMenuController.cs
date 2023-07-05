// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.ComponentModel;
using Discover.Configs;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Assertions;

namespace Discover.Menus
{
    public class CurrentAppMenuController : MonoBehaviour
    {
        [Description("The game object to de-activate/reactivate when launching an app")]
        [SerializeField] private GameObject m_appMenuPageContainer;
        [SerializeField] private AppControlsMenuObjects m_appControlsMenu;
        [SerializeField] private MainMenuController m_mainMenuController;

        private string m_appName;

        private void Awake()
        {
            Assert.IsNotNull(m_appMenuPageContainer, $"{nameof(m_appMenuPageContainer)} cannot be null.");
            Assert.IsNotNull(m_appControlsMenu, $"{nameof(m_appControlsMenu)} cannot be null.");
            Assert.IsNotNull(m_mainMenuController, $"{nameof(m_mainMenuController)} cannot be null.");

            NetworkApplicationManager.OnInstanceCreated += OnNetworkManagerCreated;
            if (NetworkApplicationManager.Instance != null)
            {
                OnNetworkManagerCreated();
            }
        }

        private void OnDestroy()
        {
            if (NetworkApplicationManager.Instance != null)
            {
                NetworkApplicationManager.Instance.OnAppStarted -= OnAppStarted;
                NetworkApplicationManager.Instance.OnAppClosed -= OnAppClosed;
            }
            NetworkApplicationManager.OnInstanceCreated -= OnNetworkManagerCreated;
        }

        public void OnEnable()
        {
            m_appControlsMenu.CloseButton.OnClick.AddListener(CloseApp);
            if (NetworkApplicationManager.Instance != null &&
                NetworkApplicationManager.Instance.CurrentApplication != null)
            {
                InitializeApp(NetworkApplicationManager.Instance.GetCurrentAppDisplayName());
            }
        }

        public void OnDisable()
        {
            m_appControlsMenu.CloseButton.OnClick.RemoveListener(CloseApp);
        }

        public void CloseApp(Handedness handedness)
        {
            NetworkApplicationManager.Instance.CloseApplication();
        }

        private void InitializeApp(string appName)
        {
            m_appName = appName;
            ToggleAppPageOn();
        }

        private void ToggleAppPageOn()
        {
            m_appMenuPageContainer.SetActive(false);
            m_appControlsMenu.SetAppInfo(m_appName, "");
            m_appControlsMenu.gameObject.SetActive(true);
        }

        private void OnAppStarted(AppManifest appManifest)
        {
            InitializeApp(appManifest.DisplayName);
        }

        private void OnAppClosed()
        {
            ToggleAppPageOff();
        }

        private void ToggleAppPageOff()
        {
            m_appMenuPageContainer.SetActive(true);
            m_appControlsMenu.gameObject.SetActive(false);
        }

        private void OnNetworkManagerCreated()
        {
            NetworkApplicationManager.Instance.OnAppStarted += OnAppStarted;
            NetworkApplicationManager.Instance.OnAppClosed += OnAppClosed;
            if (NetworkApplicationManager.Instance.CurrentApplication != null)
            {
                InitializeApp(NetworkApplicationManager.Instance.GetCurrentAppDisplayName());
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_appControlsMenu == null)
            {
                m_appControlsMenu = GetComponentInChildren<AppControlsMenuObjects>(true);
            }
            if (m_mainMenuController == null)
            {
                m_mainMenuController = GetComponentInParent<MainMenuController>(true);
            }
        }
#endif
    }
}
