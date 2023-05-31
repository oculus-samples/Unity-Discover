// Due to Unity bug (fixed in version 2021.2), copy to a native array then copy native array to ComputeBuffer in one chunk
// (ComputeBuffer.SetData erases previously set data)
// https://issuetracker.unity3d.com/issues/partial-updates-of-computebuffer-slash-graphicsbuffer-using-setdata-dont-preserve-existing-data-when-using-opengl-es
#if UNITY_2021_2_OR_NEWER
    #define COMPUTE_BUFFER_PARTIAL_UPDATE_ALLOWED
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Oculus.Avatar2;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Oculus.Skinning.GpuSkinning
{
    internal sealed class OvrComputeMeshAnimator : IDisposable
    {
        private const int BAG_OF_UINTS_STRIDE = 4; // in bytes

        private const string LOG_SCOPE = "OvrComputeMeshAnimator";

        private const int WEIGHTS_STRIDE_BYTES = 4; // 32-bit float per morph target
        private const int JOINT_MATRIX_STRIDE_BYTES = 16 * 4; // 4x4 32-bit float matrices per joint matrix
        private const int SLICE_OUTPUT_STRIDE_BYTES = 4; // a single 32-bit int

        private const string KERNEL_NAME = "CSMain";
        private const string HAS_TANGENTS_KERNEL_NAME = "CSMainWithTangents";
        private const string DOUBLE_BUFFER_KERNEL_NAME = "CSMainDoubleBuffer";
        private const string HAS_TANGENTS_DOUBLE_BUFFER_KERNEL_NAME = "CSMainDoubleBufferWithTangents";
        private const string TRIPLE_BUFFER_KERNEL_NAME = "CSMainTripleBuffer";
        private const string HAS_TANGENTS_TRIPLE_BUFFER_KERNEL_NAME = "CSMainTripleBufferWithTangents";

        public OvrSkinningTypes.SkinningQuality SkinningQuality { get; set; }

        public enum MaxOutputFrames
        {
            ONE = 1,
            TWO = 2,
            THREE = 3,
        }

        private int MaxJointsToSkin
        {
            get
            {
                switch (SkinningQuality)
                {
                    case OvrSkinningTypes.SkinningQuality.Bone4:
                        return 4;
                    case OvrSkinningTypes.SkinningQuality.Bone2:
                        return 2;
                    case OvrSkinningTypes.SkinningQuality.Bone1:
                        return 1;
                    default:
                        return 0;
                }
            }
        }

        public OvrComputeMeshAnimator(
            ComputeShader shader,
            int numMeshVerts,
            int numMeshMorphTargets,
            int numMeshJoints,
            OvrAvatarComputeSkinnedPrimitive gpuPrimitive,
            bool hasTangents,
            MaxOutputFrames maxOutputFrames)
        {
            Debug.Assert(shader != null);
            Debug.Assert(gpuPrimitive != null);
            Debug.Assert(gpuPrimitive.SourceMetaData != null);

            CheckPropertyIdInit();

            _shader = shader;

            _numMorphedVertices = gpuPrimitive.SourceMetaData.numMorphedVerts;
            _numPlainVerts = gpuPrimitive.SourceMetaData.numVertsNoJointsOrMorphs;
            _numSkinningOnlyVerts = numMeshVerts - _numPlainVerts - _numMorphedVertices;
            _maxOutputFrames = maxOutputFrames;
            _hasMorphTargets = numMeshMorphTargets > 0 && _numMorphedVertices > 0;
            _hasJoints = numMeshJoints > 0;

            _numMorphTargetWeights = numMeshMorphTargets;
            _numJointMatrices = numMeshJoints * 2; // * 2 due to interleaved normal matrices

            // The primitive still "owns" the static compute shader
            _staticBuffer = gpuPrimitive.StaticDataComputeBuffer;

            _positionOutputBias = gpuPrimitive.SourceMetaData.positionOutputBias;
            _positionOutputScale = gpuPrimitive.SourceMetaData.positionOutputScale;

            _inputPositionPrecision = gpuPrimitive.SourceMetaData.inputPositionPrecision;
            _morphDeltasPrecision = gpuPrimitive.SourceMetaData.morphDeltasPrecision;
            _outputPositionPrecision = gpuPrimitive.SourceMetaData.outputPositionPrecision;
            _jointIndicesPrecision = gpuPrimitive.SourceMetaData.jointIndicesPrecision;

            int numOutputSlices = (int)maxOutputFrames;
            CreateDynamicDataBuffer(numMeshVerts, numMeshMorphTargets, numMeshJoints, numOutputSlices);
            CreateOutputBuffers(numMeshVerts, hasTangents, numOutputSlices);
            GetShaderKernel(hasTangents, numOutputSlices);
        }

        public static MaxOutputFrames GetMaxOutputFramesForConfiguration(bool motionSmoothing, bool supportApplicationSpacewarp)
        {
            int numExtraSlicesForMotionSmoothing = motionSmoothing ? 1 : 0;
            int numExtraSlicesForAppSpacewarp = supportApplicationSpacewarp ? 1 : 0;
            return (MaxOutputFrames)(1 + numExtraSlicesForAppSpacewarp + numExtraSlicesForMotionSmoothing);
        }

        public ComputeBuffer GetPositionOutputBuffer()
        {
            return _positionOutputBuffer;
        }

        public ComputeBuffer GetFrenetOutputBuffer()
        {
            return _frenetOutputBuffer;
        }

        public Vector3 GetPositionOutputScale()
        {
            return _positionOutputScale;
        }

        public Vector3 GetPositionOutputBias()
        {
            return _positionOutputBias;
        }

        public void SetMorphTargetWeights(NativeArray<float> weights)
        {
            Debug.Assert(weights.Length == _numMorphTargetWeights);
            if (_hasMorphTargets)
            {
                _dynamicBufferUpdater.SetMorphTargetWeights(weights);
                _outputNeedsUpdating = true;
            }
        }

        public void SetJointMatrices(NativeArray<Matrix4x4> matrices)
        {
            Debug.Assert(matrices.Length == _numJointMatrices);
            if (_hasJoints )
            {
                _dynamicBufferUpdater.SetJointMatrices(matrices);
                _outputNeedsUpdating = true;
            }
        }

        public void DispatchAndUpdateOutputs()
        {
            if (!_outputNeedsUpdating) { return; }

            SetShaderKeywordsAndProperties();
            _dynamicBufferUpdater.UpdateComputeBufferBeforeDispatch();

            // Potentially do three dispatches if there are morphs and skinning and neither
            int startIndex = 0;
            if (_hasMorphTargets)
            {
                const bool applyMorphs = true;
                ComputeShaderDispatch(startIndex, _numMorphedVertices, applyMorphs, _hasJoints);
                startIndex += _numMorphedVertices;
            }

            if (_hasJoints)
            {
                const bool applyMorphs = false;
                const bool applyJoints = true;
                ComputeShaderDispatch(startIndex, _numSkinningOnlyVerts, applyMorphs, applyJoints);
                startIndex += _numSkinningOnlyVerts;
            }

            if (_numPlainVerts != 0)
            {
                const bool applyMorphs = false;
                const bool applyJoints = false;
                ComputeShaderDispatch(startIndex, _numPlainVerts, applyMorphs, applyJoints);
            }

            _outputNeedsUpdating = false;
        }

        public void SetWriteDestinationInDynamicBuffer(SkinningOutputFrame writeDestination)
        {
            Debug.Assert((int)writeDestination < (int)_maxOutputFrames);
            var writeDestinationAsUint = (UInt32)SkinningOutputFrame.FrameZero;
            _dynamicBufferUpdater.SetWriteDestinationInDynamicBuffer(writeDestinationAsUint);
        }

        private void ComputeShaderDispatch(int startIndex, int numVerts, bool applyMorphs, bool applyJoints)
        {
            int numWorkGroups = (numVerts + _threadsPerWorkGroup - 1) / _threadsPerWorkGroup;

            int dispatchEndIndex = startIndex + numVerts - 1;
            SetPerDispatchShaderProperties(startIndex, dispatchEndIndex, applyMorphs, applyJoints);
            _shader.Dispatch(_shaderKernel, numWorkGroups, 1, 1);
        }

        private void CreateDynamicDataBuffer(
            int numMeshVerts,
            int numMeshMorphTargets,
            int numMeshJoints,
            int numOutputSlicesPerAttribute)
        {
            // Data layout for dynamic buffer is
            // [numMeshVerts number of VertexInstanceData] ->
            // a single MeshInstanceMetaData ->
            // [numMorphTargets floats for morph target weights] ->
            // [2 * numJoints float4x4 for joint matrices] ->
            // a single uint for the "double buffer slice"

            // Calculate the necessary offset/sizes
            int vertexInstanceDataSizePerInstance = UnsafeUtility.SizeOf<VertexInstanceData>();
            int totalVertexInstancesSize = vertexInstanceDataSizePerInstance * numMeshVerts;
            int meshInstanceDataSize = UnsafeUtility.SizeOf<MeshInstanceMetaData>();
            int morphTargetWeightsSize = WEIGHTS_STRIDE_BYTES * numMeshMorphTargets;
            int jointMatricesSize = numMeshJoints * JOINT_MATRIX_STRIDE_BYTES * 2; // *2 because normal + model matrices
            int sliceOutputSize = numOutputSlicesPerAttribute > 1 ? SLICE_OUTPUT_STRIDE_BYTES : 0;

            int totalDynamicBufferSize =
                totalVertexInstancesSize +
                meshInstanceDataSize +
                morphTargetWeightsSize +
                jointMatricesSize +
                sliceOutputSize;

            const int meshStaticDataOffset = 0; // always 0 (no batching)
            const int vertexInstanceArrayOffset = 0; // always 0 (no batching)
            int meshInstanceDataOffset = totalVertexInstancesSize;
            int morphTargetWeightsOffset = meshInstanceDataOffset + meshInstanceDataSize;
            int jointMatricesOffset = morphTargetWeightsOffset + morphTargetWeightsSize;
            int writeToSliceOffset = jointMatricesOffset + jointMatricesSize;

            /////////////////////////////////////////////////////////////////////////////////////////////////
            // First, create VertexInstanceData structs that will be at the beginning of dynamic data buffer
            var vertexInstanceArray = new NativeArray<VertexInstanceData>(
                numMeshVerts,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            NativeArray<MeshInstanceMetaData> singleMeshInstanceMetaData = default;

            try
            {
                // Fill in vertex instance data
                for (int i = 0; i < numMeshVerts; i++)
                {
                    vertexInstanceArray[i] = new VertexInstanceData
                    {
                        // meshInstanceDataOffsetBytes(x) vertexIndexInMesh(y)
                        properties = new Vector4UInt
                        {
                            x = (uint)meshInstanceDataOffset,
                            y = (uint)i
                        }
                    };
                }

                ////////////////////////////////////////////////////////////////////////////////////////////////////
                // Now write out the single MeshInstanceMetaData
                singleMeshInstanceMetaData = new NativeArray<MeshInstanceMetaData>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);

                singleMeshInstanceMetaData[0] = new MeshInstanceMetaData
                {
                    // meshStaticDataOffsetBytes(x)
                    // morphTargetWeightsOffsetBytes(y)
                    // jointMatricesOffsetBytes(z)
                    // writeToSecondSliceAddress (w) (if double buffered)
                    properties = new Vector4UInt
                    {
                        x = meshStaticDataOffset,
                        y = (uint)morphTargetWeightsOffset,
                        z = (uint)jointMatricesOffset,
                        w = (uint)writeToSliceOffset
                    },
                    outputFrenetOffsets = new Vector4UInt
                    {
                        // outputPositionBufferOffsetBytes(x)
                        // outputFrenetBufferOffsetBytes(y)
                        x = 0,
                        y = 0
                    }
                };

#if (COMPUTE_BUFFER_PARTIAL_UPDATE_ALLOWED)
                    _dynamicBufferUpdater = new DynamicBufferDirectUpdater(
                        totalDynamicBufferSize,
                        vertexInstanceArray,
                        vertexInstanceArrayOffset,
                        singleMeshInstanceMetaData,
                        meshInstanceDataOffset,
                        morphTargetWeightsOffset,
                        jointMatricesOffset,
                        writeToSliceOffset);
#else
                    _dynamicBufferUpdater = new DynamicBufferUpdaterViaNativeArray(
                        totalDynamicBufferSize,
                        vertexInstanceArray,
                        vertexInstanceArrayOffset,
                        singleMeshInstanceMetaData,
                        meshInstanceDataOffset,
                        morphTargetWeightsOffset,
                        jointMatricesOffset,
                        writeToSliceOffset);
#endif
            }
            finally
            {
                vertexInstanceArray.Dispose();
                if (singleMeshInstanceMetaData.IsCreated)
                {
                    singleMeshInstanceMetaData.Dispose();
                }
            }
        }

        private static void SetComputeBufferDataFromNativeArray<T>(
            ComputeBuffer computeBuffer,
            NativeArray<T> nativeArr,
            int byteOffsetInComputeBuffer) where T : struct
        {
            int stride = computeBuffer.stride;
            var arrayOfUints = nativeArr.Reinterpret<uint>(UnsafeUtility.SizeOf<T>());
            computeBuffer.SetData(
                arrayOfUints,
                0,
                byteOffsetInComputeBuffer / stride,
                arrayOfUints.Length);
        }

        private static void CopyNativeArrayToBackingBuffer<T>(
            NativeArray<byte> backingBuffer,
            NativeArray<T> nativeArr,
            int byteOffset) where T : struct
        {
            var arrayOfBytes = nativeArr.Reinterpret<byte>(UnsafeUtility.SizeOf<T>());
            var sourceSlice = arrayOfBytes.Slice();
            var destSlice = backingBuffer.Slice(byteOffset, arrayOfBytes.Length);
            destSlice.CopyFrom(sourceSlice);
        }

        private void CreateOutputBuffers(
            int numMeshVerts,
            bool hasTangents,
            int numOutputSlices)
        {
            _positionOutputBuffer =
                new ComputeBuffer(
                    GetPositionOutputBufferSize(numMeshVerts, _outputPositionPrecision, numOutputSlices),
                    BAG_OF_UINTS_STRIDE,
                    ComputeBufferType.Structured);
            _frenetOutputBuffer = new ComputeBuffer(
                GetFrenetOutputBufferSize(numMeshVerts, hasTangents, numOutputSlices),
                BAG_OF_UINTS_STRIDE,
                ComputeBufferType.Structured);
        }

        private void GetShaderKernel(
            bool hasTangents,
            int numOutputSlices)
        {
            string kernelName = String.Empty;
            switch (numOutputSlices)
            {
                case 1:
                    kernelName = hasTangents ? HAS_TANGENTS_KERNEL_NAME : KERNEL_NAME;
                    break;
                case 2:
                    kernelName = hasTangents ? HAS_TANGENTS_DOUBLE_BUFFER_KERNEL_NAME : DOUBLE_BUFFER_KERNEL_NAME;
                    break;
                case 3:
                    kernelName = hasTangents ? HAS_TANGENTS_TRIPLE_BUFFER_KERNEL_NAME : TRIPLE_BUFFER_KERNEL_NAME;
                    break;
            }

            if (_shader.HasKernel(kernelName))
            {
                _shaderKernel = _shader.FindKernel(kernelName);
            }
            else
            {
                // No kernel, just default to 0
                OvrAvatarLog.LogWarning("Error finding compute shader kernel, using default compute kernel", LOG_SCOPE);
                kernelName = KERNEL_NAME;

                _shaderKernel = 0;
            }

            _shader.GetKernelThreadGroupSizes(_shaderKernel, out uint threadGroupSizeU, out _, out _);
            _threadsPerWorkGroup = (int)threadGroupSizeU;
        }

        private void SetShaderKeywordsAndProperties()
        {
            // Unfortunately compute shader keywords aren't supported in Unity 2019, so do nothing
            // in regards to keywords

            // To prevent needing a copy of the compute shader, just set properties needed (saves CPU time?)
            _shader.SetInt(_propertyIds.MaxJointsToSkinPropId, MaxJointsToSkin);

            _shader.SetBuffer(_shaderKernel, _propertyIds.StaticBufferPropId, _staticBuffer);
            _shader.SetBuffer(_shaderKernel, _propertyIds.DynamicBufferPropId, _dynamicBufferUpdater.DynamicBuffer);
            _shader.SetBuffer(_shaderKernel, _propertyIds.PositionOutputBufferPropId, _positionOutputBuffer);
            _shader.SetBuffer(_shaderKernel, _propertyIds.FrenetOutputBufferPropId, _frenetOutputBuffer);
            _shader.SetInt(
                _propertyIds.VertexPositionDataFormatPropId,
                OvrComputeUtils.GetEncodingPrecisionShaderValue(_inputPositionPrecision));
            _shader.SetInt(
                _propertyIds.MorphTargetDeltasFormatPropId,
                OvrComputeUtils.GetEncodingPrecisionShaderValue(_morphDeltasPrecision));
            _shader.SetInt(
                _propertyIds.PositionOutputDataFormatPropId,
                OvrComputeUtils.GetEncodingPrecisionShaderValue(_outputPositionPrecision));
            _shader.SetInt(
                _propertyIds.JointIndicesDataFormatPropId,
                OvrComputeUtils.GetEncodingPrecisionShaderValue(_jointIndicesPrecision));
        }

        private void SetPerDispatchShaderProperties(int dispatchStartVertIndex, int dispatchEndVertIndex, bool applyMorphs, bool applyJoints)
        {
            _shader.SetInt(_propertyIds.DispatchVertStartIndexPropId, dispatchStartVertIndex);
            _shader.SetInt(_propertyIds.DispatchVertEndIndexPropId, dispatchEndVertIndex);

            _shader.SetBool(_propertyIds.EnableMorphTargetsPropId, applyMorphs);
            _shader.SetBool(_propertyIds.EnableSkinningPropId, applyJoints);
        }

        private static int GetPositionOutputBufferSize(
            int numMeshVerts,
            GpuSkinningConfiguration.TexturePrecision positionEncodingPrecision,
            int numOutputSlices)
        {
            int positionStrideBytes = 0;
            switch (positionEncodingPrecision)
            {
                case GpuSkinningConfiguration.TexturePrecision.Float:
                    positionStrideBytes = 4 * 4; // 4 32-bit floats
                    break;
                case GpuSkinningConfiguration.TexturePrecision.Half:
                case GpuSkinningConfiguration.TexturePrecision.Unorm16:
                    positionStrideBytes = 4 * 2; // 4 16-bit halfs/unorms
                    break;
                default:
                    // Unsupported output format, this should probably be handled
                    // further up the chain, but catch here just in case
                    OvrAvatarLog.LogError($"Unsupported format {positionEncodingPrecision}.");
                    break;
            }

            int positionOutputSize = positionStrideBytes * numMeshVerts * numOutputSlices;

            return positionOutputSize / BAG_OF_UINTS_STRIDE;
        }

        private static int GetFrenetOutputBufferSize(int numMeshVerts, bool hasTangents, int numOutputSlices)
        {
            const int FRENET_ATTRIBUTE_STRIDE_BYTES = 4; // only supporting 10-10-10-2 format for normal/tangents
            int frenetOutputSize = FRENET_ATTRIBUTE_STRIDE_BYTES * numMeshVerts * (hasTangents ? 2 : 1) * numOutputSlices;

            return frenetOutputSize / BAG_OF_UINTS_STRIDE;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Vector4UInt
        {
            public uint x, y, z, w;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MeshInstanceMetaData {
            public Vector4UInt properties;
            public Vector4UInt outputFrenetOffsets;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VertexInstanceData {
            public Vector4UInt properties;
        }

        private readonly int _numMorphedVertices;
        private readonly int _numSkinningOnlyVerts;
        private readonly int _numPlainVerts; //no morphs or skinning

        private readonly int _numMorphTargetWeights;
        private readonly int _numJointMatrices;

        private readonly MaxOutputFrames _maxOutputFrames;

        private int _shaderKernel;
        private int _threadsPerWorkGroup;
        private ComputeShader _shader;

        private readonly bool _hasMorphTargets;
        private readonly bool _hasJoints;

        private bool _outputNeedsUpdating;

        private readonly CAPI.ovrGpuSkinningEncodingPrecision _inputPositionPrecision;
        private readonly CAPI.ovrGpuSkinningEncodingPrecision _morphDeltasPrecision;
        private readonly CAPI.ovrGpuSkinningEncodingPrecision _jointIndicesPrecision;
        private readonly GpuSkinningConfiguration.TexturePrecision _outputPositionPrecision;

        private readonly ComputeBuffer _staticBuffer;
        private ComputeBuffer _positionOutputBuffer;
        private ComputeBuffer _frenetOutputBuffer;

        private DynamicBufferUpdaterBase _dynamicBufferUpdater;

        private readonly Vector3 _positionOutputScale;
        private readonly Vector3 _positionOutputBias;

        private static ComputePropertyIds _propertyIds = default;
        private static void CheckPropertyIdInit()
        {
            if (!_propertyIds.IsValid)
            {
                _propertyIds = new ComputePropertyIds(ComputePropertyIds.InitMethod.PropertyToId);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isDispose)
        {
            if (isDispose)
            {
                _dynamicBufferUpdater?.Dispose();
                _positionOutputBuffer?.Dispose();
                _frenetOutputBuffer?.Dispose();
            }
            else
            {
                if (_dynamicBufferUpdater != null || _positionOutputBuffer != null || _frenetOutputBuffer != null)
                {
                    OvrAvatarLog.LogError($"OvrComputeMeshAnimator was not disposed before being destroyed", LOG_SCOPE);
                }
            }

            _dynamicBufferUpdater = null;
            _positionOutputBuffer = null;
            _frenetOutputBuffer = null;
        }

        ~OvrComputeMeshAnimator()
        {
            Dispose(false);
        }

        private readonly struct ComputePropertyIds
        {
            public readonly int StaticBufferPropId;
            public readonly int DynamicBufferPropId;
            public readonly int PositionOutputBufferPropId;
            public readonly int FrenetOutputBufferPropId;

            public readonly int DispatchVertStartIndexPropId;
            public readonly int DispatchVertEndIndexPropId;

            public readonly int EnableMorphTargetsPropId;
            public readonly int EnableSkinningPropId;
            public readonly int MaxJointsToSkinPropId;

            public readonly int VertexPositionDataFormatPropId;
            public readonly int MorphTargetDeltasFormatPropId;
            public readonly int PositionOutputDataFormatPropId;
            public readonly int JointIndicesDataFormatPropId;

            // These will both be 0 if default initialized, otherwise they are guaranteed unique
            public bool IsValid => StaticBufferPropId != DynamicBufferPropId;

            public enum InitMethod { PropertyToId }
            public ComputePropertyIds(InitMethod initMethod)
            {
                StaticBufferPropId = Shader.PropertyToID("_StaticDataBuffer");
                DynamicBufferPropId = Shader.PropertyToID("_DynamicDataBuffer");
                PositionOutputBufferPropId = Shader.PropertyToID("_PositionOutputBuffer");
                FrenetOutputBufferPropId = Shader.PropertyToID("_FrenetOutputBuffer");

                DispatchVertStartIndexPropId = Shader.PropertyToID("_DispatchStartVertIndex");
                DispatchVertEndIndexPropId = Shader.PropertyToID("_DispatchEndVertIndex");

                EnableMorphTargetsPropId = Shader.PropertyToID("_EnableMorphTargets");
                EnableSkinningPropId = Shader.PropertyToID("_EnableSkinning");
                MaxJointsToSkinPropId = Shader.PropertyToID("_MaxJointsToSkin");

                VertexPositionDataFormatPropId = Shader.PropertyToID("_VertexPositionsDataFormat");
                MorphTargetDeltasFormatPropId = Shader.PropertyToID("_MorphTargetDeltasDataFormat");
                PositionOutputDataFormatPropId = Shader.PropertyToID("_PositionOutputBufferDataFormat");
                JointIndicesDataFormatPropId = Shader.PropertyToID("_JointIndicesDataFormat");
            }
        }

        private abstract class DynamicBufferUpdaterBase : IDisposable
        {
            private readonly ComputeBuffer _dynamicBuffer;

            protected int _weightsOffsetBytes;
            protected int _matricesOffsetBytes;
            protected int _writeDestinationOffsetBytes;

            public abstract void SetMorphTargetWeights(NativeArray<float> weights);
            public abstract void SetJointMatrices(NativeArray<Matrix4x4> matrices);
            public abstract void SetWriteDestinationInDynamicBuffer(uint writeDestination);

            public abstract void UpdateComputeBufferBeforeDispatch();

            public ComputeBuffer DynamicBuffer => _dynamicBuffer;

            protected DynamicBufferUpdaterBase(
                int dynamicBufferSizeBytes,
                int weightsOffsetBytes,
                int matricesOffsetBytes,
                int writeDestinationOffsetBytes)
            {
                _weightsOffsetBytes = weightsOffsetBytes;
                _matricesOffsetBytes = matricesOffsetBytes;
                _writeDestinationOffsetBytes = writeDestinationOffsetBytes;

                _dynamicBuffer = new ComputeBuffer(
                    dynamicBufferSizeBytes / BAG_OF_UINTS_STRIDE,
                    BAG_OF_UINTS_STRIDE,
                    ComputeBufferType.Raw);
            }
            public virtual void Dispose()
            {
                _dynamicBuffer.Dispose();
            }
        }

        private class DynamicBufferDirectUpdater : DynamicBufferUpdaterBase
        {
            private readonly List<uint> _writeDestinationList = new List<uint>(1);

            public DynamicBufferDirectUpdater(
                int dynamicBufferSizeBytes,
                NativeArray<VertexInstanceData> vertexInstanceDataArray,
                int vertexInstanceDataOffsetBytes,
                NativeArray<MeshInstanceMetaData> meshInstancesMetaData,
                int meshInstancesMetaDataOffsetBytes,
                int weightsOffsetBytes,
                int matricesOffsetBytes,
                int writeDestinationOffsetBytes) :
                base(
                    dynamicBufferSizeBytes,
                    weightsOffsetBytes,
                    matricesOffsetBytes,
                    writeDestinationOffsetBytes)
            {
                SetComputeBufferDataFromNativeArray(
                    DynamicBuffer,
                    vertexInstanceDataArray,
                    vertexInstanceDataOffsetBytes);

                SetComputeBufferDataFromNativeArray(
                    DynamicBuffer,
                    meshInstancesMetaData,
                    meshInstancesMetaDataOffsetBytes);
            }

            public override void SetMorphTargetWeights(NativeArray<float> weights)
            {
                SetComputeBufferDataFromNativeArray(
                    DynamicBuffer,
                    weights,
                    _weightsOffsetBytes);
            }

            public override void SetJointMatrices(NativeArray<Matrix4x4> matrices)
            {
                SetComputeBufferDataFromNativeArray(
                    DynamicBuffer,
                    matrices,
                    _matricesOffsetBytes);
            }

            public override void SetWriteDestinationInDynamicBuffer(uint writeDestinationAsUint)
            {
                _writeDestinationList[0] = writeDestinationAsUint;

                int stride = DynamicBuffer.stride;
                DynamicBuffer.SetData(
                    _writeDestinationList,
                    0,
                    _writeDestinationOffsetBytes / stride,
                    _writeDestinationList.Count);
            }

            public override void UpdateComputeBufferBeforeDispatch()
            {
                // Intentionally empty
            }
        }

        // Due to a Unity bug, have a version which updates a NativeArray and then copies
        // that whole to the compute buffer, once per frame
        private class DynamicBufferUpdaterViaNativeArray : DynamicBufferUpdaterBase
        {
            private NativeArray<byte> _backingBuffer;

            public DynamicBufferUpdaterViaNativeArray(
                int dynamicBufferSizeBytes,
                NativeArray<VertexInstanceData> vertexInstanceDataArray,
                int vertexInstanceDataOffsetBytes,
                NativeArray<MeshInstanceMetaData> meshInstancesMetaData,
                int meshInstancesMetaDataOffsetBytes,
                int weightsOffsetBytes,
                int matricesOffsetBytes,
                int writeDestinationOffsetBytes) :
                base(
                    dynamicBufferSizeBytes,
                    weightsOffsetBytes,
                    matricesOffsetBytes,
                    writeDestinationOffsetBytes)
            {
                // Declare a "backing" native array that is the same size
                // as the compute buffer which is updated and then copied to compute buffer
                _backingBuffer = new NativeArray<byte>(
                    dynamicBufferSizeBytes,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);

                CopyNativeArrayToBackingBuffer(
                    _backingBuffer,
                    vertexInstanceDataArray,
                    vertexInstanceDataOffsetBytes);
                CopyNativeArrayToBackingBuffer(
                    _backingBuffer,
                    meshInstancesMetaData,
                    meshInstancesMetaDataOffsetBytes);
            }

            public override void Dispose()
            {
                base.Dispose();

                if (_backingBuffer.IsCreated)
                {
                    _backingBuffer.Dispose();
                }
            }

            public override void SetMorphTargetWeights(NativeArray<float> weights)
            {
                CopyNativeArrayToBackingBuffer(
                    _backingBuffer,
                    weights,
                    _weightsOffsetBytes);
            }

            public override void SetJointMatrices(NativeArray<Matrix4x4> matrices)
            {
                CopyNativeArrayToBackingBuffer(
                    _backingBuffer,
                    matrices,
                    _matricesOffsetBytes);
            }

            public override void SetWriteDestinationInDynamicBuffer(uint writeDestinationAsUint)
            {
                _backingBuffer.ReinterpretStore(_writeDestinationOffsetBytes, writeDestinationAsUint);
            }

            public override void UpdateComputeBufferBeforeDispatch()
            {
                // Copy whole thing to compute buffer
                SetComputeBufferDataFromNativeArray(DynamicBuffer, _backingBuffer, 0);
            }
        }
    }
}
