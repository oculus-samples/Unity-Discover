// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Meta.Utilities;
using Oculus.Interaction;
using UnityEngine;

namespace MRBike
{
    [RequireComponent(typeof(Grabbable))]
    public class TransformReset : MonoBehaviour
    {
        [SerializeField] private Transform m_returnHomeTarget;
        [SerializeField] private float m_returnHomeTime = 1.5f;

        [AutoSet]
        [SerializeField] private Grabbable m_grabbable;

        private Transform m_holdPoint;
        private bool m_isGrabbed = false;
        private Vector3 m_returnHomePosition;
        private Quaternion m_returnHomeRotation;

        public Transform ReturnHomeTarget
        {
            set => m_returnHomeTarget = value;
        }

        private void OnEnable()
        {
            var thisTransform = transform;
            m_returnHomePosition = thisTransform.position;
            m_returnHomeRotation = thisTransform.rotation;
        }

        public void Grabbed()
        {
            if (!m_isGrabbed)
            {
                m_isGrabbed = true;
            }
        }

        public void Released()
        {
            m_isGrabbed = false;

            if (m_grabbable.SelectingPointsCount == 0)
            {
                _ = StartCoroutine(ReturnHome());
            }
        }

        public void Released(bool isOwner)
        {
            m_isGrabbed = false;

            if (isOwner)
            {
                if (m_grabbable.SelectingPointsCount == 0)
                {
                    _ = StartCoroutine(ReturnHome());
                }
            }
        }

        private IEnumerator ReturnHome()
        {
            float timer = 0;
            var thisTransform = transform;
            while (timer < m_returnHomeTime)
            {
                if (m_isGrabbed)
                {
                    timer = m_returnHomeTime;
                }

                timer += Time.deltaTime;
                var t = timer / m_returnHomeTime;
                var targetPos = m_returnHomeTarget != null ? m_returnHomeTarget.position : m_returnHomePosition;
                var targetRot = m_returnHomeTarget != null ? m_returnHomeTarget.rotation : m_returnHomeRotation;
                thisTransform.position = Vector3.Lerp(thisTransform.position, targetPos, t);
                thisTransform.rotation = Quaternion.Lerp(thisTransform.rotation, targetRot, t);
                yield return null;
            }

            if (!m_isGrabbed)
            {
                if (m_returnHomeTarget != null)
                {
                    thisTransform.position = m_returnHomeTarget.position;
                    thisTransform.rotation = m_returnHomeTarget.rotation;
                }
                else
                {
                    thisTransform.position = m_returnHomePosition;
                    thisTransform.rotation = m_returnHomeRotation;
                }
            }
        }

    }
}
