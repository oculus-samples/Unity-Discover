// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;

namespace MRBike
{
    public class AffordanceFX : MonoBehaviour
    {
        private enum State
        {
            REST,
            ANIMATE
        }

        private static readonly int s_affordanceColorProperty = Shader.PropertyToID("_AffordanceColor");
        private static readonly int s_kAffordanceProperty = Shader.PropertyToID("_Kaffordance");

        [SerializeField] private bool m_trigger;
        [SerializeField] private Color m_effectColor = new(0.9716f, 0.9517f, 0.5087f, 0f);  // gold
        [SerializeField] private float m_duration = 2;  // reciprocal of duration in sec

        private List<MeshRenderer> m_renderers = new();
        private float m_startTime;

        private State m_fxState = State.REST;
        private float m_finalIntensity = 0;
        private bool m_pastIntensity = false;

        private MaterialPropertyBlock m_propertyBlock;

        private void Awake()
        {
            m_trigger = false;
            m_propertyBlock ??= new MaterialPropertyBlock();

            if (TryGetComponent<MeshRenderer>(out var thisMeshRenderer))
            {
                ApplyColor(thisMeshRenderer);
                m_renderers.Add(thisMeshRenderer);
            }
            var numOfChildren = transform.childCount;
            for (var i = 0; i < numOfChildren; i++)
            {
                var child = transform.GetChild(i);
                var meshRenderer = child.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    ApplyColor(meshRenderer);
                    m_renderers.Add(meshRenderer);
                }
            }
        }

        public void TriggerEffect()
        {
            m_trigger = true;
        }

        public void TriggerEffect(float finalIntensity)
        {
            m_finalIntensity = finalIntensity;
            m_trigger = true;
        }

        public void SetColor(Color color)
        {
            m_effectColor = color;
            foreach (var m in m_renderers)
            {
                ApplyColor(m);
            }
        }

        public void SetIntensity(float intensity)
        {
            m_finalIntensity = intensity;
            ApplyIntensity(intensity);
        }

        private void Update()
        {
            if (m_trigger && m_fxState == State.REST)
            {
                m_startTime = Time.time;
                m_fxState = State.ANIMATE;
            }

            if (m_fxState == State.ANIMATE)
            {
                var delta = Time.time - m_startTime;
                var intensity = ExpImpulse(delta, m_duration);
                if (intensity > m_finalIntensity)
                {
                    m_pastIntensity = true;
                }

                if (m_duration == 0 || (delta > (3 / m_duration) && intensity <= m_finalIntensity && m_pastIntensity))
                {
                    intensity = m_finalIntensity;
                    m_trigger = false;
                    m_fxState = State.REST;
                }

                ApplyIntensity(intensity);
            }
        }

        private void ApplyIntensity(float intensity)
        {
            foreach (var m in m_renderers)
            {
                m.GetPropertyBlock(m_propertyBlock);
                m_propertyBlock.SetFloat(s_kAffordanceProperty, intensity);
                m.SetPropertyBlock(m_propertyBlock);
            }
        }

        private void ApplyColor(MeshRenderer meshRenderer)
        {
            meshRenderer.GetPropertyBlock(m_propertyBlock);
            m_propertyBlock.SetColor(s_affordanceColorProperty, m_effectColor);
            meshRenderer.SetPropertyBlock(m_propertyBlock);
        }

        private static float ExpImpulse(float x, float k)  // returns max at 1/k
        {
            var h = k * x;
            return h * Mathf.Exp(1.0f - h);
        }
    }
}
