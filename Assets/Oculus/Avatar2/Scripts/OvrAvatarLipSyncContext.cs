using System;
using UnityEngine;
using UnityEngine.Serialization;

/**
 * @file OvrAvatarLipSyncContext.cs
 */

namespace Oculus.Avatar2
{

    /// Indicates the audio source to drive lip sync.
    public enum LipSyncAudioSourceType
    {
        /// Audio forwarding is disabled
        None,

        /// Audio is captured from the audio source on this game object
        AudioSource,

        /// @ref ProcessAudioSamples must be called manually to forward audio
        Manual,
    }

    public class OvrAvatarLipSyncContext : OvrAvatarLipSyncBehavior
    {
        private const string logScope = "lipSync";
        private const float bufferSizeRatio = 0.4f;

        [Header("Audio Settings")]
        [SerializeField]
        protected LipSyncAudioSourceType _audioSourceType = LipSyncAudioSourceType.AudioSource;

        /**
         * Enables or disables audio capture.
         * If set to true, no audio will play back to the speakers.
         */
        [Tooltip("If captured, no audio will play back to the speakers.")]
        [FormerlySerializedAs("_captureAudio")]
        public bool CaptureAudio = true;

        [SerializeField]
        private CAPI.ovrAvatar2LipSyncMode _mode = CAPI.ovrAvatar2LipSyncMode.Original;

        [Range(0.0f, 100.0f)]
        [SerializeField]
        private int _smoothing;

        [SerializeField]
        private int _audioSampleRate = 48000;

        protected OvrAvatarVisemeContext _visemeContext;

        /**
         * Controls the rate at which audio is sampled.
         */
        public int AudioSampleRate
        {
            get { return _audioSampleRate; }
            set
            {
                if (value != _audioSampleRate)
                {
                    _audioSampleRate = value;
                    _visemeContext?.SetSampleRate((UInt32)_audioSampleRate, (UInt32)(_audioSampleRate * bufferSizeRatio));
                }
            }
        }

        /**
         * Establishes the method used for lip sync.
         * @see CAPI.ovrAvatar2LipSyncMode
         */
        public CAPI.ovrAvatar2LipSyncMode Mode
        {
            get => _mode;
            set
            {
                if (value != _mode)
                {
                    _mode = value;
                    _visemeContext?.SetMode(_mode);
                }
            }
        }

        public override OvrAvatarLipSyncContextBase LipSyncContext
        {
            get
            {
                CreateVisemeContext();

                return _visemeContext;
            }
        }

        // Thread-safe check of this.enabled
        protected bool _active;

        // Core Unity Functions

        private void Start()
        {
            SetAudioSourceType(_audioSourceType);
        }

        protected virtual void OnAudioFilterRead(float[] data, int channels)
        {
            if (_audioSourceType == LipSyncAudioSourceType.AudioSource)
            {
                ProcessAudioSamples(data, channels);
            }
        }

        private void OnEnable()
        {
            _active = true;
            CreateVisemeContext();
        }

        private void OnDisable()
        {
            _active = false;
        }

        private void OnDestroy()
        {
            _visemeContext?.Dispose();
            _visemeContext = null;
        }

        private void OnValidate()
        {
            SetSmoothing(_smoothing);
        }

        // Public Functions

        public void SetSmoothing(int smoothing)
        {
            _smoothing = Math.Max(Math.Min(smoothing, 100), 0);
            _visemeContext?.SetSmoothing(_smoothing);
        }

        public void SetAudioSourceType(LipSyncAudioSourceType newType)
        {
            _audioSourceType = newType;

            OvrAvatarLog.LogDebug($"Setting audio source type to {_audioSourceType}", logScope);

            switch (_audioSourceType)
            {
                case LipSyncAudioSourceType.None:
                    enabled = false;
                    break;
                case LipSyncAudioSourceType.AudioSource:
                    enabled = true;
                    break;
                case LipSyncAudioSourceType.Manual:
                    enabled = true;
                    break;
                default:
                    break;
            }
        }

        public virtual void ProcessAudioSamples(float[] data, int channels)
        {
            if (!_active || !OvrAvatarManager.initialized) return;

            _visemeContext?.FeedAudio(data, channels);

            if (CaptureAudio)
            {
                // Prevent other mixers or output from also using this audio input
                Array.Clear(data, 0, data.Length);
            }
        }

        public virtual void ProcessAudioSamples(short[] data, int channels)
        {
            if (!_active || !OvrAvatarManager.initialized) return;

            _visemeContext?.FeedAudio(data, channels);

            if (CaptureAudio)
            {
                // Prevent other mixers or output from also using this audio input
                Array.Clear(data, 0, data.Length);
            }
        }

        #region Private Methods

        private void CreateVisemeContext()
        {
            if (_visemeContext == null && OvrAvatarManager.initialized)
            {
                _visemeContext = new OvrAvatarVisemeContext(new CAPI.ovrAvatar2LipSyncProviderConfig
                {
                    mode = _mode,
                    audioBufferSize = (UInt32)(_audioSampleRate * bufferSizeRatio),
                    audioSampleRate = (UInt32)_audioSampleRate
                });
                SetSmoothing(_smoothing);
            }
        }

        #endregion
    }
}
