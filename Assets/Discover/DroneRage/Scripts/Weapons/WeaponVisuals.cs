// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Discover.DroneRage.Audio;
using Discover.Utilities.Extensions;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;
using static Discover.DroneRage.Bootstrapper.DroneRageAppContainerUtils;

using Random = UnityEngine.Random;

namespace Discover.DroneRage.Weapons
{
    public class WeaponVisuals : MonoBehaviour
    {


        [SerializeField]
        private Weapon m_weapon;


        [SerializeField]
        private Transform m_muzzleTransform;


        [SerializeField]
        private ParticleSystem m_tracerPrefab;
        private ParticleSystem m_tracer;


        [SerializeField]
        private ParticleSystem m_muzzleFlashPrefab;
        private ParticleSystem m_muzzleFlash;


        [SerializeField]
        private Transform m_slideTransform;

        [SerializeField]
        private float m_slideMoveDistance = 0.03f;

        [SerializeField]
        private float m_slideMoveDuration = 0.075f;


        [SerializeField]
        private Transform m_recoilRootTransform;

        [SerializeField]
        private Transform m_recoilTargetTransform;

        [SerializeField]
        private float m_recoilDuration = 0.2f;

        [SerializeField]
        private Vector2 m_recoilForceVariance = new(0.8f, 1.2f);


        [SerializeField]
        private Transform m_shellEjectTransform;

        [SerializeField]
        private Vector3 m_shellEjectForce = Vector3.up;

        [SerializeField]
        private Vector2 m_shellEjectForceVariance = new(0.8f, 1.2f);


        [SerializeField]
        private PooledBulletCasing m_shellPrefab;
        public PooledBulletCasing ShellPrefab
        {
            get => m_shellPrefab;
            set => m_shellPrefab = value;
        }


        [SerializeField]
        private Transform m_triggerTransform;
        public Transform TriggerTransform => m_triggerTransform;


        [SerializeField]
        private AudioTriggerExtended[] m_fireSfx;

        [SerializeField]
        private AudioTriggerExtended[] m_fireLoopSfx;


        [SerializeField]
        private AudioTriggerExtended[] m_fireLoopEndSfx;


        [SerializeField]
        private float m_hapticsFrequency = 0.5f;

        [SerializeField]
        private float m_hapticsAmplitude = 1.0f;

        [SerializeField]
        private float m_hapticsTime = 0.1f;


        [SerializeField]
        private GameObject m_droppedWeaponPrefab;

        [SerializeField]
        public float DropTossSpeed = 3f;

        private Coroutine m_hapticsCoroutine;
        public OVRInput.Controller HapticsTargetController { get; set; } = OVRInput.Controller.None;

        private int m_maxShellCasings = 12;
        public int MaxShellCasings
        {
            get => m_maxShellCasings;
            set
            {
                m_maxShellCasings = value;
                InitShellPool();
            }
        }
        private LinkedList<PooledBulletCasing> m_shellReleaseQueue;
        private ObjectPool<PooledBulletCasing> m_shellObjectPool;

        private Coroutine m_sliderCoroutine;
        private Coroutine m_recoilCoroutine;
        private Vector3 m_sliderEndPosition;
        private Vector3 m_recoilEndPosition;
        private Vector3 m_recoilEndRotation;

        private void Start()
        {
            Assert.IsNotNull(m_muzzleFlashPrefab, $"{nameof(m_muzzleFlash)} cannot be null.");
            Assert.IsNotNull(m_tracerPrefab, $"{nameof(m_tracer)} cannot be null.");
            Assert.IsNotNull(m_slideTransform, $"{nameof(m_slideTransform)} cannot be null.");
            Assert.IsNotNull(m_shellEjectTransform, $"{nameof(m_shellEjectTransform)} cannot be null.");
            Assert.IsNotNull(m_recoilRootTransform, $"{nameof(m_recoilRootTransform)} cannot be null.");
            Assert.IsNotNull(m_recoilTargetTransform, $"{nameof(m_recoilTargetTransform)} cannot be null.");

            m_muzzleFlash = GetAppContainer().Instantiate(m_muzzleFlashPrefab, m_muzzleTransform);
            var muzzleFlashTransform = m_muzzleFlash.transform;
            muzzleFlashTransform.localPosition = Vector3.zero;
            muzzleFlashTransform.localRotation = Quaternion.identity;
            muzzleFlashTransform.SetWorldScale(m_muzzleFlashPrefab.transform.localScale);

            m_tracer = GetAppContainer().Instantiate(m_tracerPrefab, m_muzzleTransform);
            var tracerTransform = m_tracer.transform;
            tracerTransform.localPosition = Vector3.zero;
            tracerTransform.localRotation = Quaternion.identity;
            tracerTransform.SetWorldScale(m_tracerPrefab.transform.localScale);
        }

