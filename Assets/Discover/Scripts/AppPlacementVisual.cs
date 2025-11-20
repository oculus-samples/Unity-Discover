// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using TMPro;
using UnityEngine;

namespace Discover
{
    [MetaCodeSample("Discover")]
    public class AppPlacementVisual : MonoBehaviour
    {
        private static readonly int s_isValidPlacementProperty = Shader.PropertyToID("_IsValidPlacement");

        [SerializeField] private GameObject m_visualObject;
        [SerializeField] private GameObject m_transformObject;
        [SerializeField] private MeshRenderer m_puckBase;
        [SerializeField] private MeshRenderer m_puckArrows;
        [SerializeField] private TMP_Text m_messageText;

        private MaterialPropertyBlock m_propertyBlock;
        private static readonly int s_clockwiseHighlightProperty = Shader.PropertyToID("_ClockwiseHighlight");
        private static readonly int s_counterClockwiseHighlightProperty = Shader.PropertyToID("_CounterClockwiseHighlight");

        private void Awake()
        {
            m_propertyBlock ??= new MaterialPropertyBlock();
            m_messageText.gameObject.SetActive(false);
        }

        public GameObject GetTransformObject()
        {
            return m_transformObject;
        }

        public GameObject GetVisualObject()
        {
            return m_visualObject;
        }

        public MeshRenderer GetPuckBase()
        {
            return m_puckBase;
        }

        public MeshRenderer GetPuckArrows()
        {
            return m_puckArrows;
        }

        public void ShowMessage(string message)
        {
            m_messageText.gameObject.SetActive(true);
            m_messageText.text = message;
        }

        public void SetValidPlacement(bool isValid, string invalidMessage = null)
        {
            m_puckBase.GetPropertyBlock(m_propertyBlock);
            m_propertyBlock.SetFloat(s_isValidPlacementProperty, isValid ? 1 : 0);
            m_puckBase.SetPropertyBlock(m_propertyBlock);
            m_puckArrows.enabled = isValid;

            if (!isValid && !string.IsNullOrEmpty(invalidMessage))
            {
                m_messageText.gameObject.SetActive(true);
                m_messageText.text = invalidMessage;
            }
            else
            {
                m_messageText.gameObject.SetActive(false);
            }
        }

        public void UpdateRotationArrows(float clockwiseStrength, float counterClockwiseStrength)
        {
            m_puckArrows.GetPropertyBlock(m_propertyBlock);
            m_propertyBlock.SetFloat(s_clockwiseHighlightProperty, clockwiseStrength);
            m_propertyBlock.SetFloat(s_counterClockwiseHighlightProperty, counterClockwiseStrength);
            m_puckArrows.SetPropertyBlock(m_propertyBlock);
        }
    }
}
