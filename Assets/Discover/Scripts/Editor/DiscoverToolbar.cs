// Copyright (c) Meta Platforms, Inc. and affiliates.

using Cysharp.Threading.Tasks;
using Discover.DroneRage.Enemies;
using Discover.UI.Modal;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityToolbarExtender;

namespace Discover.Editor
{
    public static class DiscoverToolbar
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            ToolbarExtender.LeftToolbarGUI.Add(OnGUI);
        }

        private static string s_roomName;

        private static void OnGUI()
        {
            GUILayout.FlexibleSpace();

            if (Application.isPlaying)
            {
                s_roomName = GUILayout.TextField(s_roomName, GUILayout.ExpandWidth(false), GUILayout.MinWidth(100));

                if (GUILayout.Button("Host", GUILayout.ExpandWidth(false)))
                {
                    DiscoverAppController.Instance.BeginHosting(s_roomName);
                }
                if (GUILayout.Button("Join", GUILayout.ExpandWidth(false)))
                {
                    DiscoverAppController.Instance.BeginJoining(s_roomName, false);
                }
                if (GUILayout.Button("Join Remote", GUILayout.ExpandWidth(false)))
                {
                    DiscoverAppController.Instance.BeginJoining(s_roomName, false);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Bike", GUILayout.ExpandWidth(false)))
                {
                    HostAndLaunchApp("bike");
                }
                if (GUILayout.Button("DroneRage", GUILayout.ExpandWidth(false)))
                {
                    HostAndLaunchApp("dronerage");
                }

                GUILayout.FlexibleSpace();

                var manager = NetworkApplicationManager.Instance;
                using var scope = new EditorGUI.DisabledScope(
                    manager == null || manager.CurrentApplication == null ||
                    !manager.CurrentApplication.isActiveAndEnabled);
                if (GUILayout.Button("Close App", GUILayout.ExpandWidth(false)))
                {
                    manager.CloseApplication();
                }

                if (manager != null && manager.CurrentApplication != null &&
                    manager.CurrentApplication.AppName.Contains("rage") &&
                    GUILayout.Button("Drop Health Orb", GUILayout.ExpandWidth(false)))
                {
                    var enemy = Object.FindObjectOfType<Enemy>();
                    if (enemy != null)
                    {
                        enemy.DropLargeItem(true);
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Discover Scene", GUILayout.ExpandWidth(false)))
                {
                    _ = EditorSceneManager.OpenScene("Assets/Discover/Scenes/Discover.unity");
                }
            }

            GUILayout.FlexibleSpace();
        }

        private static NetworkRunner Runner => DiscoverAppController.Instance.Runner;

        private static async void HostAndLaunchApp(string app)
        {
            if (Runner == null)
            {
                DiscoverAppController.Instance.BeginHosting(s_roomName);
                await UniTask.WaitUntil(() => Runner != null);
            }

            await UniTask.WaitUntil(() => NetworkApplicationManager.Instance != null);

            NetworkApplicationManager.Instance.LaunchApplication(app, NetworkApplicationManager.Instance.transform);

            NetworkModalWindowController.Instance.Hide();
        }
    }
}
