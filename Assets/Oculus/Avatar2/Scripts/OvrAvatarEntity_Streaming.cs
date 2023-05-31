using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

namespace Oculus.Avatar2
{
    public partial class OvrAvatarEntity : MonoBehaviour
    {
        public enum StreamLOD
        {
            Full = CAPI.ovrAvatar2StreamLOD.Full,
            High = CAPI.ovrAvatar2StreamLOD.High,
            Medium = CAPI.ovrAvatar2StreamLOD.Medium,
            Low = CAPI.ovrAvatar2StreamLOD.Low
        }

        public const StreamLOD StreamLODFirst = StreamLOD.Full;
        public const StreamLOD StreamLODLast = StreamLOD.Low;
        public const int StreamLODCount = (StreamLOD.Low - StreamLOD.Full) + 1;

        // Public Properties
        public bool IsLocal => _isLocal;
        public StreamLOD activeStreamLod => (StreamLOD)_activeStreamLod;

        [System.Obsolete("Use GetLastByteSizeForStreamLod or GetLastByteSizeForLodIndex instead")]
        public IReadOnlyCollection<long> StreamLodBytes => _lastStreamLodByteSize;

        public long GetLastByteSizeForStreamLod(StreamLOD lod) => GetLastByteSizeForLodIndex((int)lod);
        public long GetLastByteSizeForLodIndex(int lodIndex) => _lastStreamLodByteSize[lodIndex];

        // When true, links the network streaming fidelity to the rendering Lod groups
        [HideInInspector]
        public bool useRenderLods = true;

        // Serialized Variables
        [Header("Networking")]
        [SerializeField]
        private bool _isLocal = true;

        // Private Variables
        private CAPI.ovrAvatar2StreamLOD _activeStreamLod = CAPI.ovrAvatar2StreamLOD.Low;
        private long[] _lastStreamLodByteSize = new long[StreamLODCount];

        #region Public Streaming Functions

        public bool RecordStart()
        {
            return CAPI.ovrAvatar2Streaming_RecordStart(entityId)
                .EnsureSuccess("ovrAvatar2Streaming_RecordStart", logScope, this);
        }

        public bool RecordStop()
        {
            return CAPI.ovrAvatar2Streaming_RecordStop(entityId)
                .EnsureSuccess("ovrAvatar2Streaming_RecordStop", logScope, this);
        }

        // TODO: Should probably be internal?
        public bool GetRecordingSize(CAPI.ovrAvatar2StreamLOD lod, out UInt64 bytes)
        {
            return CAPI.ovrAvatar2Streaming_GetRecordingSize(entityId, lod, out bytes)
                .EnsureSuccess("ovrAvatar2Streaming_GetRecordingSize", logScope, this);
        }

        public void SetIsLocal(bool newValue)
        {
            if (IsLocal == newValue) return;

            _isLocal = newValue;
            if (IsCreated)
            {
                SetStreamingPlayback(!IsLocal);
            }
        }

        // I'm not sure what the sideeffects might be of recording snapshots that go unused?
        // At the very least, it doesn't seem like the most performant option.
        internal UInt64 GetRecordingSize(StreamLOD lod)
        {
            var lastSize = GetLastByteSizeForStreamLod(lod);
            if (lastSize > 0)
            {
                return (UInt64)lastSize;
            }
            if (TryRecordSnapshot(lod, out var bytes))
            {
                return bytes;
            }
            return 0;
        }

        public void SetPlaybackTimeDelay(float value)
        {
            var result = CAPI.ovrAvatar2Streaming_SetPlaybackTimeDelay(entityId, value);
            result.LogAssert("ovrAvatar2Streaming_SetPlaybackTimeDelay", logScope, this);
        }

