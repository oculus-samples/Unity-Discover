// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Utilities;
using UnityEngine;

namespace Discover.DroneRage.PowerUps
{
    public class TextureMover : MonoBehaviour
    {
        [SerializeField] private string m_textureName = "_MainTex";
        [SerializeField] private Vector2 m_offsetSpeed = Vector2.zero;
        [SerializeField, AutoSet] private Renderer m_rend;

        private void Update()
        {
            var offset = new Vector2(Time.time * m_offsetSpeed.x, Time.time * m_offsetSpeed.y);
            m_rend.material.SetTextureOffset(m_textureName, offset);
        }
    }
}
