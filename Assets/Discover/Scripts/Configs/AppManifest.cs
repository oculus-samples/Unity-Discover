// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Discover.Icons;
using Meta.XR.MRUtilityKit;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover.Configs
{
    [CreateAssetMenu(menuName = "Discover/App Manifest")]
    [MetaCodeSample("Discover")]
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

        public MRUKAnchor.SceneLabels SurfaceTypeToSceneLabel()
        {
            switch (IconSurfaceType)
            {
                case SurfaceType.ANY:
                    throw new ArgumentOutOfRangeException();
                case SurfaceType.FLOOR:
                    return MRUKAnchor.SceneLabels.FLOOR;
                case SurfaceType.CEILING:
                    return MRUKAnchor.SceneLabels.CEILING;
                case SurfaceType.WALL_FACE:
                    return MRUKAnchor.SceneLabels.WALL_FACE;
                case SurfaceType.TABLE:
                    return MRUKAnchor.SceneLabels.TABLE;
                case SurfaceType.COUCH:
                    return MRUKAnchor.SceneLabels.COUCH;
                case SurfaceType.DOOR_FRAME:
                    return MRUKAnchor.SceneLabels.DOOR_FRAME;
                case SurfaceType.WINDOW_FRAME:
                    return MRUKAnchor.SceneLabels.WINDOW_FRAME;
                case SurfaceType.OTHER:
                    return MRUKAnchor.SceneLabels.OTHER;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}