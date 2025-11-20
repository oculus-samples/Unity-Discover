// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using TMPro;
using UnityEngine;

namespace Discover.UI.Modal
{
    [MetaCodeSample("Discover")]
    public class ModalMessage : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_text;
        public void SetText(string text)
        {
            m_text.text = text;
        }
    }
}
