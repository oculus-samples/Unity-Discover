// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Utilities;
using UnityEngine;

namespace Discover.Utilities
{
    public class MainLightForBuiltInShaders : MonoBehaviour
    {
        [SerializeField, AutoSet] private Transform m_transform;
        [SerializeField, AutoSet] private Light m_light;

        private static readonly int s_worldSpaceLightPos0 = Shader.PropertyToID("_WorldSpaceLightPos0");
        private static readonly int s_lightColor0 = Shader.PropertyToID("_LightColor0");

        private void Update()
        {
            var lightPos = -m_transform.localToWorldMatrix.GetColumn(2);
            Shader.SetGlobalVector(s_worldSpaceLightPos0, new(lightPos.x, lightPos.y, lightPos.z, 0));
            Shader.SetGlobalVector(s_lightColor0, m_light.color * m_light.intensity);
        }
    }
}