        private void OnEnable()
        {
            InitShellPool();

            m_weapon.WeaponFired += OnWeaponFired;
            m_weapon.StartedFiring += OnStartedFiring;
            m_weapon.StoppedFiring += OnStoppedFiring;
        }

        private void OnDisable()
        {
            if (m_shellObjectPool != null)
            {
                ReleaseAllShells();
                m_shellObjectPool.Dispose();
                m_shellObjectPool = null;
            }

            if (m_hapticsCoroutine != null)
            {
                StopCoroutine(m_hapticsCoroutine);
                m_hapticsCoroutine = null;
            }
            StopSliderAnim();
            StopRecoilAnim();
            m_weapon.WeaponFired -= OnWeaponFired;
            m_weapon.StartedFiring -= OnStartedFiring;
            m_weapon.StoppedFiring -= OnStoppedFiring;
        }

        public void OnWeaponFired(Vector3 shotOrigin, Vector3 shotDirection)
        {
            foreach (var sfx in m_fireSfx)
            {
                if (sfx != null)
                {
                    sfx.PlayAudio();
                }
            }

            m_muzzleFlash.Stop(true);
            m_muzzleFlash.Play();

            m_tracer.transform.forward = shotDirection;
            m_tracer.Emit(1);

            StopSliderAnim();
            m_sliderCoroutine = StartCoroutine(SlideSlidder(Vector3.back * m_slideMoveDistance, m_slideMoveDuration));

            StopRecoilAnim();
            var recoilForceScale = Random.Range(m_recoilForceVariance.x, m_recoilForceVariance.y);
            m_recoilCoroutine = StartCoroutine(ProcessRecoil(m_recoilTargetTransform.localPosition * recoilForceScale, m_recoilTargetTransform.localRotation.eulerAngles, m_recoilDuration));

            var shellEjectPosition = m_shellEjectTransform.position;
            var shellEjectRotation = m_shellEjectTransform.rotation;

            if (IsShellPoolFull())
            {
                ReleaseOldestShell();
            }

            var shell = m_shellObjectPool.Get();
            shell.transform.SetPositionAndRotation(shellEjectPosition, shellEjectRotation);
            shell.Rigidbody.AddForce(shellEjectRotation * m_shellEjectForce * Random.Range(m_shellEjectForceVariance.x, m_shellEjectForceVariance.y), ForceMode.Force);
            shell.Rigidbody.angularVelocity = Random.rotation.eulerAngles;

            if (m_hapticsCoroutine != null)
            {
                StopCoroutine(m_hapticsCoroutine);
                m_hapticsCoroutine = null;
            }
            m_hapticsCoroutine = StartCoroutine(ApplyHaptics());
        }

        public void OnStartedFiring()
        {
            foreach (var sfx in m_fireLoopSfx)
            {
                if (sfx != null)
                {
                    sfx.PlayAudio();
                }
            }
        }

        public void OnStoppedFiring()
        {
            foreach (var sfx in m_fireLoopSfx)
            {
                if (sfx != null)
                {
                    sfx.StopAudio();
                }
            }
            foreach (var sfx in m_fireLoopEndSfx)
            {
                if (sfx != null)
                {
                    sfx.PlayAudio();
                }
            }
        }

        public GameObject SpawnDroppedWeapon()
        {
            GameObject droppedWeapon = null;

            if (m_droppedWeaponPrefab != null)
            {
                droppedWeapon = GetAppContainer().Instantiate(m_droppedWeaponPrefab, transform.position, transform.rotation);

                if (DropTossSpeed > float.Epsilon && droppedWeapon.TryGetComponent<Rigidbody>(out var dropRb))
                {
                    var dir = WeaponUtils.RandomSpread(Vector2.one);
                    dir.z = Mathf.Abs(dir.y);
                    dir.y = 1f;
                    dir = dir.normalized * DropTossSpeed;

                    dropRb.AddForceAtPosition(dir,
                        new Vector3(0f, -0.09f, -0.08f),
                        ForceMode.VelocityChange);
                }
            }

            return droppedWeapon;
        }

        private void InitShellPool()
        {
            if (m_shellObjectPool != null)
            {
                ReleaseAllShells();
                m_shellObjectPool.Dispose();
                m_shellObjectPool = null;
            }

            PooledBulletCasing CreatePooledShell()
            {
                var shell = GetAppContainer().Instantiate(m_shellPrefab, Vector3.zero, Quaternion.identity);
                shell.Pool = m_shellObjectPool;
                shell.gameObject.SetActive(false);
                return shell;
            }

            void InitShell(PooledBulletCasing shell)
            {
                shell.Init();
                shell.gameObject.SetActive(true);
                _ = m_shellReleaseQueue.AddLast(shell);
            }

            void ReleaseShell(PooledBulletCasing shell)
            {
                shell.gameObject.SetActive(false);
                _ = m_shellReleaseQueue.Remove(shell);
            }

            void DestroyShell(PooledBulletCasing shell)
            {
                Destroy(shell.gameObject);
                _ = m_shellReleaseQueue.Remove(shell);
            }

            m_shellObjectPool = new ObjectPool<PooledBulletCasing>(CreatePooledShell, InitShell, ReleaseShell, DestroyShell, collectionCheck: true, MaxShellCasings, MaxShellCasings);
            m_shellReleaseQueue = new LinkedList<PooledBulletCasing>();
        }

