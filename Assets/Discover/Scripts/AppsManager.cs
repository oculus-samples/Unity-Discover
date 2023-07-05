// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Linq;
using Discover.Configs;
using Discover.Icons;
using Discover.Menus;
using Discover.SpatialAnchors;
using Fusion;
using Meta.Utilities;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Discover
{
    public class AppsManager : Singleton<AppsManager>
    {
        [SerializeField] private MainMenuController m_mainMenuController;

        [SerializeField] private AppIconPlacementController m_iconPlacementController;

        [SerializeField] private IconAnchorNetworked m_iconAnchorPrefab;
        [SerializeField] private AppList m_appList;

        private IconAnchorNetworked m_movingIcon;

        private SpatialAnchorManager<SpatialAnchorSaveData> m_anchorManager;

        public bool CanMoveIcon { get; set; } = false;

        protected override void InternalAwake()
        {
            var anchorDataFileManager = new AnchorJsonFileManager<SpatialAnchorSaveData>("app_anchors.json");
            m_anchorManager = new SpatialAnchorManager<SpatialAnchorSaveData>(anchorDataFileManager);
            m_anchorManager.OnAnchorDataLoadedCreateGameObject += CreateAppIconOnAnchorLoaded;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_mainMenuController.OnTileSelected += OnTileSelected;
            m_mainMenuController.OnAppMoving += OnAppMovingFromMenu;
            m_mainMenuController.OnMenuButtonPressed += OnMenuButtonPressed;

            m_iconPlacementController.OnAppPlaced += OnAppPlaced;
            m_iconPlacementController.OnAppPlacementCanceled += OnAppPlacementCanceled;
        }

        private void OnDisable()
        {
            m_mainMenuController.OnTileSelected -= OnTileSelected;
            m_mainMenuController.OnAppMoving -= OnAppMovingFromMenu;
            m_mainMenuController.OnMenuButtonPressed -= OnMenuButtonPressed;

            m_iconPlacementController.OnAppPlaced -= OnAppPlaced;
            m_iconPlacementController.OnAppPlacementCanceled -= OnAppPlacementCanceled;
        }

        private void OnTileSelected(AppManifest appManifest, Handedness handedness)
        {
            Debug.Log($"[{nameof(AppsManager)}] Tile selected {appManifest.UniqueName}");
            m_mainMenuController.ToggleMenu();
            if (IconsManager.Instance.TryGetIconObject(appManifest.UniqueName, out var iconObj))
            {
                NetworkApplicationManager.Instance.LaunchApplication(appManifest, iconObj.transform);
                return;
            }

            if (AvatarColocationManager.Instance.CanPlaceOrMoveIcons)
            {
                m_iconPlacementController.StartPlacement(appManifest, handedness);
            }
        }

        public void InitializeIcons()
        {
            m_anchorManager.LoadAnchors();
        }

        public void ClearIconsData()
        {
            m_anchorManager.ClearData();
        }

        public void StartMoveApp(IconController iconController, Handedness handedness)
        {
            m_mainMenuController.CloseMenu();
            StartMoveIcon(iconController.GetComponentInParent<IconAnchorNetworked>(), iconController.AppName,
                handedness);
        }

        private void OnAppMovingFromMenu(string appName, Handedness handedness)
        {
            Debug.Log($"[{nameof(AppsManager)}] On App Moving {appName}");
            m_mainMenuController.ToggleMenu();
            if (IconsManager.Instance.TryGetIconObject(appName, out var iconObj))
            {
                StartMoveIcon(iconObj.GetComponentInParent<IconAnchorNetworked>(), appName, handedness);
            }
        }

        private void StartMoveIcon(IconAnchorNetworked iconAnchor, string appName, Handedness handedness)
        {
            m_movingIcon = iconAnchor;
            m_movingIcon.gameObject.SetActive(false);
            var appManifest = m_appList.GetManifestFromName(appName);
            m_iconPlacementController.StartPlacement(appManifest, handedness);
        }

        private void OnMenuButtonPressed(bool active)
        {
            if (active)
            {
                m_iconPlacementController.CancelPlacement();
            }
        }

        private void OnAppPlaced(AppManifest appManifest, Vector3 position, Quaternion rotation)
        {
            if (m_movingIcon != null)
            {
                if (!m_movingIcon.Object.HasStateAuthority)
                {
                    m_movingIcon.Object.RequestStateAuthority();
                }
                var iconTransform = m_movingIcon.transform;
                if (iconTransform.TryGetComponent<OVRSpatialAnchor>(out var anchor))
                {
                    // we don't save right away since we will move and save the icon
                    m_anchorManager.EraseAnchor(anchor, false,
                        (erasedAnchor, success) =>
                        {
                            if (success)
                            {
                                DestroyImmediate(anchor);
                                iconTransform.position = position;
                                iconTransform.rotation = rotation;
                                var newAnchor = m_movingIcon.gameObject.AddComponent<OVRSpatialAnchor>();
                                m_anchorManager.SaveAnchor(newAnchor, new SpatialAnchorSaveData()
                                {
                                    Name = appManifest.UniqueName
                                });
                            }
                            else
                            {
                                Debug.LogError("Failed to erase anchor");
                            }
                            m_movingIcon.gameObject.SetActive(true);
                            m_movingIcon = null;
                        });
                }
                else
                {
                    iconTransform.position = position;
                    iconTransform.rotation = rotation;
                    m_movingIcon.gameObject.SetActive(true);
                    m_movingIcon = null;
                }
            }
            else
            {
                var icon = NetworkRunner.Instances?.FirstOrDefault()?.Spawn(m_iconAnchorPrefab, position, rotation,
                    onBeforeSpawned:
                    (_, instance) =>
                    {
                        // set the app name before spawning
                        instance.GetComponent<IconAnchorNetworked>().AppName = appManifest.UniqueName;
                    });

                var anchor = icon.gameObject.AddComponent<OVRSpatialAnchor>();
                m_anchorManager.SaveAnchor(anchor, new SpatialAnchorSaveData()
                {
                    Name = appManifest.UniqueName
                });
            }
        }

        private void OnAppPlacementCanceled()
        {
            if (m_movingIcon != null)
            {
                m_movingIcon.gameObject.SetActive(true);
                m_movingIcon = null;
            }
            else
            {
                m_mainMenuController.ToggleMenu();
            }
        }

        private GameObject CreateAppIconOnAnchorLoaded(SpatialAnchorSaveData data)
        {
            var icon = NetworkRunner.Instances?.FirstOrDefault()?.Spawn(m_iconAnchorPrefab,
                onBeforeSpawned:
                (_, instance) =>
                {
                    // set the app name before spawning
                    instance.GetComponent<IconAnchorNetworked>().AppName = data.Name;
                });

            return icon.gameObject;
        }
    }
}