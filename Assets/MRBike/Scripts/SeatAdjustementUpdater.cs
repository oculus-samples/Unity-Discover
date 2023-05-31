// Copyright (c) Meta Platforms, Inc. and affiliates.

using TMPro;
using UnityEngine;

namespace MRBike
{
    /// <summary>
    /// When adjusting the seat this will update the value indication of the height of the seat and update the fiducial ctrl
    /// </summary>
    public class SeatAdjustementUpdater : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_valueLabel;
        [SerializeField] private FiducialCtrl m_fiducialCtrl;
        [SerializeField] private GameObject m_movingObject;
        [SerializeField] private float m_baseDistance = 0.7f;
        [SerializeField] private float m_coef = 100;
        [SerializeField] private string m_suffix = " cm";
        [SerializeField] private float m_moveCheck = 0.01f;

        private float m_previousDistance;
        private Vector3 m_startPoint;
        private bool m_grabbed = false;
        private float m_travel;

        private void Start()
        {
            m_startPoint = m_movingObject.transform.position;
            m_fiducialCtrl.Height = m_baseDistance * 100;
        }

        public void Grab(bool grabState)
        {
            m_grabbed = grabState;
        }

        private void Update()
        {
            var position = m_movingObject.transform.position;
            var hasMoved = m_moveCheck > 0 && Mathf.Abs(position.y - m_startPoint.y) >= m_moveCheck;
            if (!m_grabbed && !hasMoved)
            {
                return;
            }

            m_travel = (position.y - m_startPoint.y) * m_coef;

            m_startPoint = position;
            m_baseDistance -= m_travel;

            m_fiducialCtrl.Height = m_baseDistance * 100;
            m_valueLabel.text = m_baseDistance.ToString("F") + m_suffix;
        }
    }
}
