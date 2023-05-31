// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;

namespace MRBike
{
    public class BikeObjectVisibilityManager : MonoBehaviour
    {
        private Dictionary<int, BikeVisibleObject> m_bikeParts = new();

        public void RegisterVisibleObject(BikeVisibleObject visibleObject, int partNum)
        {
            try
            {
                m_bikeParts.Add(partNum, visibleObject);
            }
            catch
            {
                Debug.Log($"[Bike] -- REGISTRATION FAILED -- {visibleObject.name} | part: {partNum}");
            }
        }


        public void HideNetworkObject(int partNum)
        {
            if (m_bikeParts.TryGetValue(partNum, out var bikePart))
            {
                bikePart.Hide();
            }
        }

        public void ShowNetworkObject(int partNum)
        {
            if (m_bikeParts.TryGetValue(partNum, out var bikePart))
            {
                bikePart.Show();
            }
        }

        public void RotatorGrabNetworkObject(int partNum)
        {
            if (m_bikeParts.TryGetValue(partNum, out var bikePart))
            {
                bikePart.RotatorGrab();
            }
        }

        public void RotatorReleaseNetworkObject(int partNum)
        {
            if (m_bikeParts.TryGetValue(partNum, out var bikePart))
            {
                bikePart.RotatorRelease();
            }
        }

        public void SendNetworkTrigger(int partNum)
        {
            if (m_bikeParts.TryGetValue(partNum, out var bikePart))
            {
                bikePart.Trigger();
            }
        }

        public void AffordanceActivate(int partNum)
        {
            if (m_bikeParts.TryGetValue(partNum, out var bikePart))
            {
                bikePart.AffordanceActivate();
            }
        }

        public void AffordanceDeactivate(int partNum)
        {
            if (m_bikeParts.TryGetValue(partNum, out var bikePart))
            {
                bikePart.AffordanceDeactivate();
            }
        }
    }
}