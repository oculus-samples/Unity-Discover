using Oculus.Skinning;
using Oculus.Skinning.GpuSkinning;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;

namespace Oculus.Avatar2
{
    public sealed class OvrAvatarGpuSkinnedPrimitive : IDisposable
    {
        private const string LOG_SCOPE = "OvrAvatarGPUSkinnedPrimitive";
        private const NativeArrayOptions NATIVE_ARRAY_INIT = NativeArrayOptions.UninitializedMemory;

        public bool IsLoading => _buildTextureSlice.IsValid;

        public class SourceTextureMetaData
        {
            public CAPI.ovrTextureLayoutResult LayoutInMorphTargetsTex;
            public uint NumMorphTargetAffectedVerts;
            public int[] MeshVertexToAffectedIndex;
            public CAPI.ovrTextureLayoutResult LayoutInNeutralPoseTex;
            public CAPI.ovrTextureLayoutResult LayoutInJointsTex;
            public Vector3 PositionRange;
            public Vector3 NormalRange;
            public Vector3 TangentRange;
        }

        public OvrExpandableTextureArray NeutralPoseTex { get; private set; }
        public OvrExpandableTextureArray MorphTargetSourceTex { get; private set; }
        public OvrExpandableTextureArray JointsTex { get; private set; }
        public SourceTextureMetaData MetaData { get; private set; }

        private OvrTime.SliceHandle _buildTextureSlice;

        public OvrAvatarGpuSkinnedPrimitive(string name,
            uint vertexCount, IntPtr neutralPositions, IntPtr neutralNormals, IntPtr neutralTangents,
            uint morphTargetCount, IntPtr deltaPosPtr, IntPtr deltaNormPtr, IntPtr deltaTanPtr,
            uint jointsCount, BoneWeight[] boneWeights,
            // TODO: adjust scoping to give direct access to MeshInfo
            Action neutralPoseCallback, Action finishCallback)
        {
            var gpuSkinningConfig = GpuSkinningConfiguration.Instance;

            _buildTextureSlice = OvrTime.Slice(
                BuildTextures(gpuSkinningConfig, name, vertexCount, neutralPositions, neutralNormals, neutralTangents,
                morphTargetCount, deltaPosPtr, deltaNormPtr, deltaTanPtr,
                jointsCount, boneWeights,
                neutralPoseCallback, finishCallback)
            );
        }

        private IEnumerator<OvrTime.SliceStep> BuildTextures(GpuSkinningConfiguration gpuSkinningConfig, string name,
            uint vertexCount, IntPtr neutralPositions, IntPtr neutralNormals, IntPtr neutralTangents,
            uint morphTargetCount, IntPtr deltaPosPtr, IntPtr deltaNormPtr, IntPtr deltaTanPtr,
            uint jointsCount, BoneWeight[] boneWeights,
            // TODO: adjust scoping to give direct access to MeshInfo
            Action neutralPoseCallback, Action finishCallback)
        {
            // TODO: Some of this work can be moved off the main thread (everything except creating Unity.Objects)
            var result = new SourceTextureMetaData();

            yield return OvrTime.SliceStep.Stall;
            Profiler.BeginSample("OvrAvatarGPUSkinnedPrimitive.CreateNeutralPoseTex");
            NeutralPoseTex = CreateNeutralPoseTex(name, vertexCount, neutralPositions, neutralNormals, neutralTangents,
                gpuSkinningConfig.NeutralPoseFormat, ref result);
            Profiler.EndSample();
            neutralPoseCallback?.Invoke();

            if (morphTargetCount > 0)
            {
                yield return OvrTime.SliceStep.Stall;
                Profiler.BeginSample("OvrAvatarGPUSkinnedPrimitive.CreateMorphTargetSourceTex");
                MorphTargetSourceTex = CreateMorphTargetSourceTex(name, vertexCount, morphTargetCount,
                    deltaPosPtr, deltaNormPtr, deltaTanPtr,
                    gpuSkinningConfig.SourceMorphFormat, ref result);
                Profiler.EndSample();
            }

            if (jointsCount > 0)
            {
                yield return OvrTime.SliceStep.Stall;
                Profiler.BeginSample("OvrAvatarGPUSkinnedPrimitive.CreateJointsTex");
                JointsTex = CreateJointsTex(name, vertexCount, boneWeights, gpuSkinningConfig.JointsFormat, ref result);
                Profiler.EndSample();
            }

            MetaData = result;
            finishCallback?.Invoke();

            _buildTextureSlice.Clear();
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
                if (_buildTextureSlice.IsValid)
                {
                    _buildTextureSlice.Cancel();
                }

                JointsTex?.Destroy();
                MorphTargetSourceTex?.Destroy();
                NeutralPoseTex?.Destroy();
            }
            else
            {
                if (_buildTextureSlice.IsValid)
                {
                    OvrAvatarLog.LogError("Build texture slice still valid when finalized", LOG_SCOPE);

                    // Prevent OvrTime from stalling
                    _buildTextureSlice.EmergencyShutdown();
                }
                if (NeutralPoseTex != null || MorphTargetSourceTex != null || JointsTex != null)
                {
                    OvrAvatarLog.LogError($"OvrAvatarGPUSkinnedPrimitive was not disposed before being destroyed", LOG_SCOPE);
                }
            }
            JointsTex = null;
            MorphTargetSourceTex = null;
            NeutralPoseTex = null;
        }

