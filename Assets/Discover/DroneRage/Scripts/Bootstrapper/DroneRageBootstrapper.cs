// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Discover.DroneRage.Audio;
using Discover.DroneRage.Game;
using Discover.DroneRage.Scene;
using Discover.DroneRage.UI.EndScreen;
using Discover.DroneRage.Weapons;
using Discover.Networking;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using static Discover.DroneRage.Bootstrapper.DroneRageAppContainerUtils;
using Assert = UnityEngine.Assertions.Assert;

namespace Discover.DroneRage.Bootstrapper
{
    public class DroneRageBootstrapper : MonoBehaviour
    {

        [SerializeField]
        private GameObject m_spawnerPrefab;


        [SerializeField]
        private Material m_skyboxMaterial;


        [SerializeField]
        private Transform m_unAnchoredRoot;


        [SerializeField]
        private EndScreenController m_endScreenUI;


        [SerializeField]
        private DroneRageGameController m_gameControllerPrefab;


        [SerializeField]
        private Color m_skyColor = new(0.754717f, 0.6932604f, 0.60875756f);


        [SerializeField]
        private Color m_equatorColor = new(0.4716981f, 0.44664276f, 0.38937342f);


        [SerializeField]
        private Color m_groundColor = new(0.122641504f, 0.11784824f, 0.10586507f);

        private bool m_isExperienceActive = false;

        private Material m_prevSkybox;
        private LinkedList<Light> m_disabledLights = new();

        private GameObject m_spawner;
        private GameObject m_gameController;

        private void Awake()
        {
            Assert.IsNotNull(m_spawnerPrefab, $"{nameof(m_spawnerPrefab)} cannot be null.");
            Assert.IsNotNull(m_skyboxMaterial, $"{nameof(m_skyboxMaterial)} cannot be null.");
            Assert.IsNotNull(m_unAnchoredRoot, $"{nameof(m_unAnchoredRoot)} cannot be null.");
        }

        private void Start()
        {
            StartExperience();
        }

        private async void StartExperience()
        {
            if (m_isExperienceActive)
            {
                return;
            }

            Debug.Log($"{nameof(DroneRageBootstrapper)}: {nameof(StartExperience)} called");
            m_isExperienceActive = true;

            DroneRageGameController.WhenInstantiated(c => c.OnGameOver += OnGameOver);

            // Adjust scene lighting
            m_prevSkybox = RenderSettings.skybox;
            RenderSettings.skybox = m_skyboxMaterial;
            var lights = FindObjectsOfType<Light>();
            foreach (var light in lights)
            {
                if (light.enabled && light.transform.root == light.transform)
                {
                    light.enabled = false;
                    _ = m_disabledLights.AddLast(light);
                }
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = m_skyColor;
            RenderSettings.ambientEquatorColor = m_equatorColor;
            RenderSettings.ambientGroundColor = m_groundColor;

            DynamicGI.UpdateEnvironment();

            m_unAnchoredRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            m_endScreenUI.gameObject.SetActive(false);


            DroneRageAppLifecycle.Instance.OnAppStarted();
            AppInteractionController.Instance.DisableSystemInteractorForApp();

            if (PhotonNetwork.Runner.IsMasterClient())
            {
                EnvironmentSwapper.Instance.SwapToAltRoomObjects();

                await UniTask.Delay(TimeSpan.FromSeconds(1.0f));

                m_spawner = GetAppContainer().Instantiate(m_spawnerPrefab, transform);
                m_spawner.transform.SetPositionAndRotation(new Vector3(0f, 0.8f, 0f), Quaternion.identity);

                m_gameController = GetAppContainer().NetInstantiate(m_gameControllerPrefab, Vector3.zero, Quaternion.identity).gameObject;
            }
        }

        private void CleanupExperience()
        {
            if (!m_isExperienceActive)
            {
                return;
            }

            Debug.Log($"{nameof(DroneRageBootstrapper)}: {nameof(CleanupExperience)} called");
            m_isExperienceActive = false;

            AppInteractionController.Instance.EnableSystemInteractorForApp();

            DroneRageAppLifecycle.Instance.OnAppExited();

            if (m_endScreenUI != null)
            {
                m_endScreenUI.gameObject.SetActive(false);
            }

            if (DroneRageGameController.Instance != null)
                DroneRageGameController.Instance.OnGameOver -= OnGameOver;

            BulletImpactParticles.DestroyPools();

            // Restore scene lighting
            RenderSettings.skybox = m_prevSkybox;
            foreach (var light in m_disabledLights)
            {
                if (light != null)
                {
                    light.enabled = true;
                }
            }

            m_disabledLights.Clear();

            RenderSettings.ambientMode = AmbientMode.Skybox;

            DynamicGI.UpdateEnvironment();

            if (PhotonNetwork.Runner.IsMasterClient())
            {
                Destroy(m_gameController);
                Destroy(m_spawner);
                EnvironmentSwapper.Instance.SwapToDefaultRoomObjects();
            }
        }

        private void OnDisable()
        {
            CleanupExperience();
        }

        private void OnGameOver(bool victory)
        {
            if (victory)
            {
                m_endScreenUI.ShowWinScreen();
            }
            else
            {
                m_endScreenUI.ShowLoseScreen();
            }

            DroneRageAudioManager.Instance.EndGameMusic();
            AppInteractionController.Instance.EnableSystemInteractorForApp();
        }

        [Button("Enter App")]
        [ContextMenu("Enter App")]
        public void EnterApp()
        {
            EnterApp(transform.position, transform.rotation, transform.localScale);
        }

        public void EnterApp(Vector3 position, Quaternion rotation, float scale)
        {
            EnterApp(position, rotation, Vector3.one * scale);
        }

        public void EnterApp(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Debug.Log($"{nameof(DroneRageBootstrapper)}: {nameof(EnterApp)} called");
            StartExperience();
        }

        public void PauseApp()
        {
            Debug.Log($"{nameof(DroneRageBootstrapper)}: {nameof(PauseApp)} called");
            CleanupExperience();
        }

        public void ExitApp()
        {
            Debug.Log($"{nameof(DroneRageBootstrapper)}: {nameof(ExitApp)} called");
            CleanupExperience();
        }
    }
}