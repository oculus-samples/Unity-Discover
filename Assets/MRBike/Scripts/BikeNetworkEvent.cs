// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.Events;

namespace MRBike
{
    public class BikeNetworkEvent : MonoBehaviour
    {
        public UnityEvent NetworkTriggerEvent;

        public void OnEventActivate()
        {
            NetworkTriggerEvent.Invoke();
        }

    }
}
