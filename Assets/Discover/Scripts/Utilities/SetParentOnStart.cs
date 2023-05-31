// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.Utilities
{
    public class SetParentOnStart : MonoBehaviour
    {
        public Transform Parent;

        private void Start()
        {
            transform.SetParent(Parent);
        }
    }
}