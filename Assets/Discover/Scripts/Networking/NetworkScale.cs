// Copyright (c) Meta Platforms, Inc. and affiliates.

using Fusion;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover.Networking
{
    /// <summary>
    /// Syncs the Scale of an object over network
    /// </summary>
    [MetaCodeSample("Discover")]
    public class NetworkScale : NetworkBehaviour
    {
        [Networked(OnChanged = nameof(OnScaleChanged))] public Vector3 Scale { get; set; }

        private void Update()
        {
            if (HasStateAuthority)
            {
                Scale = transform.localScale;
            }
        }

        public static void OnScaleChanged(Changed<NetworkScale> changed)
        {
            changed.Behaviour.transform.localScale = changed.Behaviour.Scale;
        }
    }
}