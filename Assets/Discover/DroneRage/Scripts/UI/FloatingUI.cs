// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.Networking;
using UnityEngine;

namespace Discover.DroneRage.UI
{
    public class FloatingUI : MonoBehaviour
    {


        [SerializeField]
        private Vector3 m_targetPosition = new(0.0f, 0.1f, 1.0f);
        public Vector3 TargetPosition
        {
            get => m_targetPosition;
            set
            {
                m_targetPosition = value;
                m_targetPosition.z = Mathf.Max(0.01f, m_targetPosition.z);
            }
        }

        public float SoftLeashSpeed = 2.0f;


        [SerializeField]
        [Tooltip("The UI will gradually return to the center of the player's gaze when the player looks outside this area. Width and height are relative to the UI size, depth is in world units.")]
        private Vector3 m_softLeashSize = new(0.8f, 0.8f, 0.1f);
        public Vector3 SoftLeashSize
        {
            get => m_softLeashSize;
            set => m_softLeashSize = Vector3.Max(value, Vector3.zero);
        }


        [SerializeField]
        [Tooltip("The UI will be restricted within the leash area. Width and height are relative to the UI size, depth is in world units.")]
        private Vector3 m_hardLeashSize = new(2.0f, 2.0f, 0.15f);
        public Vector3 HardLeashSize
        {
            get => m_hardLeashSize;
            set
            {
                m_hardLeashSize = Vector3.Max(value, m_softLeashSize);
                m_hardLeashSize.z = Mathf.Min(m_hardLeashSize.z, TargetPosition.z - 0.01f);
            }
        }


        [SerializeField]
        [Tooltip("The UI will gradually return to the center of the player's gaze when the player looks outside this area. Width and height are relative to the UI size, depth is in world units.")]
        private Vector3 m_recenterTriggerSize = new(2.0f, 2.0f, 0.15f);
        public Vector3 RecenterTriggerSize
        {
            get => m_recenterTriggerSize;
            set => m_recenterTriggerSize = Vector3.Min(Vector3.Max(value, m_softLeashSize), m_hardLeashSize);
        }

        public float RecenterSpeed = 0.5f;
        public float RecenterArriveRadius = 0.1f;
        public bool RecenterX = true;
        public bool RecenterY = false;
        public bool RecenterZ = false;

        public bool ConstrainY = true;

        [SerializeField]
        private Vector2 m_constrainYMinMax = new(-0.1f, 0.25f);
        public Vector2 ConstrainYMinMax
        {
            get => m_constrainYMinMax;
            set => m_constrainYMinMax = new Vector2(value.x, Mathf.Max(value.y, value.x));
        }

        public bool LockPitch = true;

        private RectTransform m_rectTransform;
        private bool m_isRecentering = false;
        private Vector3 m_recenterWorldPosition;

        private void Awake()
        {
            m_rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (PhotonNetwork.CameraRig == null)
            {
                enabled = false;
                return;
            }
            // Jump to the target position
            var cameraTransform = PhotonNetwork.CameraRig.centerEyeAnchor;
            var uiPosition = cameraTransform.TransformPoint(TargetPosition);
            transform.position = cameraTransform.TransformPoint(TargetPosition);
            UpdateRotation(uiPosition - cameraTransform.position);
        }

