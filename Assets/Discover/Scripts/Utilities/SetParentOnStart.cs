// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;

namespace Discover.Utilities
{
    [MetaCodeSample("Discover")]
    public class SetParentOnStart : MonoBehaviour
    {
        public Transform Parent;

        private void Start()
        {
            transform.SetParent(Parent);
        }
    }
}