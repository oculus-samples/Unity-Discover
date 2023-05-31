// Due to Unity bug (fixed in version 2021.2), copy to a native array then copy native array to ComputeBuffer in one chunk
// (ComputeBuffer.SetData erases previously set data)
// https://issuetracker.unity3d.com/issues/partial-updates-of-computebuffer-slash-graphicsbuffer-using-setdata-dont-preserve-existing-data-when-using-opengl-es
#if UNITY_2021_2_OR_NEWER
        #define COMPUTE_BUFFER_PARTIAL_UPDATE_ALLOWED
#endif

using Oculus.Skinning;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;
using UnityEngine.Profiling;

namespace Oculus.Avatar2
{
    public sealed class OvrAvatarComputeSkinnedPrimitive : IDisposable
    {
        private const string LOG_SCOPE = "OvrAvatarComputeSkinnedPrimitive";

        internal class StaticMetaData
        {
            // Number of verts in mesh affected by at least one morph target
            public int numMorphedVerts;
            public int numVertsNoJointsOrMorphs;

            public Vector3 positionOutputScale;
            public Vector3 positionOutputBias;

            public CAPI.ovrGpuSkinningEncodingPrecision jointIndicesPrecision;
            public CAPI.ovrGpuSkinningEncodingPrecision inputPositionPrecision;
            public CAPI.ovrGpuSkinningEncodingPrecision morphDeltasPrecision;
            public GpuSkinningConfiguration.TexturePrecision outputPositionPrecision;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Vector4UInt
        {
            public uint x, y, z, w;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ComputeBufferStaticMeshHeader {
            // Static data offsets

            // Offset to an array of positions(x), normals(y), tangents(z), joint weights (w)
            public Vector4UInt attributesAndJointWeightsOffsetBytes;

            // Morph target deltas offset bytes(x), numMorphs(y), numMorphedVertices(z), joint indices(w)
            public Vector4UInt morphTargetInfoAndJointIndicesOffsetBytes;

            // Offset of an array of output indices (x) (yzw) - unused
            public Vector4UInt outputIndexOffset;

            public Vector4 vertexInputPositionBias; // Float4s for alignment, w unused (is this needed?)
            public Vector4 vertexInputPositionScale;
            public Vector4 vertexOutputPositionBias; // Float4s for alignment, w unused (is this needed?)
            public Vector4 vertexOutputPositionScale;

            public Vector4 morphTargetsPosRange;
            public Vector4 morphTargetsNormRange;
            public Vector4 morphTargetsTanRange;

            public int PositionsOffset => (int)attributesAndJointWeightsOffsetBytes.x;
            public int NormalsOffset => (int)attributesAndJointWeightsOffsetBytes.y;
            public int TangentsOffset => (int)attributesAndJointWeightsOffsetBytes.z;
            public int JointWeightsOffset => (int)attributesAndJointWeightsOffsetBytes.w;
            public int MorphTargetDeltasOffset => (int)morphTargetInfoAndJointIndicesOffsetBytes.x;
            public int JointIndicesOffset => (int)morphTargetInfoAndJointIndicesOffsetBytes.w;

            public int OutputIndicesOffset => (int)outputIndexOffset.x;
        }

        public ComputeBuffer StaticDataComputeBuffer { get; private set; }
        internal StaticMetaData SourceMetaData { get; private set; }

        public bool IsLoading => _buildSlice.IsValid;

        private OvrTime.SliceHandle _buildSlice;

        public OvrAvatarComputeSkinnedPrimitive(
            string name,
            int vertexCount,
            IntPtr neutralPositions,
            IntPtr neutralNormals,
            IntPtr neutralTangents,
            int morphTargetCount,
            IntPtr deltaPosPtr,
            IntPtr deltaNormPtr,
            IntPtr deltaTanPtr,
            BoneWeight[] boneWeights,
            // TODO: adjust scoping to give direct access to MeshInfo
            Action neutralPoseCallback,
            Action finishCallback)
        {
            var gpuSkinningConfig = GpuSkinningConfiguration.Instance;

            _buildSlice = OvrTime.Slice(
                BuildBuffers(
                    gpuSkinningConfig,
                    name,
                    vertexCount,
                    neutralPositions,
                    neutralNormals,
                    neutralTangents,
                    morphTargetCount,
                    deltaPosPtr,
                    deltaNormPtr,
                    deltaTanPtr,
                    boneWeights,
                    neutralPoseCallback,
                    finishCallback)
            );
        }

        private IEnumerator<OvrTime.SliceStep> BuildBuffers(
            GpuSkinningConfiguration gpuSkinningConfig,
            string name,
            int vertexCount,
            IntPtr neutralPositions,
            IntPtr neutralNormals,
            IntPtr neutralTangents,
            int morphTargetCount,
            IntPtr deltaPosPtr,
            IntPtr deltaNormPtr,
            IntPtr deltaTanPtr,
            BoneWeight[] boneWeights,
            Action neutralPoseCallback,
            Action finishCallback)
        {
            // TODO: Some of this work can be moved off the main thread (everything except creating Unity.Objects)
            yield return OvrTime.SliceStep.Stall;

            bool hasTangents = neutralTangents != IntPtr.Zero;

            // Native arrays for temporary data
            var jointIndices = new NativeArray<CAPI.ovrAvatar2Vector4us>();
            var jointWeights = new NativeArray<CAPI.ovrAvatar2Vector4f>();
            var jointWeightsSourceData = new NativeArray<byte>();
            var jointIndicesSourceData = new NativeArray<byte>();
            var morphBufferData = new NativeArray<byte>();
            var neutralNormalsSourceData = new NativeArray<byte>();
            var neutralPositionsSourceData = new NativeArray<byte>();
            var neutralTangentsSourceData = new NativeArray<byte>();
            var vertexReorderBuffer = new NativeArray<ushort>();

            try
            {
                Profiler.BeginSample("OvrAvatarComputeSkinnedPrimitive.GetJointIndicesAndWeights");
                GetJointIndicesAndWeights(
                    vertexCount,
                    boneWeights,
                    out jointIndices,
                    out jointWeights);
                Profiler.EndSample();

                yield return OvrTime.SliceStep.Stall;
                // Grab the morph target (and vertex reordering) info
                Profiler.BeginSample("OvrAvatarComputeSkinnedPrimitive.CreateMorphTargetSourceData");
                CreateMorphTargetSourceData(
                    name,
                    vertexCount,
                    morphTargetCount,
                    hasTangents,
                    deltaPosPtr,
                    deltaNormPtr,
                    deltaTanPtr,
                    jointWeights,
                    gpuSkinningConfig.SourceMorphFormat,
                    out CAPI.ovrGpuMorphTargetBufferDesc morphBufferDesc,
                    out morphBufferData,
                    out vertexReorderBuffer);

                Profiler.EndSample();

                // Grab the neutral pose info
                yield return OvrTime.SliceStep.Stall;
                Profiler.BeginSample("OvrAvatarComputeSkinnedPrimitive.CreateNeutralPositionsSourceData");
                CreateNeutralPositionsSourceData(
                    vertexCount,
                    neutralPositions,
                    vertexReorderBuffer,
                    GpuSkinningConfiguration.TexturePrecision.Float, // Hard coding float precision
                    out CAPI.ovrGpuSkinningBufferDesc neutralPositionsDesc,
                    out neutralPositionsSourceData);
                Profiler.EndSample();

                yield return OvrTime.SliceStep.Stall;
                Profiler.BeginSample("OvrAvatarComputeSkinnedPrimitive.CreateNeutralNormalsSourceData");
                CreateNeutralNormalsSourceData(
                    vertexCount,
                    neutralNormals,
                    vertexReorderBuffer,
                    GpuSkinningConfiguration.TexturePrecision.Snorm10, // Hard coding 10-10-10-2
                    out CAPI.ovrGpuSkinningBufferDesc neutralNormalsDesc,
                    out neutralNormalsSourceData);
                Profiler.EndSample();

                if (hasTangents)
                {
                    yield return OvrTime.SliceStep.Stall;
                    Profiler.BeginSample("OvrAvatarComputeSkinnedPrimitive.CreateNeutralTangentsSourceData");
                    CreateNeutralTangentsSourceData(
                        vertexCount,
                        neutralTangents,
                        vertexReorderBuffer,
                        GpuSkinningConfiguration.TexturePrecision.Snorm10, // Hard coding 10-10-10-2
                        out CAPI.ovrGpuSkinningBufferDesc neutralTangentsDesc,
                        out neutralTangentsSourceData);
                    Profiler.EndSample();
                }

                neutralPoseCallback?.Invoke();

                // Grab the joint weights and indices info
                yield return OvrTime.SliceStep.Stall;
                Profiler.BeginSample("OvrAvatarComputeSkinnedPrimitive.CreateJointWeightsSourceData");
                CreateJointWeightsSourceData(
                    vertexCount,
                    jointWeights,
                    vertexReorderBuffer,
                    out jointWeightsSourceData);
                Profiler.EndSample();

                yield return OvrTime.SliceStep.Stall;
                Profiler.BeginSample("OvrAvatarComputeSkinnedPrimitive.CreateJointIndicesSourceData");
                CreateJointIndicesSourceData(
                    vertexCount,
                    jointIndices,
                    vertexReorderBuffer,
                    CAPI.ovrGpuSkinningEncodingPrecision.ENCODING_PRECISION_UINT8, // hard coding UINT8 for now < 256 joints
                    out CAPI.ovrGpuSkinningBufferDesc jointIndicesBufferDesc,
                    out jointIndicesSourceData);
                Profiler.EndSample();

                // Now pull out relevant meta data and create compute buffer
                yield return OvrTime.SliceStep.Stall;
                Profiler.BeginSample("ovrAvatarComputeSkinnedPrimitive.CreateStaticDataComputeBuffer");
                StaticDataComputeBuffer = CreateStaticDataComputeBuffer(
                    gpuSkinningConfig,
                    morphBufferDesc,
                    morphBufferData,
                    neutralPositionsSourceData,
                    neutralNormalsSourceData,
                    neutralTangentsSourceData,
                    jointWeightsSourceData,
                    jointIndicesSourceData,
                    vertexReorderBuffer,
                    out Vector3 posOutputScale,
                    out Vector3 posOutputBias);

                yield return OvrTime.SliceStep.Stall;
                Profiler.BeginSample("OvrAvatarComputeSkinnedPrimitive.PopulateMetadata");
                SourceMetaData = CreateExternalMetadata(
                    morphBufferDesc,
                    neutralPositionsDesc,
                    jointIndicesBufferDesc,
                    gpuSkinningConfig.SkinnerOutputFormat,
                    posOutputScale,
                    posOutputBias);
                Profiler.EndSample();

                Debug.Assert(neutralPositionsSourceData.IsCreated && vertexReorderBuffer.IsCreated);
                Debug.Assert(neutralNormalsSourceData.IsCreated);
            } finally {
                // Clear the temporary native arrays if they were created
                morphBufferData.Reset();
                jointWeightsSourceData.Reset();
                jointIndicesSourceData.Reset();
                neutralTangentsSourceData.Reset();
                jointIndices.Reset();
                jointWeights.Reset();
                neutralPositionsSourceData.Reset();
                neutralNormalsSourceData.Reset();
                vertexReorderBuffer.Reset();
            }

            finishCallback?.Invoke();

            // Mark loading as finished/cleanup
            _buildSlice.Clear();
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
                if (_buildSlice.IsValid)
                {
                    _buildSlice.Cancel();
                }

                StaticDataComputeBuffer?.Dispose();
            }
            else
            {
                if (_buildSlice.IsValid)
                {
                    OvrAvatarLog.LogError("Build buffers slice still valid when finalized", LOG_SCOPE);

                    // Prevent OvrTime from stalling
                    _buildSlice.EmergencyShutdown();
                }

                if (StaticDataComputeBuffer != null)
                {
                    OvrAvatarLog.LogError($"OvrAvatarComputeSkinnedPrimitive was not disposed before being destroyed", LOG_SCOPE);
                }
            }

            StaticDataComputeBuffer = null;
            SourceMetaData = null;
        }

