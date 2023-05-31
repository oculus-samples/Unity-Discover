// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

namespace Discover.DroneRage.Enemies
{
    public class PhysicsDebris : MonoBehaviour
    {
        private enum DestroyBehaviour
        {
            DESTROY,
            DISABLE
        }


        [SerializeField]
        private DestroyBehaviour m_destroyBehaviour = DestroyBehaviour.DESTROY;

        [SerializeField]
        private float m_destroyTime = 3.0f;

        [SerializeField]
        private float m_maxLifetime = 10.0f;

        private Rigidbody m_rigidbody;

        private float m_aliveTime;
        private float m_asleepTime;

        private void Awake()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            Assert.IsNotNull(m_rigidbody, $"{nameof(m_rigidbody)} cannot be null.");
        }

        private void Update()
        {
            m_aliveTime += Time.deltaTime;
            if (m_aliveTime > m_maxLifetime)
            {
                DestroySelf();
                return;
            }

            if (m_rigidbody.IsSleeping())
            {
                m_asleepTime += Time.deltaTime;
                if (m_asleepTime > m_destroyTime)
                {
                    DestroySelf();
                    return;
                }
            }
            else
            {
                m_asleepTime = 0.0f;
            }

            if (transform.localPosition.y < -5.0f)
            {
                DestroySelf();
            }
        }

        private void DestroySelf()
        {
            if (m_destroyBehaviour == DestroyBehaviour.DESTROY)
            {
                _ = SinkAndDestroy();
                enabled = false;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private async UniTaskVoid SinkAndDestroy()
        {
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
            {
                collider.enabled = false;
            }

            var scaleCoroutine = StartCoroutine(ScaleDown(Vector3.zero, 1.0f));

            await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: this.GetCancellationTokenOnDestroy());

            StopCoroutine(scaleCoroutine);
            Destroy(gameObject);
        }

        private IEnumerator ScaleDown(Vector3 finalScale, float duration)
        {
            var time = 0f;
            var startScale = transform.localScale;
            while (time < duration)
            {
                time += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, finalScale, time / duration);
                yield return null;
            }

            transform.localScale = finalScale;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_rigidbody == null)
            {
                m_rigidbody = GetComponent<Rigidbody>();
            }
        }
#endif
    }
}
