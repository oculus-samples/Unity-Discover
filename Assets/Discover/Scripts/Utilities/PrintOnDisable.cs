// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.Utilities
{
    public class PrintOnDisable : MonoBehaviour
    {
        protected void OnDisable()
        {
            Debug.Log($"Disabling {this}", this);
        }
    }
}
