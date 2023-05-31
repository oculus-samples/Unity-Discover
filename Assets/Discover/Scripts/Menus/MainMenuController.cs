// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Configs;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Assertions;

namespace Discover.Menus
{
    public class MainMenuController : MonoBehaviour
    {
        private const float TARGET_MENU_RADIUS = 0.3f;

        public delegate void OnTileSelectedHandler(AppManifest appManifest, Handedness handedness);
        public delegate void OnAppMovingHandler(string appName, Handedness handedness);
        public delegate void OnMenuButtonHandler(bool active);

        [Tooltip("Position of the menu relative to the camera")]
        [SerializeField] private Vector3 m_positionRelativeToCamera = new(0.6f, 0.5f, 0.35f);

        [SerializeField] private GameObject m_mainMenuRoot;
        [SerializeField] private Transform m_canvasRoot;
        [SerializeField] private AppListMenuController m_appListMenu;
        [SerializeField] private AppList m_appList;

        // the menu is offset as a child. The parent (this) then snaps to the player as the player moves
        private Vector3 m_targetCameraPosition = Vector3.one;
        private Vector3 m_targetCameraForward = Vector3.forward;
        private Transform m_mainCameraTransform;

        private bool m_menuButtonEnabled = true;

        public static MainMenuController Instance { get; private set; }

        public event OnTileSelectedHandler OnTileSelected;
        public event OnAppMovingHandler OnAppMoving;
        public event OnMenuButtonHandler OnMenuButtonPressed;

        private void Awake()
        {
            Instance ??= this;

            Assert.IsNotNull(m_mainMenuRoot, $"{nameof(m_mainMenuRoot)} cannot be null.");
            Assert.IsNotNull(m_canvasRoot, $"{nameof(m_canvasRoot)} cannot be null.");
            Assert.IsNotNull(m_appListMenu, $"{nameof(m_appListMenu)} cannot be null.");
            Assert.IsNotNull(m_appList, $"{nameof(m_appList)} cannot be null.");
        }

        private void Start()
        {
            // Instead of placing the menu "absolutely," set its local position as an offset. This way, its parent (this.transform) is easier to lerp position/rotation
            // 0.7 m away on z and looking up by 15 degrees -> 0.187 on y
            // put in front of the x-z plane similar to UI
            if (Camera.main != null)
            {
                m_mainCameraTransform = Camera.main.transform;
                var camDirection = m_mainCameraTransform.forward;
                m_mainMenuRoot.transform.localPosition = camDirection * m_positionRelativeToCamera.z
                                                         - new Vector3(0.0f, m_positionRelativeToCamera.y, 0.0f);
            }

            m_mainMenuRoot.transform.LookAt(transform);

            Debug.Log($"{nameof(MainMenuController)}: Populating app list with {m_appList.AppManifests.Count} apps.");
            foreach (var app in m_appList.AppManifests)
            {
                if (!app)
                {
                    Debug.LogError($"Null app manifest in {nameof(m_appList)}.");
                    continue;
                }
                var tile = m_appListMenu.AddApp(app.UniqueName, app.DisplayName, app.Icon, app.DisplayType);
                tile.OnClick.AddListener(handedness => OnTileSelected?.Invoke(app, handedness));
            }

            m_mainMenuRoot.SetActive(false);
        }

        private void Update()
        {
            // if menu is behind player but active, don't hide it; bring to front
            var cameraForward = m_mainCameraTransform.forward;
            var menuBehindPlayer = Vector3.Dot(new Vector3(cameraForward.x, 0, cameraForward.z).normalized, m_targetCameraForward) < 0;

            // check for controller press or hand gesture
            var summonMenu = m_menuButtonEnabled && OVRInput.GetDown(OVRInput.RawButton.Start);
            if (summonMenu)
            {
                if (menuBehindPlayer)
                {
                    SetNewMenuLocation(true);
                }
                else
                {
                    ToggleMenu();
                }
            }

            if (IsMenuActive())
            {
                // only translate if user moves away
                var distToCam = Vector3.Distance(m_mainCameraTransform.position, m_targetCameraPosition);
                if (distToCam > TARGET_MENU_RADIUS)
                {
                    SetNewMenuLocation(false);
                }

                var thisTransform = transform;
                thisTransform.position = Vector3.Lerp(thisTransform.position, m_targetCameraPosition, 0.1f);
                transform.rotation = Quaternion.Lerp(thisTransform.rotation, Quaternion.LookRotation(m_targetCameraForward, Vector3.up), 0.1f);
            }

#if UNITY_EDITOR
            if (Input.GetKeyUp("m"))
            {
                Debug.Log("Menu key pressed");
                ToggleMenu();
            }
#endif
        }

        public void EnableMenuButton(bool value)
        {
            m_menuButtonEnabled = value;
        }

        /// <summary>
        /// Sets a new location for this.transform
        /// </summary>
        /// <remarks>The tablet menu position is already baked in as a local offset</remarks>
        private void SetNewMenuLocation(bool resetOrientation)
        {
            m_targetCameraPosition = m_mainCameraTransform.position;

            m_targetCameraForward = resetOrientation ? m_mainCameraTransform.forward : m_mainMenuRoot.transform.position - m_targetCameraPosition;
            m_targetCameraForward = new Vector3(m_targetCameraForward.x, 0, m_targetCameraForward.z);
            m_targetCameraForward.Normalize();
        }

        public void ToggleMenu()
        {
            if (IsMenuActive())
            {
                m_mainMenuRoot.SetActive(false);
                AppInteractionController.Instance.ToggleSystemInteractorForMenu(false);
                OnMenuButtonPressed?.Invoke(false);
                return;
            }

            Debug.Log("Menu is not active");

            m_mainMenuRoot.SetActive(true);

            m_targetCameraPosition = m_mainCameraTransform.position;
            var camForward = m_mainCameraTransform.forward;
            m_targetCameraForward = new Vector3(camForward.x, 0, camForward.z).normalized;

            transform.position = m_targetCameraPosition;
            transform.rotation = Quaternion.LookRotation(m_targetCameraForward, Vector3.up);

            AppInteractionController.Instance.ToggleSystemInteractorForMenu(true);
            OnMenuButtonPressed?.Invoke(true);
        }

        public void CloseMenu()
        {
            if (IsMenuActive())
            {
                m_mainMenuRoot.SetActive(false);
                AppInteractionController.Instance.ToggleSystemInteractorForMenu(false);
                OnMenuButtonPressed?.Invoke(false);
            }
        }

        public bool IsMenuActive()
        {
            return m_mainMenuRoot.activeSelf;
        }

        public void MoveApp(string appName, Handedness handedness)
        {
            OnAppMoving?.Invoke(appName, handedness);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_appListMenu == null)
            {
                m_appListMenu = GetComponentInChildren<AppListMenuController>(true);
            }

            if (m_canvasRoot == null && m_mainMenuRoot != null)
            {
                var canvas = m_mainMenuRoot.GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                {
                    m_canvasRoot = canvas.transform;
                }
            }
        }
#endif
    }
}