        private void Update()
        {
            var cameraTransform = PhotonNetwork.CameraRig.centerEyeAnchor;
            var cameraPosition = cameraTransform.position;

            var uiWorldPosition = transform.position;
            var uiPosition = cameraTransform.InverseTransformPoint(uiWorldPosition);


            var hardLeashExtents = GetScaledExtents(HardLeashSize);
            var softLeashExtents = GetScaledExtents(m_softLeashSize);
            var recenterExtents = GetScaledExtents(RecenterTriggerSize);
            var toTarget = TargetPosition - uiPosition;

            // Hard leash - Restrict position within leash area
            uiPosition = Vector3.Min(Vector3.Max(uiPosition, TargetPosition - hardLeashExtents), TargetPosition + hardLeashExtents);

            // Recenter - Float back to the center of the view
            if ((RecenterX && Mathf.Abs(toTarget.x) > recenterExtents.x)
                || (RecenterY && Mathf.Abs(toTarget.y) > recenterExtents.y)
                || (RecenterZ && Mathf.Abs(toTarget.z) > recenterExtents.z))
            {
                m_recenterWorldPosition = cameraTransform.TransformPoint(TargetPosition);
                m_isRecentering = true;
            }

            if (m_isRecentering)
            {
                // Leash the recenter target by the arrive radius
                var recenterTarget = cameraTransform.InverseTransformPoint(m_recenterWorldPosition);
                recenterTarget = Vector3.Min(Vector3.Max(recenterTarget, TargetPosition - Vector3.one * RecenterArriveRadius), TargetPosition + Vector3.one * RecenterArriveRadius);
                var toRecenterTarget = recenterTarget - uiPosition;

                // Recenter
                if ((!RecenterX || Mathf.Abs(toRecenterTarget.x) <= RecenterArriveRadius)
                    && (!RecenterY || Mathf.Abs(toRecenterTarget.y) <= RecenterArriveRadius)
                    && (!RecenterZ || Mathf.Abs(toRecenterTarget.z) <= RecenterArriveRadius))
                {
                    m_isRecentering = false;
                }
                else
                {
                    recenterTarget = Vector3.Slerp(uiPosition, recenterTarget, RecenterSpeed * Time.deltaTime);
                    recenterTarget = GetConstrainedRecenterTarget(recenterTarget, uiPosition);
                    uiPosition = recenterTarget;
                }
            }

            // Soft leash - Float back into view gradually when outside leash area
            if (IsPointOutsideExtents(toTarget, softLeashExtents, out var softLeashAxes))
            {
                var leashTarget = toTarget - Vector3.Scale(Sign(toTarget), softLeashExtents);
                leashTarget = Vector3.Scale(leashTarget, softLeashAxes);
                leashTarget = Vector3.Slerp(Vector3.zero, leashTarget, SoftLeashSpeed * Time.deltaTime);
                uiPosition += leashTarget;
            }

            uiWorldPosition = cameraTransform.TransformPoint(uiPosition);

            // Constrain Y to fixed range
            if (ConstrainY)
            {
                uiWorldPosition.y = Mathf.Clamp(uiWorldPosition.y, m_constrainYMinMax.x + cameraPosition.y + TargetPosition.y, m_constrainYMinMax.y + cameraPosition.y + TargetPosition.y);
            }

            transform.position = uiWorldPosition;
            UpdateRotation(uiWorldPosition - cameraPosition);
        }

        private void UpdateRotation(Vector3 forward)
        {
            var targetRotation = Quaternion.LookRotation(forward, Vector3.up).eulerAngles;
            if (LockPitch)
            {
                targetRotation.x = 0.0f;
            }
            transform.eulerAngles = targetRotation;
        }

        private bool IsPointOutsideExtents(Vector3 point, Vector3 extents, out Vector3 axesOutsideExtents)
        {
            axesOutsideExtents = new Vector3(Mathf.Abs(point.x) > extents.x ? 1.0f : 0.0f,
                                             Mathf.Abs(point.y) > extents.y ? 1.0f : 0.0f,
                                             Mathf.Abs(point.z) > extents.z ? 1.0f : 0.0f);
            return Mathf.Abs(point.x) > extents.x
                || Mathf.Abs(point.y) > extents.y
                || Mathf.Abs(point.z) > extents.z;
        }

        private Vector3 Sign(Vector3 v)
        {
            return new Vector3(Mathf.Sign(v.x), Mathf.Sign(v.y), Mathf.Sign(v.z));
        }

        private Vector3 GetConstrainedRecenterTarget(Vector3 target, Vector3 original)
        {
            return new Vector3(RecenterX ? target.x : original.x, RecenterY ? target.y : original.y, RecenterZ ? target.z : original.z);
        }

        private Vector3 GetScaledExtents(Vector3 relativeSize)
        {
            var scaledUiSize = Vector3.Scale(GetUISize(), transform.lossyScale);
            return Vector3.Scale(relativeSize, scaledUiSize) * 0.5f;
        }

        private Vector3 GetUISize()
        {
            return m_rectTransform ? new Vector3(m_rectTransform.rect.width, m_rectTransform.rect.height, 1.0f) : Vector3.one;
        }

#if UNITY_EDITOR

        private void OnValidate()
        {
            SoftLeashSize = m_softLeashSize;
            HardLeashSize = m_hardLeashSize;
            RecenterTriggerSize = m_recenterTriggerSize;
            TargetPosition = m_targetPosition;
            ConstrainYMinMax = m_constrainYMinMax;
        }

        private void OnDrawGizmosSelected()
        {
            var lastColor = Gizmos.color;
            var lastMatrix = Gizmos.matrix;

            Gizmos.matrix = transform.localToWorldMatrix;

            m_rectTransform = gameObject.GetComponent<RectTransform>();

            Gizmos.color = Color.green;
            var softLeash = Vector3.Scale(m_softLeashSize, GetUISize());
            Gizmos.DrawWireCube(Vector3.zero, softLeash);
            Gizmos.color = Color.yellow;
            var recenterTrigger = Vector3.Scale(m_recenterTriggerSize, GetUISize());
            Gizmos.DrawWireCube(Vector3.zero, recenterTrigger);
            Gizmos.color = Color.red;
            var hardLeash = Vector3.Scale(m_hardLeashSize, GetUISize());
            Gizmos.DrawWireCube(Vector3.zero, hardLeash);

            Gizmos.color = lastColor;
            Gizmos.matrix = lastMatrix;
        }
#endif
    }
}
