// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Discover.FakeRoom;
using Fusion;
using Meta.Utilities;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace Discover
{
    public class SceneElement : NetworkBehaviour
    {
        // We simply show a mesh on top. We could include this in the shaders
        [SerializeField] private GameObject m_highlightObject;

        [field: SerializeField, AutoSetFromChildren] public Renderer Renderer { get; private set; }
        
        [Networked] private MRUKAnchor.SceneLabels Label { set; get; }

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
                InitializeLabel();
            }

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

        public bool ContainsLabel(MRUKAnchor.SceneLabels label)
        {
            return label == Label;
        }

        private void InitializeLabel()
        {
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
            var classificationObj = GetComponentInParent<MRUKAnchor>();
            if (classificationObj == null)
            {
                return false;
            }

            Label = classificationObj.Label;

            return true;
        }

        private bool TryInitializeFromFakeRoom()
        {
            var classification = GetComponentInParent<FakeRoomClassification>();
            if (classification == null)
            {
                return false;
            }

            Label = classification.Label;

            return true;
        }
    }
}