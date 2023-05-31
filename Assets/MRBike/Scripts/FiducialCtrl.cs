// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Utilities;
using UnityEngine;

namespace MRBike
{
    /// <summary>
    /// Adjust the skinned mesh blend shape based on the updated height value
    /// </summary>
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class FiducialCtrl : MonoBehaviour
    {
        [SerializeField] private float m_startHeight = 0;

        [AutoSet]
        [SerializeField] private SkinnedMeshRenderer m_skinnedMesh;

        private float m_currentHeight;
        public float Height
        {
            get => m_currentHeight;
            set => UpdateHeight(value);
        }

        private void Start()
        {
            UpdateHeight(m_startHeight);
        }

        private void UpdateHeight(float height)
        {
            m_skinnedMesh.SetBlendShapeWeight(0, height);
            m_currentHeight = height;
        }
    }
}