        ~OvrAvatarGpuSkinnedPrimitive()
        {
            Dispose(false);
        }

        private static OvrExpandableTextureArray CreateNeutralPoseTex(string name, uint vertexCount, IntPtr positions,
            IntPtr normals, IntPtr tangents,
            GraphicsFormat neutralTexFormat, ref SourceTextureMetaData metaData)
        {
            var hasTangents = tangents != IntPtr.Zero;

            CAPI.ovrGpuSkinningTextureDesc texDesc = CAPI.OvrGpuSkinning_NeutralPoseTextureDesc(
                OvrGpuSkinningUtils.MAX_TEXTURE_DIMENSION,
                vertexCount,
                hasTangents);

            // Create expandable texture array and fill with data
            var output = new OvrExpandableTextureArray(
                "neutral(" + name + ")",
                texDesc.width,
                texDesc.height,
                neutralTexFormat);

            OvrSkinningTypes.Handle handle = output.AddEmptyBlock(texDesc.width, texDesc.height);
            CAPI.ovrTextureLayoutResult layout = output.GetLayout(handle);

            {
                Texture2D tempTex = new Texture2D(
                    layout.w,
                    layout.h,
                    output.Format,
                    output.HasMips,
                    output.IsLinear);

                var texData = tempTex.GetRawTextureData<byte>();

                // This will validate the sizes match
                Debug.Assert(texData.Length == texDesc.dataSize);

                IntPtr dataPtr = texData.GetIntPtr();

                CAPI.OvrGpuSkinning_NeutralPoseEncodeTextureData(in texDesc, vertexCount,
                        positions, normals, tangents, dataPtr,
                        texData.GetBufferSize());

                tempTex.Apply(false, true);

                output.CopyFromTexture(layout, tempTex);

                Texture2D.Destroy(tempTex);
            }

            metaData.LayoutInNeutralPoseTex = layout;

            return output;
        }

        private static OvrExpandableTextureArray CreateMorphTargetSourceTex(string name,
            uint vertexCount, uint morphTargetCount, IntPtr deltaPosPtr, IntPtr deltaNormPtr, IntPtr deltaTanPtr,
            GpuSkinningConfiguration.TexturePrecision morphSrcPrecision, ref SourceTextureMetaData metaData)
        {
            var hasTangents = deltaTanPtr != IntPtr.Zero;

            CAPI.ovrGpuMorphTargetTextureDesc morphTexDesc;
            OvrExpandableTextureArray output;

            using (var mVtoAVBuffer = new NativeArray<Int32>((int)vertexCount, Allocator.Temp, NATIVE_ARRAY_INIT))
            {
                IntPtr mVtoAVPtr;
                unsafe
                {
                    mVtoAVPtr = (IntPtr)mVtoAVBuffer.GetUnsafePtr();
                }

                if (hasTangents)
                {
                    morphTexDesc = CAPI.OvrGpuSkinning_MorphTargetEncodeMeshVertToAffectedVertWithTangents(
                        OvrGpuSkinningUtils.MAX_TEXTURE_DIMENSION,
                        vertexCount,
                        morphTargetCount,
                        morphSrcPrecision.GetOvrPrecision(),
                        deltaPosPtr,
                        deltaNormPtr,
                        deltaTanPtr,
                        mVtoAVPtr);
                }
                else
                {
                    morphTexDesc = CAPI.OvrGpuSkinning_MorphTargetEncodeMeshVertToAffectedVert(
                        OvrGpuSkinningUtils.MAX_TEXTURE_DIMENSION,
                        vertexCount,
                        morphTargetCount,
                        morphSrcPrecision.GetOvrPrecision(),
                        deltaPosPtr,
                        deltaNormPtr,
                        mVtoAVPtr);
                }

                metaData.NumMorphTargetAffectedVerts = morphTexDesc.numAffectedVerts;

                if (metaData.NumMorphTargetAffectedVerts <= 0)
                {
                    // TODO: Could we catch this much, much earlier?
                    //HasMorphTargets = false;
                    metaData.MeshVertexToAffectedIndex = Array.Empty<int>();
                    metaData.LayoutInMorphTargetsTex = CAPI.ovrTextureLayoutResult.INVALID_LAYOUT;

                    OvrAvatarLog.LogDebug($"Primitive ({name}) has morph target, but no affected verts", LOG_SCOPE);
                    return null;
                }

                var morphTargetTexels = morphTexDesc.texWidth * morphTexDesc.texHeight;
                Debug.Assert(morphTargetTexels > 0);

                // Create expandable texture array and fill with data
                output = new OvrExpandableTextureArray(
                    "morphSrc(" + name + ")",
                     morphTexDesc.texWidth,
                     morphTexDesc.texHeight,
                    morphSrcPrecision.GetGraphicsFormat());

                OvrSkinningTypes.Handle handle = output.AddEmptyBlock(
                     morphTexDesc.texWidth,
                     morphTexDesc.texHeight);
                CAPI.ovrTextureLayoutResult layout = output.GetLayout(handle);

                Texture2D tempTex = new Texture2D(
                    layout.w,
                    layout.h,
                    output.Format,
                    output.HasMips,
                    output.IsLinear);

                var texData = tempTex.GetRawTextureData<byte>();

                // This will validate the sizes match
                Debug.Assert(texData.Length == morphTexDesc.textureDataSize);

                IntPtr dataPtr;
                unsafe
                {
                    dataPtr = (IntPtr)texData.GetUnsafePtr();
                }

                if (hasTangents)
                {
                    if (!CAPI.OvrGpuSkinning_MorphTargetEncodeTextureDataWithTangents(morphTexDesc, mVtoAVPtr, morphSrcPrecision.GetOvrPrecision(), deltaPosPtr, deltaNormPtr, deltaTanPtr, dataPtr))
                    {
                        OvrAvatarLog.LogError("failed to get morph data", LOG_SCOPE);
                    }
                }
                else
                {
                    if (!CAPI.OvrGpuSkinning_MorphTargetEncodeTextureData(morphTexDesc, mVtoAVPtr, morphSrcPrecision.GetOvrPrecision(), deltaPosPtr, deltaNormPtr, dataPtr))
                    {
                        OvrAvatarLog.LogError("failed to get morph data", LOG_SCOPE);
                    }
                }

                tempTex.Apply(false, true);

                output.CopyFromTexture(layout, tempTex);

                Texture2D.Destroy(tempTex);

                metaData.LayoutInMorphTargetsTex = layout;

                metaData.MeshVertexToAffectedIndex = mVtoAVBuffer.ToArray();

                metaData.PositionRange = morphTexDesc.positionRange;
                metaData.NormalRange = morphTexDesc.normalRange;
                metaData.TangentRange = morphTexDesc.tangentRange;
            }

            return output;
        }

