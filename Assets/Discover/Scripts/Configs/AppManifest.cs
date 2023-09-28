// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Icons;
using UnityEngine;

namespace Discover.Configs
{
    [CreateAssetMenu(menuName = "Discover/App Manifest")]
    public class AppManifest : ScriptableObject
    {
        public enum SurfaceType
        {
            ANY,
            FLOOR,
            CEILING,
            WALL_FACE,
            TABLE,
            COUCH,
            DOOR_FRAME,
            WINDOW_FRAME,
            OTHER,
            WALL
        }

        [Header("Game Metadata")]
        [Tooltip("The internal name used to identify the app. This should be unique and limited to simple characters.")]
        public string UniqueName;
        [Tooltip("The name as it will be displayed to the user.")]
        public string DisplayName;

        public Sprite Icon;

        [Header("System Integration Behaviour")]
        public AppType DisplayType;

        public SurfaceOrientation IconSurfaceOrientation;
        public SurfaceType IconSurfaceType;

        [Header("Icon Settings")]
        public float IconStartRotation = 0;
        public IconController IconPrefab;
        public GameObject DropIndicator;

        [Header("App Data")]
        public NetworkApplicationContainer AppPrefab;
    }
}