// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Discover.FakeRoom
{
    /// <summary>
    /// Implementation of the equivalent of the <see cref="OVRSemanticClassification"/> for the fake room
    /// </summary>
    public class FakeRoomClassification : MonoBehaviour
    {
        /// Labels need to be same as <see cref="OVRSceneManager.Classification"/>
        [SerializeField] private List<string> m_labels;

        public IReadOnlyList<string> Labels => m_labels;

        public bool Contains(string label)
        {
            return m_labels.Any(item => item == label);
        }
    }
}