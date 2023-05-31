// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.XR;

namespace Meta.Utilities.Input
{
    public class XRTrackedPoseDriver : TrackedPoseDriver
    {
        [SerializeField] private UnityEvent m_onPerformUpdate = new();

        public event Action InputDataAvailable;

        public int CurrentDataVersion { get; private set; }

        protected override void PerformUpdate()
        {
            base.PerformUpdate();
            CurrentDataVersion += 1;
            InputDataAvailable?.Invoke();
            m_onPerformUpdate.Invoke();
        }

        protected override void OnUpdate()
        {
            PerformUpdate();
        }
    }
}