        ~OvrAvatarComputeSkinnedPrimitive()
        {
            Dispose(false);
        }

        private static int GetUintAlignedLength<T>(int numEntries) where T : struct
        {
            // Since this will be writing to a ByteAddressBuffer in the ComputeBuffer,
            // and a ByteAddressBuffer is basically a "bag of uints", each but if data written to the ComputeBuffer
            // must have a byte length that is a multiple of the size of a uint. Some arrays may need additional
            // padding when written to the ComputeBuffer
            // Pad the vertex count so that the total byte size of vertexReorderBuffer is a multiple of 4
            const int sizeOfUint = sizeof(uint);
            int sizeOfType = UnsafeUtility.SizeOf<T>();
            var byteSize = numEntries * sizeOfType;
            int numUintsNeeded = (byteSize + sizeOfUint - 1) / sizeOfUint;

            return numUintsNeeded * sizeOfUint / sizeOfType;
        }

        private static void CreateMorphTargetSourceData(
            string name,
            int vertexCount,
            int morphTargetCount,
            bool hasTangents,
            IntPtr deltaPosPtr,
            IntPtr deltaNormPtr,
            IntPtr deltaTanPtr,
            in NativeArray<CAPI.ovrAvatar2Vector4f> jointWeights,
            GpuSkinningConfiguration.TexturePrecision morphSrcPrecision,
            out CAPI.ovrGpuMorphTargetBufferDesc bufferDesc,
            out NativeArray<byte> morphStaticData,
            out NativeArray<UInt16> vertexReorderBuffer)
        {
            // Pad the vertex count so that the total byte size of vertexReorderBuffer is a multiple of 4
            var paddedLength = GetUintAlignedLength<UInt16>(vertexCount);
            vertexReorderBuffer = new NativeArray<UInt16>(paddedLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            IntPtr vertexReorderPtr = vertexReorderBuffer.GetIntPtr();
            IntPtr jointWeightsPtr = jointWeights.GetIntPtr();

            if (hasTangents)
            {
                bufferDesc = CAPI.OvrGpuSkinning_MorphTargetGetTextureBufferMetaDataWithTangents(
                    (uint)vertexCount,
                    (uint)morphTargetCount,
                    morphSrcPrecision.GetOvrPrecision(),
                    deltaPosPtr,
                    deltaNormPtr,
                    deltaTanPtr,
                    jointWeightsPtr,
                    vertexReorderPtr);
            }
            else
            {
                bufferDesc = CAPI.OvrGpuSkinning_MorphTargetGetTextureBufferMetaData(
                    (uint)vertexCount,
                    (uint)morphTargetCount,
                    morphSrcPrecision.GetOvrPrecision(),
                    deltaPosPtr,
                    deltaNormPtr,
                    jointWeightsPtr,
                    vertexReorderPtr);
            }

            if (bufferDesc.numMorphedVerts <= 0)
            {
                OvrAvatarLog.LogDebug($"Primitive ({name}) has morph targets, but no affected verts", LOG_SCOPE);
                morphStaticData = default;
                return;
            }

            // Create a native array and fill it with data
            paddedLength = GetUintAlignedLength<byte>((int)bufferDesc.bufferDataSize);
            morphStaticData = new NativeArray<byte>(paddedLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            IntPtr dataPtr = morphStaticData.GetIntPtr();

            if (hasTangents)
            {
                if (!CAPI.OvrGpuSkinning_MorphTargetEncodeBufferDataWithTangents(bufferDesc, vertexReorderPtr,
                    deltaPosPtr, deltaNormPtr, deltaTanPtr, dataPtr))
                {
                    OvrAvatarLog.LogError("failed to get morph data", LOG_SCOPE);
                }
            }
            else
            {
                if (!CAPI.OvrGpuSkinning_MorphTargetEncodeBufferData(bufferDesc, vertexReorderPtr, deltaPosPtr,
                    deltaNormPtr, dataPtr))
                {
                    OvrAvatarLog.LogError("failed to get morph data", LOG_SCOPE);
                }
            }
        } // end method

        private static void CreateNeutralPositionsSourceData(
            int vertexCount,
            IntPtr neutralPosPtr,
            in NativeArray<UInt16> vertexReorder,
            GpuSkinningConfiguration.TexturePrecision precision,
            out CAPI.ovrGpuSkinningBufferDesc bufferDesc,
            out NativeArray<byte> neutralPositionsSourceData)
        {
            bufferDesc = CAPI.OvrGpuSkinning_NeutralPositionsBufferDesc(
                (uint)vertexCount,
                precision.GetOvrPrecision());

            // Create a native array and fill it with data
            int paddedLength = GetUintAlignedLength<byte>((int)bufferDesc.dataSize);
            neutralPositionsSourceData = new NativeArray<byte>(paddedLength, Allocator.Persistent);

            IntPtr dataPtr = neutralPositionsSourceData.GetIntPtr();
            IntPtr vertexReorderPtr = vertexReorder.GetIntPtr();

            if (!CAPI.OvrGpuSkinning_EncodeNeutralPositionsBufferData(bufferDesc, neutralPosPtr, vertexReorderPtr, dataPtr))
            {
                OvrAvatarLog.LogError("failed to get neutral pose position data", LOG_SCOPE);
            }
        } // end method

        private static void CreateNeutralNormalsSourceData(
            int vertexCount,
            IntPtr neutralNormPtr,
            in NativeArray<UInt16> vertexReorder,
            GpuSkinningConfiguration.TexturePrecision precision,
            out CAPI.ovrGpuSkinningBufferDesc bufferDesc,
            out NativeArray<byte> neutralNormalsSourceData)
        {
            bufferDesc = CAPI.OvrGpuSkinning_NeutralNormalsBufferDesc(
                (uint)vertexCount,
                precision.GetOvrPrecision());

            // Create a native array and fill it with data
            int paddedLength = GetUintAlignedLength<byte>((int)bufferDesc.dataSize);
            neutralNormalsSourceData = new NativeArray<byte>(paddedLength, Allocator.Persistent);

            IntPtr dataPtr = neutralNormalsSourceData.GetIntPtr();
            IntPtr vertexReorderPtr = vertexReorder.GetIntPtr();

            if (!CAPI.OvrGpuSkinning_EncodeNeutralNormalsBufferData(bufferDesc, neutralNormPtr, vertexReorderPtr, dataPtr))
            {
                OvrAvatarLog.LogError("failed to get neutral pose normal data", LOG_SCOPE);
            }
        } // end method

        private static void CreateNeutralTangentsSourceData(
            int vertexCount,
            IntPtr neutralTanPtr,
            in NativeArray<UInt16> vertexReorder,
            GpuSkinningConfiguration.TexturePrecision precision,
            out CAPI.ovrGpuSkinningBufferDesc bufferDesc,
            out NativeArray<byte> neutralTangentsSourceData)
        {
            bufferDesc = CAPI.OvrGpuSkinning_NeutralTangentsBufferDesc(
                (uint)vertexCount,
                precision.GetOvrPrecision());

            // Create a native array and fill it with data
            int paddedLength = GetUintAlignedLength<byte>((int)bufferDesc.dataSize);
            neutralTangentsSourceData = new NativeArray<byte>(paddedLength, Allocator.Persistent);

            IntPtr dataPtr = neutralTangentsSourceData.GetIntPtr();
            IntPtr vertexReorderPtr = vertexReorder.GetIntPtr();

            if (!CAPI.OvrGpuSkinning_EncodeNeutralTangentsBufferData(bufferDesc, neutralTanPtr, vertexReorderPtr, dataPtr))
            {
                OvrAvatarLog.LogError("failed to get neutral pose tangent data", LOG_SCOPE);
            }
        } // end method

        private static void CreateJointWeightsSourceData(
            int vertexCount,
            in NativeArray<CAPI.ovrAvatar2Vector4f> jointWeights,
            in NativeArray<UInt16> vertexReorder,
            out NativeArray<byte> jointWeightsSourceData)
        {
            CAPI.ovrGpuSkinningBufferDesc desc = CAPI.OvrGpuSkinning_JointWeightsBufferDesc((uint)vertexCount);

            // Create a native array and fill it with data
            int paddedLength = GetUintAlignedLength<byte>((int)desc.dataSize);
            jointWeightsSourceData = new NativeArray<byte>(paddedLength, Allocator.Persistent);

            IntPtr dataPtr;
            IntPtr vertexReorderPtr;
            IntPtr jointWeightsPtr;

            unsafe
            {
                dataPtr = (IntPtr)jointWeightsSourceData.GetUnsafePtr();
                vertexReorderPtr = (IntPtr)vertexReorder.GetUnsafePtr();
                jointWeightsPtr = (IntPtr)jointWeights.GetUnsafePtr();
            }

            if (!CAPI.OvrGpuSkinning_EncodeJointWeightsBufferData(desc,  jointWeightsPtr, vertexReorderPtr, dataPtr))
            {
                OvrAvatarLog.LogError("failed to get joint wegihts data", LOG_SCOPE);
            }
        } // end method

        private static void CreateJointIndicesSourceData(
            int vertexCount,
            in NativeArray<CAPI.ovrAvatar2Vector4us> jointIndices,
            in NativeArray<UInt16> vertexReorder,
            CAPI.ovrGpuSkinningEncodingPrecision encodingPrecision,
            out CAPI.ovrGpuSkinningBufferDesc bufferDesc,
            out NativeArray<byte> jointIndicesSourceData)
        {
            bufferDesc = CAPI.OvrGpuSkinning_JointIndicesBufferDesc((uint)vertexCount, encodingPrecision);

            // Create a native array and fill it with data
            int paddedLength = GetUintAlignedLength<byte>((int)bufferDesc.dataSize);
            jointIndicesSourceData = new NativeArray<byte>(paddedLength, Allocator.Persistent);

            IntPtr dataPtr;
            IntPtr vertexReorderPtr;
            IntPtr jointIndicesPtr;
            unsafe
            {
                dataPtr = (IntPtr)jointIndicesSourceData.GetUnsafePtr();
                vertexReorderPtr = (IntPtr)vertexReorder.GetUnsafePtr();
                jointIndicesPtr = (IntPtr)jointIndices.GetUnsafePtr();
            }

            if (!CAPI.OvrGpuSkinning_EncodeJointIndicesBufferData(bufferDesc, jointIndicesPtr, vertexReorderPtr, dataPtr))
            {
                OvrAvatarLog.LogError("failed to get joint indices data", LOG_SCOPE);
            }
        } // end method


        private static StaticMetaData CreateExternalMetadata(
            in CAPI.ovrGpuMorphTargetBufferDesc morphBufferDesc,
            in CAPI.ovrGpuSkinningBufferDesc neutralPositionBufferDesc,
            in CAPI.ovrGpuSkinningBufferDesc jointIndicesBufferDesc,
            GpuSkinningConfiguration.TexturePrecision outputPosPrecision,
            in Vector3 posOutputScale,
            in Vector3 posOutputBias)
        {
            StaticMetaData externalMeta = new StaticMetaData
            {
                numMorphedVerts = (int)morphBufferDesc.numMorphedVerts,
                numVertsNoJointsOrMorphs = (int)morphBufferDesc.numMorphedVertsNoJoints,
                morphDeltasPrecision = morphBufferDesc.encodingPrecision,
                inputPositionPrecision = neutralPositionBufferDesc.precision,
                outputPositionPrecision = outputPosPrecision,
                jointIndicesPrecision = jointIndicesBufferDesc.precision,
                positionOutputBias = posOutputBias,
                positionOutputScale = posOutputScale,
            };

            return externalMeta;
        }

        private static void FillStaticDataComputeBufferViaBackingBuffer(
            ComputeBufferStaticMeshHeader header,
            in NativeArray<byte> rawMorphData,
            in NativeArray<byte> rawNeutralPosData,
            in NativeArray<byte> rawNeutralNormData,
            in NativeArray<byte> rawNeutralTanData,
            in NativeArray<byte> rawJointWeightsData,
            in NativeArray<byte> rawJointIndicesData,
            in NativeArray<UInt16> vertexReorderBuffer,
            ComputeBuffer bufferToFill,
            int bufferSizeBytes)
        {
            using(var backingBuffer = new NativeArray<byte>(bufferSizeBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                // Data layout is header -> neutral pose -> morphs deltas (if applicable) -> joint weights -> joint indices (if applicable)
                backingBuffer.ReinterpretStore(0, header);
                CopyNativeArrayToBackingBuffer(backingBuffer, rawNeutralPosData, header.PositionsOffset);
                CopyNativeArrayToBackingBuffer(backingBuffer, rawNeutralNormData, header.NormalsOffset);

                if (rawNeutralTanData.Length > 0)
                {
                    CopyNativeArrayToBackingBuffer(backingBuffer, rawNeutralTanData, header.TangentsOffset);
                }

                if (rawMorphData.Length > 0)
                {
                    CopyNativeArrayToBackingBuffer(backingBuffer, rawMorphData, header.MorphTargetDeltasOffset);
                }

                if (rawJointIndicesData.Length > 0)
                {
                    CopyNativeArrayToBackingBuffer(backingBuffer, rawJointWeightsData, header.JointWeightsOffset);
                    CopyNativeArrayToBackingBuffer(backingBuffer, rawJointIndicesData, header.JointIndicesOffset);
                }

                CopyNativeArrayToBackingBuffer(backingBuffer, vertexReorderBuffer, header.OutputIndicesOffset);

                // Now copy over to compute buffer as one whole copy
                SetComputeBufferDataFromNativeArray(bufferToFill, backingBuffer, 0);
            }
        }

         private static void FillStaticDataComputeBufferViaPartialUpdates(
            ComputeBufferStaticMeshHeader header,
            in NativeArray<byte> rawMorphData,
            in NativeArray<byte> rawNeutralPosData,
            in NativeArray<byte> rawNeutralNormData,
            in NativeArray<byte> rawNeutralTanData,
            in NativeArray<byte> rawJointWeightsData,
            in NativeArray<byte> rawJointIndicesData,
            in NativeArray<UInt16> vertexReorderBuffer,
            int sizeOfHeaderBytes,
            ComputeBuffer bufferToFill)
        {
            // Convert header to native array
            NativeArray<byte> headerAsBytes = new NativeArray<byte>(
                sizeOfHeaderBytes,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            headerAsBytes.ReinterpretStore(0, header);

            try
            {
                // Data layout is header -> neutral pose -> morphs deltas (if applicable) -> joint weights -> joint indices (if applicable)
                SetComputeBufferDataFromNativeArray(bufferToFill, headerAsBytes, 0);
                SetComputeBufferDataFromNativeArray(bufferToFill, rawNeutralPosData, header.PositionsOffset);
                SetComputeBufferDataFromNativeArray(bufferToFill, rawNeutralNormData, header.NormalsOffset);

                if (rawNeutralTanData.Length > 0)
                {
                    SetComputeBufferDataFromNativeArray(bufferToFill, rawNeutralTanData, header.TangentsOffset);
                }

                if (rawMorphData.Length > 0)
                {
                    SetComputeBufferDataFromNativeArray(bufferToFill, rawMorphData, header.MorphTargetDeltasOffset);
                }

                if (rawJointIndicesData.Length > 0)
                {
                    SetComputeBufferDataFromNativeArray(bufferToFill, rawJointWeightsData, header.JointWeightsOffset);
                    SetComputeBufferDataFromNativeArray(bufferToFill, rawJointIndicesData, header.JointIndicesOffset);
                }

                SetComputeBufferDataFromNativeArray(bufferToFill, vertexReorderBuffer, header.OutputIndicesOffset);            }
            finally
            {
                headerAsBytes.Dispose();
            }
        }

        private static ComputeBuffer CreateStaticDataComputeBuffer(
            GpuSkinningConfiguration gpuSkinningConfig,
            in CAPI.ovrGpuMorphTargetBufferDesc morphBufferDesc,
            in NativeArray<byte> rawMorphData,
            in NativeArray<byte> rawNeutralPosData,
            in NativeArray<byte> rawNeutralNormData,
            in NativeArray<byte> rawNeutralTanData,
            in NativeArray<byte> rawJointWeightsData,
            in NativeArray<byte> rawJointIndicesData,
            in NativeArray<UInt16> vertexReorderBuffer,
            out Vector3 positionOutputScale,
            out Vector3 positionOutputBias)
        {
            const int BAG_OF_UINTS_BUFFER_STRIDE_BYTES = sizeof(UInt32);

            // Data layout is header -> neutral pose -> morphs deltas (if applicable) -> joints (if applicable)

            // Create a "header" of the metadata to describe the rest of the data in the buffer
            ComputeBufferStaticMeshHeader header = new ComputeBufferStaticMeshHeader();

            // Calculate offsets into the buffer for the neutral attribute and the joints and store in the header
            int sizeOfHeaderBytes = UnsafeUtility.SizeOf<ComputeBufferStaticMeshHeader>();
            int neutralPosePosOffset = sizeOfHeaderBytes;
            int neutralPoseNormOffset = neutralPosePosOffset + rawNeutralPosData.Length;
            int neutralPoseTanOffset = neutralPoseNormOffset + rawNeutralNormData.Length;
            int morphDeltasOffset = neutralPoseTanOffset + rawNeutralTanData.Length;
            int jointWeightsOffset = morphDeltasOffset + rawMorphData.Length;
            int jointIndicesOffset = jointWeightsOffset + rawJointWeightsData.Length;
            int outputIndicesOffset = jointIndicesOffset + rawJointIndicesData.Length;

            header.attributesAndJointWeightsOffsetBytes = new Vector4UInt
            {
                x = (uint)neutralPosePosOffset,
                y = (uint)neutralPoseNormOffset,
                z = (uint)neutralPoseTanOffset,
                w = (uint)jointWeightsOffset,
            };

            // Calculate offset for the morph target deltas and store other info in the header
            header.morphTargetInfoAndJointIndicesOffsetBytes = new Vector4UInt
            {
                x = (uint)morphDeltasOffset,
                y = morphBufferDesc.numMorphTargets,
                z = morphBufferDesc.numMorphedVerts,
                w = (uint)jointIndicesOffset,
            };

            header.outputIndexOffset = new Vector4UInt
            {
                x = (uint)outputIndicesOffset,
            };

            // Store the misc. offsets and scales for normalized data into the header
            header.morphTargetsPosRange = new Vector4(morphBufferDesc.positionScale.x, morphBufferDesc.positionScale.y, morphBufferDesc.positionScale.z, 0.0f);
            header.morphTargetsNormRange = new Vector4(morphBufferDesc.normalScale.x, morphBufferDesc.normalScale.y, morphBufferDesc.normalScale.z, 0.0f);
            header.morphTargetsTanRange = new Vector4(
                morphBufferDesc.tangentScale.x,
                morphBufferDesc.tangentScale.y,
                morphBufferDesc.tangentScale.z, 0.0f);

            // For now, since position input  is only supporting float values, just set offset and scale to be 0 and 1
            header.vertexInputPositionBias = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            header.vertexInputPositionScale = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

            // Set output position scale/bias based on gpu skinning config
            switch (gpuSkinningConfig.SkinnerOutputFormat)
            {
                case GpuSkinningConfiguration.TexturePrecision.Unorm16:
                    // TODO*: Read in a normalization scale and bias from GpuSkinningConfiguration when
                    // available. For now, just hard code
                    float scale = 8.0f;
                    float bias = -0.75f;
                    positionOutputBias = new Vector3(bias, bias, bias);
                    positionOutputScale = new Vector3(scale, scale, scale);
                    break;
                default:
                    positionOutputBias = Vector3.zero;
                    positionOutputScale = Vector3.one;
                    break;
            }
            header.vertexOutputPositionBias = positionOutputBias;
            header.vertexOutputPositionScale = positionOutputScale;

            int totalSizeBytes =
                sizeOfHeaderBytes +
                rawMorphData.Length +
                rawNeutralPosData.Length +
                rawNeutralNormData.Length +
                rawNeutralTanData.Length +
                rawJointIndicesData.Length +
                rawJointWeightsData.Length +
                (vertexReorderBuffer.Length * sizeof(UInt16));

            // Unity requires compute buffers to have a minimum stride of 4 even for "raw" buffers because
            // ByteAddressBuffers are really bags of uints instead of bags of bytes.
            // All of the data should be aligned to 4 byte boundaries anyway, so, just need to convert the sizes/offsets
            // when setting compute buffer data to be divided by 4...sigh
            var result = new ComputeBuffer(
                totalSizeBytes / BAG_OF_UINTS_BUFFER_STRIDE_BYTES,
                 BAG_OF_UINTS_BUFFER_STRIDE_BYTES,
                 ComputeBufferType.Raw); // Raw for ByteAddressBuffer

            try
            {
#if COMPUTE_BUFFER_PARTIAL_UPDATE_ALLOWED
                FillStaticDataComputeBufferViaPartialUpdates(
                    header,
                    rawMorphData,
                    rawNeutralPosData,
                    rawNeutralNormData,
                    rawNeutralTanData,
                    rawJointWeightsData,
                    rawJointIndicesData,
                    vertexReorderBuffer,
                    sizeOfHeaderBytes,
                    result);
#else
                FillStaticDataComputeBufferViaBackingBuffer(
                    header,
                    rawMorphData,
                    rawNeutralPosData,
                    rawNeutralNormData,
                    rawNeutralTanData,
                    rawJointWeightsData,
                    rawJointIndicesData,
                    vertexReorderBuffer,
                    result,
                    totalSizeBytes);
#endif
            }
            catch
            {
                // Don't leak memory here if there is an exception filling the compute buffer
                result.Dispose();
                throw;
            }

            return result;
        }

        // ASSUMPTIONS: Starting an byte array index 0 and copying whole array
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

        void GetJointIndicesAndWeights(
            int vertexCount,
            BoneWeight[] boneWeights,
            out NativeArray<CAPI.ovrAvatar2Vector4us> jointIndices,
            out NativeArray<CAPI.ovrAvatar2Vector4f> jointWeights)
        {
            // TODO: get these two arrays directly! See RetrieveBoneWeights()
            jointIndices = new NativeArray<CAPI.ovrAvatar2Vector4us>(
                vertexCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            jointWeights = new NativeArray<CAPI.ovrAvatar2Vector4f>(
                vertexCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < boneWeights.Length; i++)
            {
                BoneWeight bw = boneWeights[i];
                jointIndices[i] = new CAPI.ovrAvatar2Vector4us
                {
                    x = (ushort)bw.boneIndex0,
                    y = (ushort)bw.boneIndex1,
                    z = (ushort)bw.boneIndex2,
                    w = (ushort)bw.boneIndex3,
                };

                jointWeights[i] = new CAPI.ovrAvatar2Vector4f
                {
                    x = bw.weight0,
                    y = bw.weight1,
                    z = bw.weight2,
                    w = bw.weight3,
                };
            }
        }

    } // end class OvrAvatarComputeSkinnedPrimitive
} // end namespace
