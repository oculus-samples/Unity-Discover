using System;
using System.Runtime.InteropServices;

namespace Oculus.Avatar2
{
    /// <summary>
    /// C# wrapper around OVRBody lip sync api.
    /// </summary>
    public sealed class OvrAvatarVisemeContext : OvrAvatarLipSyncContextBase
    {
        private IntPtr _context;
        private CAPI.ovrAvatar2LipSyncContext _nativeCallbacks;

        internal CAPI.ovrAvatar2LipSyncContextNative NativeCallbacks { get; }

        // Cached so we can keep some previous options when calling SetSampleRate/SetMode
        private CAPI.ovrAvatar2LipSyncProviderConfig _config;

        #region Public Methods

        public OvrAvatarVisemeContext(CAPI.ovrAvatar2LipSyncProviderConfig config)
        {
            _config = config;
            var result = CAPI.ovrAvatar2LipSync_CreateProvider(ref config, ref _context);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"ovrAvatar2LipSync_CreateProvider failed with {result}");
                // New exception type?
                throw new Exception("Failed to create viseme context");
            }

            var callbacks = CreateLipSyncContext();
            if (callbacks.HasValue)
            {
                _nativeCallbacks = callbacks.Value;
            }

            result = CAPI.ovrAvatar2LipSync_InitializeContextNative(_context, out var nativeCb);
            if (result == CAPI.ovrAvatar2Result.Success)
            {
                NativeCallbacks = nativeCb;
            }
            else
            {
                OvrAvatarLog.LogError($"ovrAvatar2LipSync_InitializeContextNative failed with {result}");
            }
        }

        public void FeedAudio(float[] data, int channels)
        {
            FeedAudio(data, 0, data.Length, channels);
        }

        public void FeedAudio(ArraySegment<float> data, int channels)
        {
            FeedAudio(data.Array, data.Offset, data.Count, channels);
        }

        private void FeedAudio(float[] data, int offset, int count, int channels)
        {
            bool isStereo = channels == 2;
            CAPI.ovrAvatar2AudioDataFormat format =
                isStereo ? CAPI.ovrAvatar2AudioDataFormat.F32_Stereo : CAPI.ovrAvatar2AudioDataFormat.F32_Mono;

            uint samples = (uint) (isStereo ? count / 2 : count);

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            var offsetAddress = IntPtr.Add(handle.AddrOfPinnedObject(), offset * sizeof(float));
            var result = CAPI.ovrAvatar2LipSync_FeedAudio(_context, format, offsetAddress, samples);
            handle.Free();
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"ovrAvatar2LipSync_FeedAudio failed with {result}");
            }
        }

        public void FeedAudio(short[] data, int channels)
        {
            FeedAudio(data, 0, data.Length, channels);
        }

        public void FeedAudio(ArraySegment<short> data, int channels)
        {
            FeedAudio(data.Array, data.Offset, data.Count, channels);
        }

        private void FeedAudio(short[] data, int offset, int count, int channels)
        {
            bool isStereo = channels == 2;
            CAPI.ovrAvatar2AudioDataFormat format =
                isStereo ? CAPI.ovrAvatar2AudioDataFormat.S16_Stereo : CAPI.ovrAvatar2AudioDataFormat.S16_Mono;

            uint samples = (uint) (isStereo ? count / 2 : count);

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            var offsetAddress = IntPtr.Add(handle.AddrOfPinnedObject(), offset * sizeof(short));
            var result = CAPI.ovrAvatar2LipSync_FeedAudio(_context, format, offsetAddress, samples);
            handle.Free();
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"ovrAvatar2LipSync_FeedAudio failed with {result}");
            }
        }

        public void Reconfigure(CAPI.ovrAvatar2LipSyncProviderConfig config)
        {
            _config = config;
            Reconfigure();
        }

        public void SetMode(CAPI.ovrAvatar2LipSyncMode newMode)
        {
            _config.mode = newMode;
            Reconfigure();
        }

        public void SetSampleRate(UInt32 sampleRate, UInt32 bufferSize)
        {
            _config.audioSampleRate = sampleRate;
            _config.audioBufferSize = bufferSize;
            Reconfigure();
        }

        public void SetSmoothing(int smoothing)
        {
            var result = CAPI.ovrAvatar2LipSync_SetSmoothing(_context, smoothing);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogWarning($"ovrAvatar2LipSync_SetSmoothing failed with {result}");
            }
        }

        public void EnableViseme(CAPI.ovrAvatar2Viseme viseme)
        {
            var result = CAPI.ovrAvatar2LipSync_EnableViseme(_context, viseme);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogWarning($"ovrAvatar2LipSync_EnableViseme failed with {result}");
            }
        }

        public void DisableViseme(CAPI.ovrAvatar2Viseme viseme)
        {
            var result = CAPI.ovrAvatar2LipSync_DisableViseme(_context, viseme);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogWarning($"ovrAvatar2LipSync_DisableViseme failed with {result}");
            }
        }

        public void SetViseme(CAPI.ovrAvatar2Viseme viseme, int amount)
        {
            var result = CAPI.ovrAvatar2LipSync_SetViseme(_context, viseme, amount);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogWarning($"ovrAvatar2LipSync_SetViseme failed with {result}");
            }
        }

        public void SetLaughter(int amount)
        {
            var result = CAPI.ovrAvatar2LipSync_SetLaughter(_context, amount);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogWarning($"ovrAvatar2LipSync_SetLaughter failed with {result}");
            }
        }

        #endregion

        private CAPI.ovrAvatar2LipSyncContext? CreateLipSyncContext()
        {
            var lipSyncContext = new CAPI.ovrAvatar2LipSyncContext();
            var result = CAPI.ovrAvatar2LipSync_InitializeContext(_context, ref lipSyncContext);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"ovrAvatar2LipSync_InitializeContext failed with {result}");
                return null;
            }

            return lipSyncContext;
        }

        private void ReleaseUnmanagedResources()
        {
            if (_context == IntPtr.Zero) return;
            var result = CAPI.ovrAvatar2LipSync_DestroyProvider(_context);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"ovrAvatar2LipSync_DestroyProvider failed with {result}");
            }

            _context = IntPtr.Zero;
        }

        protected override bool GetLipSyncState(OvrAvatarLipSyncState lipsyncState)
        {
            if (_nativeCallbacks.lipSyncCallback != null &&
                   _nativeCallbacks.lipSyncCallback(out var nativeState, _nativeCallbacks.context))
            {
                lipsyncState.FromNative(ref nativeState);
                return true;
            }
            return false;
        }

        private void Reconfigure()
        {
            var result = CAPI.ovrAvatar2LipSync_ReconfigureProvider(_context, ref _config);
            if (!result.IsSuccess())
            {
                OvrAvatarLog.LogWarning($"ovrAvatar2LipSync_ReconfigureProvider failed with {result}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            base.Dispose(disposing);
        }

        ~OvrAvatarVisemeContext()
        {
            ReleaseUnmanagedResources();
        }
    }
}
