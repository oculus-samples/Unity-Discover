// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.Linq;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace Discover.FakeRoom
{
    /// <summary>
    /// Implementation of the equivalent of the <see cref="MRUKAnchor"/> for the fake room
    /// </summary>
    public class FakeRoomClassification : MonoBehaviour
    {
        [SerializeField] private MRUKAnchor.SceneLabels m_label;

        public MRUKAnchor.SceneLabels Label => m_label;
    }
}