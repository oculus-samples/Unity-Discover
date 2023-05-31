using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

// This holds a pool of compute buffers that can be used by many draw calls.
// These buffers are unsynchronized, the memory returned is GPU memory(when possible).
// Currently there are 2 pools, one for joints, and one for morph target weights.
//
// Joints: This buffer is structured(shader storage buffer). It will be read from main memory
//         in the shader. Use setBuffer on material.
// Weights: For morph target weights. This will be in constant(local) memory. Must be a multiple of 256 bytes
//          and deal with std140. Use setContantBuffer on material.
internal class OvrComputeBufferPool : System.IDisposable
{
    // max active avatars, plus 4 extras(can go over by a couple for 1-2 frames sometimes)
    private const int BUFFER_SIZE = 36;
    // 3 "should" be enough for VR(2 if using low latency mode).
    // use 4 just in case, might be needed in Editor. Increase if seeing really strange behavior
    // outside of VR. If not enough, you could be writing to memory that the GPU is actively using to render.
    private const int NUM_BUFFERS = 4;

    private const int VECTOR4_SIZE_BYTES = sizeof(float) * 4;
    private const int BYTES_PER_MATRIX = sizeof(float) * 16;

    // Our glb encodes joint indices in 8 bits currently.
    // Currently we use 134 bones(8/3/2022). Its always creeping up though.
    // Leave some extra for future. This number can't be increased beyond 254 though
    // glb reserves some values, so only get 254 max in 8 bits.
    internal const int MaxJoints = 160;

    internal const int JointDataSize = 2 * BYTES_PER_MATRIX;
    [StructLayout(LayoutKind.Explicit, Size = JointDataSize)]
    internal struct JointData
    {
        [FieldOffset(0)] public Matrix4x4 transform;
        [FieldOffset(BYTES_PER_MATRIX)] public Matrix4x4 normalTransform;
    }

    // Currently at 103(8/3/2022). some extra, round to next 256 byte alignment.
    // Note, because of std140, access this as a vec4 in shader to avoid having to pad here.
    internal const int MAX_WEIGHTS = 128;
    internal const int WeightsSize = sizeof(float) * MAX_WEIGHTS;

    private const int JOINTS_PER_BUFFER = BUFFER_SIZE * MaxJoints;
    private const int WEIGHTS_PER_BUFFER = BUFFER_SIZE * MAX_WEIGHTS;

    internal OvrComputeBufferPool()
    {
        _jointsBuffer = new ComputeBuffer(JOINTS_PER_BUFFER * NUM_BUFFERS, JointDataSize, ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);
        // TODO: really should be a constant buffer, so it can live in on chip memory instead of main memory. Doesn't work in Unity 2020 though. If it is working
        // in a renderdoc capture you should see a call to glBindBufferRange instead of glBindBufferBase when using a constant buffer.
        _weightsBuffer = new ComputeBuffer(WEIGHTS_PER_BUFFER * NUM_BUFFERS, sizeof(float), ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);

        _jointsBuffer.name = "OVR Avatar GPU-Skinning Joint Buffer";
        _weightsBuffer.name = "OVR Avatar GPU-Skinning Weight Buffer";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool isMainThread)
    {
        _jointsBuffer.Release();
        _weightsBuffer.Release();
    }

    ~OvrComputeBufferPool()
    {
        Dispose(false);
    }

    public void StartFrame()
    {
        Debug.Assert(_currentBuffer < NUM_BUFFERS);
        var dataJoints = _jointsBuffer.BeginWrite<JointData>(_currentBuffer * JOINTS_PER_BUFFER, JOINTS_PER_BUFFER);
        var dataWeights = _weightsBuffer.BeginWrite<float>(_currentBuffer * WEIGHTS_PER_BUFFER, WEIGHTS_PER_BUFFER);
        unsafe
        {
            _jointMappedData = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(dataJoints);
            _weightMappedData = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(dataWeights);
        }
        _jointNumberWritten = 0;
        _weightsNumberWritten = 0;
    }

    public void EndFrame()
    {
        _jointsBuffer.EndWrite<JointData>(_jointNumberWritten * MaxJoints);
        _weightsBuffer.EndWrite<float>(_weightsNumberWritten * MAX_WEIGHTS);
        _currentBuffer = (_currentBuffer + 1) % NUM_BUFFERS;
    }

    public struct EntryJoints
    {
        public IntPtr Data;
        public int JointOffset;
    }

    public EntryJoints GetNextEntryJoints()
    {
        Debug.Assert(_jointNumberWritten < BUFFER_SIZE, "Too many joint entries requested. increase BUFFER_SIZE");
        EntryJoints result;
        result.JointOffset = _currentBuffer * JOINTS_PER_BUFFER + (_jointNumberWritten * MaxJoints);
        unsafe
        {
            Debug.Assert(_jointMappedData != null, "Calling GetNextEntryJoints outside of a frame");
            var jointSet = ((JointData*)_jointMappedData) + _jointNumberWritten * MaxJoints;
            result.Data = (IntPtr)jointSet;
        }
        ++_jointNumberWritten;
        return result;
    }

    public struct EntryWeights
    {
        public IntPtr Data;
        public int Offset;
    }

    public EntryWeights GetNextEntryWeights(int numMorphTargets)
    {
        Debug.Assert(numMorphTargets <= MAX_WEIGHTS, "Too many morph targets, increase MAX_WEIGHTS");
        Debug.Assert(_weightsNumberWritten < BUFFER_SIZE, "Too many weight entries requested. increase BUFFER_SIZE");
        EntryWeights result;
        result.Offset = _currentBuffer * WEIGHTS_PER_BUFFER + (_weightsNumberWritten * MAX_WEIGHTS);
        unsafe
        {
            Debug.Assert(_weightMappedData != null, "Calling GetNextEntryWeights outside of a frame");
            var weights = ((float*)_weightMappedData) + (_weightsNumberWritten * MAX_WEIGHTS);
            result.Data = (IntPtr)weights;
        }
        ++_weightsNumberWritten;
        return result;
    }

    internal ComputeBuffer GetJointBuffer()
    {
        return _jointsBuffer;
    }

    internal ComputeBuffer GetWeightsBuffer()
    {
        return _weightsBuffer;
    }

    private readonly ComputeBuffer _jointsBuffer;
    private readonly ComputeBuffer _weightsBuffer;

    //Note, these is only valid between StartFrame/EndFrame
    private unsafe void* _jointMappedData;
    private unsafe void* _weightMappedData;

    int _currentBuffer = 0;
    int _jointNumberWritten;
    int _weightsNumberWritten;
}
