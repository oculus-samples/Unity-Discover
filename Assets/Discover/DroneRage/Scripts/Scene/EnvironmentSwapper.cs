// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Linq;
using Cysharp.Threading.Tasks;
using Fusion;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace Discover.DroneRage.Scene
{
    public class EnvironmentSwapper : NetworkBehaviour
    {
        private static readonly int s_visibility = Shader.PropertyToID("_Visibility");

        public static EnvironmentSwapper Instance { get; private set; }

        [SerializeField] private Material[] m_altWallMaterials;

        private Material[] m_originalWallMaterials;

        [Networked(OnChanged = nameof(OnIsSetToAltChanged))]
        private NetworkBool IsSetToAlt { get; set; } = false;

        private void Awake()
        {
            if (null != Instance)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SwapToAltRoomObjects()
        {
            IsSetToAlt = true;
        }

        public void SwapToDefaultRoomObjects()
        {
            IsSetToAlt = false;
        }

        private static void OnIsSetToAltChanged(Changed<EnvironmentSwapper> changed)
        {
            var toAlt = changed.Behaviour.IsSetToAlt;
            changed.Behaviour.SwapCeiling(toAlt);
            changed.Behaviour.SwapWalls(toAlt);
        }

        private async void SwapWalls(bool toAlt)
        {
            await UniTask.WaitUntil(() => SceneElementsManager.Instance.AreAllElementsSpawned());
            foreach (var wall in SceneElementsManager.Instance.GetElementsByLabel(MRUKAnchor.SceneLabels.WALL_FACE))
            {
                if (toAlt)
                {
                    m_originalWallMaterials ??= wall.Renderer.sharedMaterials;
                    wall.Renderer.sharedMaterials = m_altWallMaterials;
                }
                else
                {
                    wall.Renderer.sharedMaterials = m_originalWallMaterials;
                }
            }
        }

        private async void SwapCeiling(bool toAlt)
        {
            await UniTask.WaitUntil(() => SceneElementsManager.Instance.AreAllElementsSpawned());
            var ceiling = SceneElementsManager.Instance.
                GetElementsByLabel(MRUKAnchor.SceneLabels.CEILING).
                FirstOrDefault()?.
                Renderer;
            if (ceiling == null)
                return;

            var block = new MaterialPropertyBlock();
            block.SetFloat(s_visibility, toAlt ? 0 : 1);
            ceiling.SetPropertyBlock(block);
        }
    }
}