        // Local Avatar
        public byte[] RecordStreamData(StreamLOD lod)
        {
            if (!TryRecordSnapshot(lod, out var bytes))
            {
                return null;
            }

            var lodToUse = (CAPI.ovrAvatar2StreamLOD)lod;
            using (var data = new NativeArray<byte>((int)bytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                bool success;
                unsafe
                {
                    UInt64 bufferSize = bytes;
                    success = CAPI.OvrAvatar2Streaming_SerializeRecording(entityId, lodToUse, data.GetPtr(), ref bufferSize);
                }
                return success ? data.ToArray() : null;
            }
        }

        // Caller owns lifetime of dataBuffer
        [Obsolete("Using fixed size buffers leads to unexpected networking failures. Will be removed in near future. Use RecordStreamData_AutoBuffer to pass buffer by reference, so SDK can resize if needed.")]
        public UInt32 RecordStreamData(StreamLOD lod, in NativeArray<byte> dataBuffer)
        {
            IntPtr dataBufferPtr;
            unsafe { dataBufferPtr = (IntPtr)dataBuffer.GetUnsafePtr(); }
            return RecordStreamData(lod, dataBufferPtr, dataBuffer.GetBufferSize());
        }

        // If data fits w/in the provided buffer, it is used
        // - otherwise buffer is disposed and a new one created to fit requested LOD
        public UInt32 RecordStreamData_AutoBuffer(StreamLOD lod, ref NativeArray<byte> dataBuffer)
        {
            // TODO: When `RecordStreamData` removed, rename `RecordStreamData_AutoBuffer` to `RecordStreamData`
            if (!TryRecordSnapshot(lod, out var bytes))
            {
                return 0;
            }

            int bufferSize = dataBuffer.Length;
            if ((UInt32)bytes > bufferSize)
            {
                if (dataBuffer.IsCreated) { dataBuffer.Dispose(); }

                // TODO: Use allocator type of provided `dataBuffer`?
                dataBuffer = new NativeArray<byte>((int)bytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            var lodToUse = (CAPI.ovrAvatar2StreamLOD)lod;
            bool success;
            unsafe
            {
                UInt64 dataBufferSize = dataBuffer.GetBufferSize();
                success = CAPI.OvrAvatar2Streaming_SerializeRecording(entityId, lodToUse, dataBuffer.GetPtr(), ref dataBufferSize);
            }
            if (!success)
            {
                dataBuffer.Dispose();
                dataBuffer = default;
                return 0;
            }

            return (UInt32)bytes;
        }

        // If data fits w/in the provided buffer, it is used
        // - otherwise buffer is resized
        public UInt32 RecordStreamData_AutoBuffer(StreamLOD lod, ref byte[] dataBuffer)
        {
            // TODO: When `RecordStreamData` removed, rename `RecordStreamData_AutoBuffer` to `RecordStreamData`
            if (!TryRecordSnapshot(lod, out var bytes))
            {
                return 0;
            }

            int bufferSize = dataBuffer.Length;
            if ((UInt32)bytes > bufferSize)
            {
                Array.Resize(ref dataBuffer, (int)bytes);
            }


            bool success;
            unsafe
            {
                fixed (byte* dataBufferPtr = dataBuffer)
                {
                    var lodToUse = (CAPI.ovrAvatar2StreamLOD)lod;
                    var bufferSizeAndBytesWritten = (UInt64)dataBuffer.LongLength;
                    success = CAPI.OvrAvatar2Streaming_SerializeRecording(entityId, lodToUse, dataBufferPtr, ref bufferSizeAndBytesWritten);
                }
            }

            return success ? (UInt32)bytes : 0;
        }

        [Obsolete("Prefer RecordStreamData_AutoBuffer variants")]
        public UInt32 RecordStreamData(StreamLOD lod, IntPtr buffer, UInt32 bufferSize)
        {
            if (!TryRecordSnapshot(lod, out var bytes))
            {
                return 0;
            }

            if ((UInt32)bytes > bufferSize)
            {
                OvrAvatarLog.LogError($"StreamData buffer is too small. Size was {bufferSize} but needed to be {(UInt32)bytes}");
                return 0;
            }

            var lodToUse = (CAPI.ovrAvatar2StreamLOD)lod;
            bool success;
            unsafe
            {
                byte* bufferPtr = (byte*)buffer.ToPointer();
                UInt64 bufferSizeAndBytesWritten = bufferSize;
                success = CAPI.OvrAvatar2Streaming_SerializeRecording(
                    entityId, lodToUse, bufferPtr, ref bufferSizeAndBytesWritten);
            }
            return success ? (UInt32)bytes : 0;
        }

        // Remote Avatar
        public void ForceStreamLod(StreamLOD newLod)
        {
            useRenderLods = false;
            _activeStreamLod = (CAPI.ovrAvatar2StreamLOD)newLod;
        }

        public bool ApplyStreamData(byte[] data)
        {
            if (!_VerifyCanApplyStreaming()) { return false; }

            unsafe
            {
                fixed (byte* dataPtr = data)
                {
                    return _ExecuteApplyStreamData(dataPtr, (UInt32)data.Length);
                }
            }
        }
        public bool ApplyStreamData(in NativeArray<byte> array)
        {
            OvrAvatarLog.AssertConstMessage(array.IsCreated, "NativeArray is not created");
            if (array.GetBufferSize() <= 0)
            {
                OvrAvatarLog.LogError("Invalid buffer size parameter", logScope, this);
                return false;
            }

            if (!_VerifyCanApplyStreaming()) { return false; }

            unsafe
            {
                return _ExecuteApplyStreamData(array.GetPtr(), array.GetBufferSize());
            }
        }
        public bool ApplyStreamData(in NativeSlice<byte> slice)
        {
            if (slice.Length <= 0)
            {
                OvrAvatarLog.LogError("NativeSlice has size 0", logScope, this);
                return false;
            }

            if (!_VerifyCanApplyStreaming()) { return false; }

            unsafe
            {
                return _ExecuteApplyStreamData(slice.GetPtr(), (UInt32)(slice.Length * sizeof(byte)));
            }
        }

        [Obsolete("Prefer ApplyStreamData overrides with explicit container types")]
        public bool ApplyStreamData(IntPtr data, UInt32 size)
        {
            if (!_VerifyCanApplyStreaming()) { return false; }

            unsafe
            {
                return _ExecuteApplyStreamData((byte*)data, size);
            }
        }

        private bool _VerifyCanApplyStreaming()
        {
            if (!IsCreated) { return false; }
            // TODO: This is not entirely sufficient, could have joints but the wrong skeleton :/
            if (!HasJoints) { return false; }
            if (IsLocal)
            {
                OvrAvatarLog.LogWarning(
                    $"Tried to receive network data on a local avatar. Use SetIsLocal first.", logScope, this);
                return false;
            }
            return true;
        }

        private unsafe bool _ExecuteApplyStreamData(byte* dataPtr, UInt32 size)
        {
            OvrAvatarLog.Assert(dataPtr != null);
            OvrAvatarLog.Assert(size > 0);

            var result = CAPI.OvrAvatar2Streaming_DeserializeRecording(entityId, dataPtr, size, this);
            if (!result)
            {
                OvrAvatarLog.LogWarning("Failed to apply stream data", logScope, this);
            }
            return result;
        }

        public CAPI.ovrAvatar2StreamingPlaybackState? GetStreamingPlaybackState()
        {
            var result = CAPI.ovrAvatar2Streaming_GetPlaybackState(entityId, out var playbackState);
            switch (result)
            {
                case CAPI.ovrAvatar2Result.Success:
                    return playbackState;
                case CAPI.ovrAvatar2Result.NotFound:
                    return null;
                default:

                    result.LogAssert("ovrAvatar2Streaming_GetPlaybackState", logScope, this);
                    return null;
            }
        }

        #endregion Public Streaming Functions

        protected bool SetStreamingPlayback(bool shouldStart)
        {
            CAPI.ovrAvatar2Result result;
            if (shouldStart)
            {
                result = CAPI.ovrAvatar2Streaming_PlaybackStart(entityId);
                result.LogAssert("ovrAvatar2Streaming_PlaybackStart", logScope, this);
            }
            else
            {
                result = CAPI.ovrAvatar2Streaming_PlaybackStop(entityId);
                result.LogAssert("ovrAvatar2Streaming_PlaybackStop", logScope, this);
            }

            return result.IsSuccess();
        }

        protected void ComputeNetworkLod()
        {
            var newLod = CAPI.ovrAvatar2StreamLOD.Low;

            var lodLevel = AvatarLOD.overrideLOD ? AvatarLOD.overrideLevel : AvatarLOD.wantedLevel;
            if (lodLevel != -1)
            {
                if (lodLevel < 1)
                {
                    newLod = CAPI.ovrAvatar2StreamLOD.High;
                }
                else if (lodLevel < 2)
                {
                    newLod = CAPI.ovrAvatar2StreamLOD.Medium;
                }
            }

            _activeStreamLod = newLod;
        }

        private bool TryRecordSnapshot(StreamLOD lod, out UInt64 bytes)
        {
            if (!IsCreated)
            {
                OvrAvatarLog.LogError("Cannot record stream data until entity is created", logScope, this);
                bytes = 0;
                return false;
            }

            if (!HasJoints)
            {
                OvrAvatarLog.LogError("Cannot record stream data until entity has loaded a skeleton", logScope, this);
                bytes = 0;
                return false;
            }

            var result = CAPI.ovrAvatar2Streaming_RecordSnapshot(entityId);
            if (!result.EnsureSuccess("ovrAvatar2Streaming_RecordSnapshot", logScope, this))
            {
                bytes = 0;
                return false;
            }

            var lodToUse = (CAPI.ovrAvatar2StreamLOD)lod;
            result = CAPI.ovrAvatar2Streaming_GetRecordingSize(entityId, lodToUse, out bytes);
            if (!result.EnsureSuccess("ovrAvatar2Streaming_GetRecordingSize", logScope, this))
            {
                // TODO: Is there any necessary "cleanup" after `ovrAvatar2Streaming_RecordSnapshot` was unsuccesful?
                bytes = 0;
                return false;
            }

            _lastStreamLodByteSize[(int)lod] = (long)(bytes);

            return true;
        }
    }
}
