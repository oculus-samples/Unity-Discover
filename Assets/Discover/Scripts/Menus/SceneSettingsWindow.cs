// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.UI;

namespace Discover.Menus
{
    [MetaCodeSample("Discover")]
    public class SceneSettingsWindow : MonoBehaviour
    {
        [SerializeField] private Toggle m_highlightToggle;
        private void OnEnable()
        {
            m_highlightToggle.SetIsOnWithoutNotify(SceneElementsManager.Instance.IsHighlightOn);
        }

        public void OnHighlightSceneToggled(bool isOn)
        {
            if (isOn)
            {
                SceneElementsManager.Instance.ShowHighlight();
            }
            else
            {
                SceneElementsManager.Instance.HideHighlight();
            }
        }
    }
}