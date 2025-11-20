// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover.NUX
{
    [MetaCodeSample("Discover")]
    public class NUXController : MonoBehaviour
    {
        [SerializeField] private string m_nuxKey;

        [SerializeField] private GameObject[] m_nuxPages;
        [SerializeField] private float m_pageDuration;
        [SerializeField] private bool m_autoProgress = true;
        [SerializeField] private GameObject m_rootCanvas;

        [Tooltip("Position of the menu relative to the camera")]
        [SerializeField] private Vector3 m_positionRelativeToCamera = new(0f, 0.187f, 0.5f);

        [SerializeField] private GameObject m_progressButtons;
        [SerializeField] private GameObject m_progressButtonPrevious;
        [SerializeField] private GameObject m_progressButtonNext;
        [SerializeField] private GameObject m_progressButtonConfirm;

        private int m_currentNuxStep;

        public Action OnNuxCompleted;

        public string NuxKey => m_nuxKey;

        public bool IsCompleted => m_currentNuxStep >= m_nuxPages.Length;

        private void Awake()
        {
            LoadNuxStep();
        }

        public void StartNux()
        {
            if (IsCompleted)
            {
                OnCompletedCallback();
                return;
            }
            m_rootCanvas.SetActive(true);

            SetupPosition();

            if (m_autoProgress)
            {
                _ = StartCoroutine(HandleNuxStepAuto());
            }
            else
            {
                m_progressButtons.SetActive(true);
                ShowCurrentNuxStep();
            }
        }

        public void ResetNux()
        {
            foreach (var page in m_nuxPages)
            {
                page.gameObject.SetActive(false);
            }

            m_currentNuxStep = 0;
            PlayerPrefs.SetInt(m_nuxKey, 0);
        }

        [ContextMenu("On Next")]
        public void OnClickNext()
        {
            m_nuxPages[m_currentNuxStep].SetActive(false);
            m_currentNuxStep++;
            SaveNuxStep();
            ShowCurrentNuxStep();
        }

        [ContextMenu("On Previous")]
        public void OnClickPrevious()
        {
            m_nuxPages[m_currentNuxStep].SetActive(false);
            m_currentNuxStep--;
            ShowCurrentNuxStep();
        }
        [ContextMenu("On Confirm")]
        public void OnClickConfirm()
        {
            m_currentNuxStep++;
            SaveNuxStep();
            EndNux();
        }

        private void SetupPosition()
        {
            if (Camera.main != null)
            {
                var camTransform = Camera.main.transform;
                var camDirection = camTransform.forward;
                transform.position = camTransform.position
                                                    + camDirection * m_positionRelativeToCamera.z
                                                    - new Vector3(0.0f, m_positionRelativeToCamera.y, 0.0f);
                transform.LookAt(camTransform);
            }
        }

        private IEnumerator HandleNuxStepAuto()
        {
            ShowCurrentNuxStep();
            var waitTime = new WaitForSeconds(m_pageDuration);
            while (!IsCompleted)
            {
                yield return waitTime;

                var prevNuxStep = m_currentNuxStep;
                if (IncrementNuxStep())
                {
                    m_nuxPages[prevNuxStep].SetActive(false);
                }
            }
        }

        private void ShowCurrentNuxStep()
        {
            m_nuxPages[m_currentNuxStep].SetActive(true);
            if (!m_autoProgress)
            {
                EvaluateProgressButtons();
            }
        }

        private void EvaluateProgressButtons()
        {
            var isFirstPage = m_currentNuxStep == 0;
            var isLastPage = m_currentNuxStep == m_nuxPages.Length - 1;
            m_progressButtonPrevious.SetActive(!isFirstPage);
            m_progressButtonNext.SetActive(!isLastPage);
            m_progressButtonConfirm.SetActive(isLastPage);
        }

        private bool IncrementNuxStep()
        {
            m_currentNuxStep++;
            SaveNuxStep();
            if (IsCompleted)
            {
                EndNux();
                return false;
            }
            return true;
        }

        private void EndNux()
        {
            m_rootCanvas.SetActive(false);
            OnCompletedCallback();
        }

        private void OnCompletedCallback()
        {
            var completedCallback = OnNuxCompleted;
            OnNuxCompleted = null;
            completedCallback?.Invoke();
        }

        private void LoadNuxStep()
        {
            m_currentNuxStep = PlayerPrefs.GetInt(m_nuxKey, 0);
        }

        private void SaveNuxStep()
        {
            PlayerPrefs.SetInt(m_nuxKey, m_currentNuxStep);
        }
    }
}