        private static OvrExpandableTextureArray CreateJointsTex(string name, uint vertexCount, BoneWeight[] boneWeights,
            GraphicsFormat jointsTexFormat, ref SourceTextureMetaData metaData)
        {
            // TODO: get these two arrays directly! See RetrieveBoneWeights()
            var jointIndices = new CAPI.ovrAvatar2Vector4us[vertexCount];
            var jointWeights = new CAPI.ovrAvatar2Vector4f[vertexCount];

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

            var texDesc = CAPI.OvrGpuSkinning_JointTextureDesc(OvrGpuSkinningUtils.MAX_TEXTURE_DIMENSION, vertexCount);

            var output = new OvrExpandableTextureArray(
                "joints(" + name + ")",
                texDesc.width,
                texDesc.height,
                jointsTexFormat);

            OvrSkinningTypes.Handle handle = output.AddEmptyBlock(texDesc.width, texDesc.height);
            CAPI.ovrTextureLayoutResult layout = output.GetLayout(handle);

            OvrAvatarLog.AssertConstMessage(layout.IsValid, "invalid texture layout detected", LOG_SCOPE);
            {
                var tempTex = new Texture2D(
                    layout.w,
                    layout.h,
                    output.Format,
                    output.HasMips,
                    output.IsLinear);

                var texData = tempTex.GetRawTextureData<byte>();

                Debug.Assert(texData.Length == texDesc.dataSize);

                IntPtr dataPtr;
                unsafe
                {
                    dataPtr = (IntPtr)texData.GetUnsafePtr();
                }

                var dataIndicesPtr = GCHandle.Alloc(jointIndices, GCHandleType.Pinned);
                var dataWeightsPtr = GCHandle.Alloc(jointWeights, GCHandleType.Pinned);

                bool didEncode = CAPI.OvrGpuSkinning_JointEncodeTextureData(
                        in texDesc,
                        vertexCount,
                        dataIndicesPtr.AddrOfPinnedObject(),
                        dataWeightsPtr.AddrOfPinnedObject(),
                        dataPtr,
                        texData.GetBufferSize());

                dataIndicesPtr.Free();
                dataWeightsPtr.Free();

                OvrAvatarLog.AssertConstMessage(didEncode, "get skinning data failure", LOG_SCOPE);

                tempTex.Apply(false, true);

                output.CopyFromTexture(layout, tempTex);

                Texture2D.Destroy(tempTex);
            }

            metaData.LayoutInJointsTex = layout;

            return output;
        }
    }
}
