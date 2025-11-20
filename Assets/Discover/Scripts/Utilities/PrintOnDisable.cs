// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;

namespace Discover.Utilities
{
    [MetaCodeSample("Discover")]
    public class PrintOnDisable : MonoBehaviour
    {
        protected void OnDisable()
        {
            Debug.Log($"Disabling {this}", this);
        }
    }
}
