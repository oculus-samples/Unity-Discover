// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Discover.FakeRoom;
using Fusion;
using Meta.Utilities;
using UnityEngine;

namespace Discover
{
    public class SceneElement : NetworkBehaviour
    {
        private const int MAX_LABEL_COUNT = 5;

        // We simply show a mesh on top. We could include this in the shaders
        [SerializeField] private GameObject m_highlightObject;

        private HashSet<string> m_labels = new();

        [field: SerializeField, AutoSetFromChildren] public Renderer Renderer { get; private set; }

        [Capacity(MAX_LABEL_COUNT)]
        [Networked] private NetworkLinkedList<NetworkString<_16>> Labels { get; }

        public bool IsSpawned { get; private set; }

        private void Awake()
        {
            m_highlightObject.SetActive(false);
            SceneElementsManager.Instance.RegisterElement(this);
        }

        private void OnDestroy()
        {
            if (SceneElementsManager.Instance != null)
            {
                SceneElementsManager.Instance.UnregisterElement(this);
            }
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                InitializeLabels();
            }

            RefreshLabelHash();
            IsSpawned = true;
        }

        public void ShowHighlight()
        {
            m_highlightObject.SetActive(true);
        }

        public void HideHighlight()
        {
            m_highlightObject.SetActive(false);
        }

        public bool ContainsLabel(string label)
        {
            return m_labels.Contains(label);
        }

        private void InitializeLabels()
        {
            Labels.Clear();
#if UNITY_EDITOR
            if (!TryInitializeFromOVR())
            {
                _ = TryInitializeFromFakeRoom();
            }
#else
            _ = TryInitializeFromOVR();
#endif
        }

        private bool TryInitializeFromOVR()
        {
            var classification = GetComponentInParent<OVRSemanticClassification>();
            if (classification == null)
            {
                return false;
            }

            var labels = classification.Labels;
            InitializeLabels(labels);

            return true;
        }

        private bool TryInitializeFromFakeRoom()
        {
            var classification = GetComponentInParent<FakeRoomClassification>();
            if (classification == null)
            {
                return false;
            }

            var labels = classification.Labels;
            InitializeLabels(labels);

            return true;
        }

        private void InitializeLabels(IReadOnlyList<string> labels)
        {
            if (labels.Count > MAX_LABEL_COUNT)
            {
                Debug.LogError($"Scene element has more than {MAX_LABEL_COUNT} labels. Some will be cut");
            }
            for (var i = 0; i < labels.Count && i < MAX_LABEL_COUNT; ++i)
            {
                Labels.Add(labels[i]);
            }
        }

        private void RefreshLabelHash()
        {
            m_labels.Clear();
            foreach (var label in Labels)
            {
                _ = m_labels.Add(label.Value);
            }
        }
    }
}