// Copyright (c) Meta Platforms, Inc. and affiliates.

using TMPro;
using UnityEngine;

namespace Discover.UI.Modal
{
    public class ModalMessage : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_text;
        public void SetText(string text)
        {
            m_text.text = text;
        }
    }
}
