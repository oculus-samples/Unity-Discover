// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.UI;

namespace Discover.Icons
{
    public class MoveIconController : MonoBehaviour
    {
        [SerializeField] private Image m_circleFill;

        private void Awake()
        {
            SetFill(0);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetFill(float fill)
        {
            m_circleFill.fillAmount = fill;
        }
    }
}