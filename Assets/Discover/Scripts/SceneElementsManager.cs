// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Meta.Utilities;
using Meta.XR.MRUtilityKit;
using Meta.XR.Samples;

namespace Discover
{
    [MetaCodeSample("Discover")]
    public class SceneElementsManager : Singleton<SceneElementsManager>
    {
        private List<SceneElement> m_sceneElements = new();

        public ReadOnlyCollection<SceneElement> SceneElements => m_sceneElements.AsReadOnly();

        public bool IsHighlightOn { get; private set; } = false;

        public bool AreAllElementsSpawned()
        {
            foreach (var element in SceneElements)
            {
                if (!element.IsSpawned)
                {
                    return false;
                }
            }

            return true;
        }

        public void RegisterElement(SceneElement sceneElement)
        {
            m_sceneElements.Add(sceneElement);
            if (IsHighlightOn)
            {
                sceneElement.ShowHighlight();
            }
        }

        public void UnregisterElement(SceneElement sceneElement)
        {
            _ = m_sceneElements.Remove(sceneElement);
        }

        public void ShowHighlight()
        {
            IsHighlightOn = true;
            foreach (var element in m_sceneElements)
            {
                element.ShowHighlight();
            }
        }

        public void HideHighlight()
        {
            IsHighlightOn = false;
            foreach (var element in m_sceneElements)
            {
                element.HideHighlight();
            }
        }

        public IEnumerable<SceneElement> GetElementsByLabel(MRUKAnchor.SceneLabels label) =>
            Instance.SceneElements.Where(e => e.ContainsLabel(label));
    }
}