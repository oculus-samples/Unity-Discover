// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace MRBike
{
    public class ColorIndicator : MonoBehaviour
    {
        private static readonly int s_affordanceColorProperty = Shader.PropertyToID("_AffordanceColor");
        private static readonly int s_kAffordanceProperty = Shader.PropertyToID("_Kaffordance");

        [SerializeField] private bool m_active = false;

        [SerializeField] private Color m_onColor = Color.red;
        [SerializeField] private Color m_offColor = Color.black;
        [SerializeField] private MeshRenderer[] m_renderers;
        [SerializeField] private float m_throbSpeed = 1;

        private MaterialPropertyBlock m_propertyBlock;

        private float m_newAffordance = 0;

        private void Awake()
        {
            m_propertyBlock ??= new MaterialPropertyBlock();
        }

        public void Activate()
        {
            m_active = true;
            foreach (var r in m_renderers)
            {
                SetColorAndAffordance(r, m_offColor, 0);
            }
        }

        public void Deactivate()
        {
            m_active = false;
            foreach (var r in m_renderers)
            {
                SetColorAndAffordance(r, m_offColor, 0);
            }
        }

        private void Update()
        {
            if (!m_active) return;

            var sin = Mathf.Sin(m_throbSpeed * Time.time);
            sin = (sin + 1) / 2.0f;
            m_newAffordance = sin;
            foreach (var r in m_renderers)
            {
                var newColor = Color.Lerp(m_onColor, m_offColor, m_newAffordance);
                SetColorAndAffordance(r, newColor, m_newAffordance);
            }

        }

        private void SetColorAndAffordance(Renderer rendererToUpdate, Color color, float affordance)
        {
            rendererToUpdate.GetPropertyBlock(m_propertyBlock);
            m_propertyBlock.SetColor(s_affordanceColorProperty, color);
            m_propertyBlock.SetFloat(s_kAffordanceProperty, affordance);
            rendererToUpdate.SetPropertyBlock(m_propertyBlock);
        }
    }
}
