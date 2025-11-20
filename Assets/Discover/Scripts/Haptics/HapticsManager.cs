// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Concurrent;
using System.Collections.Generic;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover.Haptics
{
    public enum VibrationForce
    {
        LIGHT,
        MEDIUM,
        HARD,
    }

    [MetaCodeSample("Discover")]
    public class HapticsManager : MonoBehaviour
    {
        private readonly Dictionary<OVRInput.Controller, float> m_stopVibratingControllersAt =
          new();

        private readonly ConcurrentQueue<VibrationSettings> m_vibrationQueue = new();

        public static HapticsManager Instance { get; private set; }

        private void Awake()
        {
            if (null != Instance)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private readonly List<OVRInput.Controller> m_controllersToStop = new();

        private void Update()
        {
            foreach (var entry in m_stopVibratingControllersAt)
            {
                if (Time.realtimeSinceStartup >= entry.Value)
                {
                    OVRInput.SetControllerVibration(0f, 0f, entry.Key);
                    m_controllersToStop.Add(entry.Key);
                }
            }

            foreach (var controller in m_controllersToStop)
            {
                _ = m_stopVibratingControllersAt.Remove(controller);
            }

            if (m_controllersToStop.Count > 0)
            {
                m_controllersToStop.Clear();
            }

            if (m_vibrationQueue.TryDequeue(out var settings))
            {
                VibrateForDuration(settings.Force, settings.DurationSeconds, settings.Controller);
            }
        }

        /// <summary>
        /// Do not call from outside main thread
        /// </summary>
        public void VibrateForDuration(VibrationForce force, float durationSeconds, OVRInput.Controller controller)
        {
            float frequency;
            float amplitude;

            switch (force)
            {
                case VibrationForce.LIGHT:
                    frequency = 1.0f;
                    amplitude = 0.25f;
                    break;

                case VibrationForce.HARD:
                    frequency = 1.0f;
                    amplitude = 0.75f;
                    break;

                case VibrationForce.MEDIUM:
                default:
                    frequency = 1.0f;
                    amplitude = 0.5f;
                    break;
            }

            OVRInput.SetControllerVibration(frequency, amplitude, controller);
            // Note: if two events to vibrate come to us, we're simply overriding with the latest one
            // which makes sense since the vibration will take over anyway, so it makes sense to
            // stop vibrating corresponding to the overriding event

            m_stopVibratingControllersAt[controller] = Time.realtimeSinceStartup + durationSeconds;
        }

        /*
         * To call for vibration
         * off the main thread
         */
        public void QueueVibrateForDuration(VibrationForce force, float durationSeconds, OVRInput.Controller controller)
        {
            var vibrationSettings = new VibrationSettings
            {
                Force = force,
                DurationSeconds = durationSeconds,
                Controller = controller
            };

            m_vibrationQueue.Enqueue(vibrationSettings);
        }
    }

    [MetaCodeSample("Discover")]
    public class VibrationSettings
    {
        public VibrationForce Force;
        public float DurationSeconds;
        public OVRInput.Controller Controller;
    }
}