        private bool IsShellPoolFull()
        {
            return m_shellReleaseQueue.Count == MaxShellCasings;
        }

        private void ReleaseOldestShell()
        {
            var shell = m_shellReleaseQueue.First.Value;
            m_shellReleaseQueue.RemoveFirst();
            m_shellObjectPool.Release(shell);
        }

        private void ReleaseAllShells()
        {
            while (m_shellReleaseQueue.Count > 0)
            {
                ReleaseOldestShell();
            }
        }

        private IEnumerator ApplyHaptics()
        {
            var time = 0f;
            while (time < m_hapticsTime)
            {
                time += Time.deltaTime;
                var progress = time / m_hapticsTime;
                var easeInQuad = progress * progress; // easeInQuad (x * x)
                var value = Mathf.Lerp(m_hapticsAmplitude, 0, easeInQuad);
                OVRInput.SetControllerVibration(m_hapticsFrequency, value, HapticsTargetController);
                yield return null;
            }
            OVRInput.SetControllerVibration(m_hapticsFrequency, 0, HapticsTargetController);
            m_hapticsCoroutine = null;
        }

        private void StopSliderAnim()
        {
            if (m_sliderCoroutine != null)
            {
                StopCoroutine(m_sliderCoroutine);
                m_sliderCoroutine = null;
                m_slideTransform.localPosition = m_sliderEndPosition;
            }
        }

        private void StopRecoilAnim()
        {
            if (m_recoilCoroutine != null)
            {
                StopCoroutine(m_recoilCoroutine);
                m_recoilCoroutine = null;
                m_recoilRootTransform.localPosition = m_recoilEndPosition;
                m_recoilRootTransform.localEulerAngles = m_recoilEndRotation;
            }
        }
        private IEnumerator SlideSlidder(Vector3 movement, float duration)
        {
            var time = 0f;
            m_sliderEndPosition = m_slideTransform.localPosition;
            var to = m_sliderEndPosition + movement;
            while (time < duration)
            {
                time += Time.deltaTime;
                var progress = Mathf.Clamp01(time / duration);
                var ease = progress < 0.5f ? progress / 0.5f : (1 - progress) / 0.5f;
                m_slideTransform.localPosition = Vector3.Lerp(m_sliderEndPosition, to, ease);
                yield return null;
            }

            m_slideTransform.localPosition = m_sliderEndPosition;
            m_sliderCoroutine = null;
        }

        private IEnumerator ProcessRecoil(Vector3 movement, Vector3 rotateBy, float duration)
        {
            var time = 0f;
            m_recoilEndPosition = m_recoilRootTransform.localPosition;
            var targetPos = m_recoilEndPosition + movement;
            m_recoilEndRotation = m_recoilRootTransform.localEulerAngles;
            var rotationTarget = m_recoilEndRotation + rotateBy;
            rotationTarget.x = rotationTarget.x >= 360 ? rotationTarget.x - 360 : rotationTarget.x;
            rotationTarget.y = rotationTarget.y >= 360 ? rotationTarget.y - 360 : rotationTarget.y;
            rotationTarget.z = rotationTarget.z >= 360 ? rotationTarget.z - 360 : rotationTarget.z;
            while (time < duration)
            {
                time += Time.deltaTime;
                var progress = Mathf.Clamp01(time / duration);
                var ease = progress < 0.5f ? progress / 0.5f : (1 - progress) / 0.5f;
                m_recoilRootTransform.localPosition = Vector3.Lerp(m_recoilEndPosition, targetPos, ease);
                var rotationEase = Mathf.Sqrt(1 - Mathf.Pow(progress - 1, 2)); // OutCirc
                m_recoilRootTransform.localEulerAngles = Vector3.Lerp(rotationTarget, m_recoilEndRotation, rotationEase);
                yield return null;
            }

            m_recoilRootTransform.localPosition = m_recoilEndPosition;
            m_recoilRootTransform.localEulerAngles = m_recoilEndRotation;
            m_recoilCoroutine = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_weapon == null)
            {
                m_weapon = GetComponent<Weapon>();
            }
            if (m_recoilRootTransform == null)
            {
                m_recoilRootTransform = transform;
            }
        }
#endif
    }
}
