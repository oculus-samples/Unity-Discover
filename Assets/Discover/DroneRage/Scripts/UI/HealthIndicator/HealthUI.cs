// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Discover.Networking;
using Discover.Utilities.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Discover.DroneRage.UI.HealthIndicator
{
    public class HealthUI : MonoBehaviour
    {
        public const float RCP_MAX_HEALTH = 1f / 100f;
        public static readonly Vector3 ScaleFlipped = new(-1f, 1f, 1f);


        [SerializeField]
        private float m_maxFill = 0.4f;


        [SerializeField]
        private Image m_healthBar;

        private Coroutine m_healthUpdateCoroutine = null;

        private Player.Player m_owner;
        public Player.Player Owner
        {
            get => m_owner;
            set
            {
                if (m_owner != null)
                {
                    m_owner.OnHpChange -= UpdateUI;
                }

                m_owner = value;
                if (m_owner != null)
                {
                    m_owner.OnHpChange += UpdateUI;

                    transform.localScale = m_owner.HasStateAuthority ? ScaleFlipped : Vector3.one;
                }
            }
        }

        private void OnDisable()
        {
            if (m_healthUpdateCoroutine != null)
            {
                StopCoroutine(m_healthUpdateCoroutine);
                m_healthUpdateCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            Owner = null;
        }

        private void Update()
        {
            if (m_owner == null)
            {
                return;
            }

            var healthBarRotation = 180.0f * m_healthBar.fillAmount - 90.0f;
            m_healthBar.transform.localEulerAngles = new Vector3(0.0f, 0.0f, healthBarRotation);

            transform.position = new Vector3(m_owner.gameObject.transform.position.x, 0f, m_owner.gameObject.transform.position.z);
            if (m_owner.HasStateAuthority)
            {
                transform.rotation = Quaternion.AngleAxis(m_owner.gameObject.transform.rotation.eulerAngles.y, Vector3.up);
            }
            else
            {
                var toCamera = (PhotonNetwork.CameraRig.centerEyeAnchor.position - m_owner.gameObject.transform.position).XZ().normalized;
                var angle = Vector3.SignedAngle(Vector3.forward, new Vector3(toCamera.x, 0.0f, toCamera.y), Vector3.up);
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.up);
            }
        }

        private void UpdateUI()
        {
            if (m_owner == null)
            {
                return;
            }

            if (m_healthUpdateCoroutine != null)
            {
                StopCoroutine(m_healthUpdateCoroutine);
                m_healthUpdateCoroutine = null;
            }

            var newFillAmount = m_maxFill * m_owner.Health * RCP_MAX_HEALTH;
            m_healthUpdateCoroutine = StartCoroutine(
                UpdateHealthAmount(m_healthBar.fillAmount, newFillAmount, 0.2f));
        }

        private IEnumerator UpdateHealthAmount(float from, float to, float duration)
        {
            var time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                m_healthBar.fillAmount = Mathf.Lerp(from, to, time / duration);
                yield return null;
            }

            m_healthBar.fillAmount = to;
            m_healthUpdateCoroutine = null;
        }
    }
}
