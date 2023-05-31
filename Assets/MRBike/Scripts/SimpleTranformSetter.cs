// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace MRBike
{
    public class SimpleTranformSetter : MonoBehaviour
    {
        [SerializeField] private GameObject m_objectToMove;
        [SerializeField] private GameObject m_targetObject;

        public void SetTransform()
        {
            m_objectToMove.transform.position = m_targetObject.transform.position;
            m_objectToMove.transform.rotation = m_targetObject.transform.rotation;
        }

    }
}
