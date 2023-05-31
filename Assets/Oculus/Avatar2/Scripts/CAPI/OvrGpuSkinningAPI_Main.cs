//#define OVR_GPUSKINNING_USE_NATIVE_SKINNER

/* CAPI wrapper for gpuskinning.[dll/so]
 * version: 0.0.1
 * */

using Oculus.Skinning;

using System;
using System.Runtime.InteropServices;

namespace Oculus.Avatar2
{
    public partial class CAPI
    {
        // enum only used for type-safety
        public enum ovrGpuSkinningHandle : Int32 { AtlasPackerId_Invalid = -1 };

        public enum ovrGpuSkinningEncodingPrecision : Int32 { ENCODING_PRECISION_FLOAT, ENCODING_PRECISION_HALF, ENCODING_PRECISION_10_10_10_2, ENCODING_PRECISION_UINT16, ENCODING_PRECISION_UINT8 };

        // Result codes for GpuSkinning CAPI methods
        [System.Flags]
        public enum ovrGpuSkinningResult : Int32
        {
            Success = 0,
            Failure = 1 << 0,

            InvalidId = 1 << 1,
            InvalidHandle = 1 << 2,
            InvalidParameter = 1 << 3,

            NullParameter = 1 << 4,
            InsufficientBuffer = 1 << 5,

            SystemUninitialized = 1 << 8,
            SubSystemUninitialized = 1 << 9,

            Unknown = 1 << 16,
        };

#if UNITY_EDITOR || !UNITY_IOS
#if UNITY_EDITOR_OSX
        private const string GpuSkinningLibFile = OvrAvatarPlugin.FullPluginFolderPath + "libovrgpuskinning.framework/libovrgpuskinning";
#else
        private const string GpuSkinningLibFile = OvrAvatarManager.IsAndroidStandalone ? "ovrgpuskinning" : "libovrgpuskinning";
#endif  // UNITY_EDITOR_OSX
#else   // !UNITY_EDITOR && UNITY_IOS
        private const string GpuSkinningLibFile = "__Internal";
#endif  // !UNITY_EDITOR && UNITY_IOS

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrGpuSkinningTextureDesc
        {
            public UInt32 width, height, dataSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrGpuSkinningBufferDesc
        {
            public UInt32 dataSize, numVerts;
            public ovrGpuSkinningEncodingPrecision precision;
        }

        //-----------------------------------------------------------------
        //
        // GpuSkinningDLL Management
        //
        public static bool OvrGpuSkinning_Initialize()
        {
            return ovrGpuSkinning_Initialize()
                .EnsureSuccess("ovrGpuSkinning_Initialize");
        }
        public static bool OvrGpuSkinning_Shutdown()
        {
            return ovrGpuSkinning_Shutdown()
                .EnsureSuccess("ovrGpuSkinning_Shutdown");
        }

        public static AtlasPackerId OvrGpuSkinning_AtlasPackerCreate(
            UInt32 atlasWidth,
            UInt32 atlasHeight,
            AtlasPackerPackingAlgortihm packingAlgorithm
        )
        {
            if (ovrGpuSkinning_AtlasPackerCreate(
                atlasWidth, atlasHeight, packingAlgorithm, out var newId)
                .EnsureSuccess("ovrGpuSkinning_AtlasPackerCreate"))
            {
                return newId;
            }
            return AtlasPackerId.Invalid;
        }

        public static ovrGpuSkinningHandle OvrGpuSkinning_AtlasPackerAddBlock(
            AtlasPackerId id,
            UInt32 width,
            UInt32 height)
        {
            if (ovrGpuSkinning_AtlasPackerAddBlock(id, width, height, out var newHandle)
                .EnsureSuccess("ovrGpuSkinning_AtlasPackerAddBlock"))
            {
                return newHandle;
            }
            return ovrGpuSkinningHandle.AtlasPackerId_Invalid;
        }

        //-----------------------------------------------------------------
        //
        // GpuMorphTargetTextureInfo
        //

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct ovrGpuMorphTargetTextureDesc
        {
            public readonly UInt32 textureDataSize;
            public readonly UInt32 numVerts;
            public readonly UInt32 numAffectedVerts;
            public readonly UInt32 firstAffectedVert;
            public readonly UInt32 lastAffectedVert;

            public readonly UInt32 texWidth;
            public readonly UInt32 texHeight;
            public readonly UInt32 numRowsPerMorphTarget;
            public readonly UInt32 numMorphTargets;

            public readonly ovrAvatar2Vector3f positionRange;
            public readonly ovrAvatar2Vector3f normalRange;
            public readonly ovrAvatar2Vector3f tangentRange;
        }

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct ovrGpuMorphTargetBufferDesc
        {
            public readonly UInt32 bufferDataSize;
            public readonly UInt32 numMorphedVerts;

            public readonly ovrAvatar2Vector3f positionScale;
            public readonly ovrAvatar2Vector3f normalScale;
            public readonly ovrAvatar2Vector3f tangentScale;

            public readonly UInt32 numMorphedVertsFourJoints;
            public readonly UInt32 numMorphedVertsThreeJoints;
            public readonly UInt32 numMorphedVertsTwoJoints;
            public readonly UInt32 numMorphedVertsOneJoint;
            public readonly UInt32 numMorphedVertsNoJoints;

            public readonly UInt32 numVertsFourJointsOnly;
            public readonly UInt32 numVertsThreeJointsOnly;
            public readonly UInt32 numVertsTwoJointsOnly;
            public readonly UInt32 numVertsOneJointOnly;
            public readonly UInt32 numVertsNoJointsOrMorphs;

            public readonly UInt32 numMorphTargets;
            public readonly ovrGpuSkinningEncodingPrecision encodingPrecision;
        }

        public static ovrGpuMorphTargetTextureDesc OvrGpuSkinning_MorphTargetEncodeMeshVertToAffectedVert(
            UInt32 maxTexDimension,
            UInt32 numMeshVerts,
            UInt32 numMorphTargets,
            ovrGpuSkinningEncodingPrecision texType,
            IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            IntPtr deltaNormalsArray,    // ovrAvatar2Vector3f**
            IntPtr meshVertToAffectedVert
        )
        {
            unsafe
            {
                if (ovrGpuSkinning_MorphTargetEncodeMeshVertToAffectedVert(
                    maxTexDimension, numMeshVerts, numMorphTargets, texType
                    , deltaPositionsArray, deltaNormalsArray, (Int32*)meshVertToAffectedVert
                    , out var newTextureDesc)
                    .EnsureSuccess("ovrGpuSkinning_GpuMorphTargetTextureInfoCreate"))
                {
                    return newTextureDesc;
                }
            }

            return new ovrGpuMorphTargetTextureDesc();
        }

        public static ovrGpuMorphTargetTextureDesc OvrGpuSkinning_MorphTargetEncodeMeshVertToAffectedVertWithTangents(
            UInt32 maxTexDimension,
            UInt32 numMeshVerts,
            UInt32 numMorphTargets,
            ovrGpuSkinningEncodingPrecision texType,
            IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            IntPtr deltaNormalsArray,    // ovrAvatar2Vector3f**
            IntPtr deltaTangentsArray,    // ovrAvatar2Vector3f**
            IntPtr meshVertToAffectedVert
        )
        {
            unsafe
            {
                if (ovrGpuSkinning_MorphTargetEncodeMeshVertToAffectedVertWithTangents(
                    maxTexDimension, numMeshVerts, numMorphTargets, texType
                    , deltaPositionsArray, deltaNormalsArray, deltaTangentsArray, (Int32*)meshVertToAffectedVert
                    , out var newTextureDesc)
                    .EnsureSuccess("ovrGpuSkinning_GpuMorphTargetTextureInfoCreateWithTangents"))
                {
                    return newTextureDesc;
                }
            }

            return new ovrGpuMorphTargetTextureDesc();
        }

        public static bool OvrGpuSkinning_MorphTargetEncodeTextureData(
            in ovrGpuMorphTargetTextureDesc morphTargetDesc,
            IntPtr meshVertToAffectedVert,
            ovrGpuSkinningEncodingPrecision texType,
            IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            IntPtr deltaNormalsArray,    // ovrAvatar2Vector3f**
            IntPtr resultBuffer
        )
        {
            unsafe
            {
                return ovrGpuSkinning_MorphTargetEncodeTextureData(
                    morphTargetDesc, (Int32*)meshVertToAffectedVert, texType
                    , deltaPositionsArray, deltaNormalsArray, (byte*)resultBuffer)
                    .EnsureSuccess("OvrGpuSkinning_GpuMorphTargetTextureInfoCreateTextureData");
            }
        }

        public static bool OvrGpuSkinning_MorphTargetEncodeTextureDataWithTangents(
            in ovrGpuMorphTargetTextureDesc morphTargetDesc,
            IntPtr meshVertToAffectedVert,
            ovrGpuSkinningEncodingPrecision texType,
            IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            IntPtr deltaNormalsArray,    // ovrAvatar2Vector3f**
            IntPtr deltaTangentsArray,    // ovrAvatar2Vector3f**
            IntPtr resultBuffer
        )
        {
            unsafe
            {
                return ovrGpuSkinning_MorphTargetEncodeTextureDataWithTangents(
                    morphTargetDesc, (Int32*)meshVertToAffectedVert, texType
                    , deltaPositionsArray, deltaNormalsArray, deltaTangentsArray, (byte*)resultBuffer)
                    .EnsureSuccess("OvrGpuSkinning_GpuMorphTargetTextureInfoCreateTextureData");
            }
        }

        public static ovrGpuMorphTargetBufferDesc OvrGpuSkinning_MorphTargetGetTextureBufferMetaData(
            UInt32 numMeshVerts,
            UInt32 numMorphTargets,
            ovrGpuSkinningEncodingPrecision encodingPrecision,
            IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            IntPtr deltaNormalsArray,    // ovrAvatar2Vector3f**
            IntPtr jointWeights, // float*
            IntPtr vertexIndexReordering // UInt16*
        )
        {
            unsafe
            {
                if (ovrGpuSkinning_MorphTargetGetBufferMetaData(
                        numMeshVerts,
                        numMorphTargets,
                        encodingPrecision,
                        deltaPositionsArray,
                        deltaNormalsArray,
                        jointWeights,
                        (UInt16*)vertexIndexReordering,
                        out var newBufferDesc)
                    .EnsureSuccess("ovrGpuSkinning_MorphTargetGetBufferMetaData"))
                {
                    return newBufferDesc;
                }
            }

            return new ovrGpuMorphTargetBufferDesc();
        }

        public static ovrGpuMorphTargetBufferDesc OvrGpuSkinning_MorphTargetGetTextureBufferMetaDataWithTangents(
            UInt32 numMeshVerts,
            UInt32 numMorphTargets,
            ovrGpuSkinningEncodingPrecision encodingPrecision,
            IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            IntPtr deltaNormalsArray,   // ovrAvatar2Vector3f**
            IntPtr deltaTangentsArray,  // ovrAvatar2Vector3f**
            IntPtr jointWeights, // float*
            IntPtr vertexIndexReordering // UInt16*
        )
        {
            unsafe
            {
                if (ovrGpuSkinning_MorphTargetGetBufferMetaDataWithTangents(
                        numMeshVerts,
                        numMorphTargets,
                        encodingPrecision,
                        deltaPositionsArray,
                        deltaNormalsArray,
                        deltaTangentsArray,
                        jointWeights,
                        (UInt16*)vertexIndexReordering,
                        out var newBufferDesc)
                    .EnsureSuccess("ovrGpuSkinning_MorphTargetGetBufferMetaDataWithTangents"))
                {
                    return newBufferDesc;
                }
            }

            return new ovrGpuMorphTargetBufferDesc();
        }

        public static bool OvrGpuSkinning_MorphTargetEncodeBufferData(
            in ovrGpuMorphTargetBufferDesc morphTargetDesc,
            /* const */ IntPtr vertexIndexReordering, // UInt16*
            IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            IntPtr deltaNormalsArray,   // ovrAvatar2Vector3f**
            IntPtr resultBuffer
        )
        {
            unsafe
            {
                return ovrGpuSkinning_MorphTargetEncodeBufferData(
                        morphTargetDesc, (UInt16*)vertexIndexReordering, deltaPositionsArray, deltaNormalsArray, (byte*)resultBuffer)
                    .EnsureSuccess("OvrGpuSkinning_MorphTargetEncodeBufferData");
            }
        }

        public static bool OvrGpuSkinning_MorphTargetEncodeBufferDataWithTangents(
            in ovrGpuMorphTargetBufferDesc morphTargetDesc,
            /* const */ IntPtr vertexIndexReordering, // UInt16*
            IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            IntPtr deltaNormalsArray,   // ovrAvatar2Vector3f**
            IntPtr deltaTangentsArray,  // ovrAvatar2Vector3f**
            IntPtr resultBuffer
        )
        {
            unsafe
            {
                return ovrGpuSkinning_MorphTargetEncodeBufferDataWithTangents(
                        morphTargetDesc, (UInt16*)vertexIndexReordering, deltaPositionsArray, deltaNormalsArray, deltaTangentsArray, (byte*)resultBuffer)
                    .EnsureSuccess("OvrGpuSkinning_MorphTargetEncodeBufferDataWithTangents");
            }
        }

        //-----------------------------------------------------------------
        //
        // GpuSkinningIndirectionTextureInfo
        //

        public static UInt32 OvrGpuSkinning_IndirectionTextureInfoTexCoordsSizeInBytes()
        {
            if (ovrGpuSkinning_IndirectionTextureInfoTexCoordsSizeInBytes(out var coordSizeInBytes)
                .EnsureSuccess("ovrGpuSkinning_IndirectionTextureInfoTexCoordsSizeInBytes"))
            {
                return coordSizeInBytes;
            }
            return 0;
        }

        public static UInt32 OvrGpuSkinning_IndirectionTextureInfoTexelSizeInBytes()
        {
            if (ovrGpuSkinning_IndirectionTextureInfoTexelSizeInBytes(out var texelSize)
                .EnsureSuccess("ovrGpuSkinning_IndirectionTextureInfoTexelSizeInBytes"))
            {
                return texelSize;
            }
            return 0;
        }

        public static bool OvrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArrays(
            in ovrGpuSkinningRecti texelsInCombinedTex,
            UInt32 combinedTexSlice,
            UInt32 combinedTexWidth,
            UInt32 combinedTexHeight,
            in ovrAvatar2Vector3f unaffectedVertTexCoordInCombinedTex,
            UInt32 meshVertCount,
            UInt32 morphTargetAffectedVertCount,
            /*const*/ IntPtr meshVertIndexToAffectedVertIndex,  // Int32[]
            IntPtr resultPositionTexCoords, // float results in a raw byte array
            IntPtr resultNormalTexCoords    // float results in a raw byte array
        )
        {
            unsafe
            {
                return ovrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArrays(in texelsInCombinedTex
                    , combinedTexSlice, combinedTexWidth, combinedTexHeight, in unaffectedVertTexCoordInCombinedTex
                    , meshVertCount, morphTargetAffectedVertCount, (Int32*)meshVertIndexToAffectedVertIndex
                    , (float*)resultPositionTexCoords, (float*)resultNormalTexCoords)
                    .EnsureSuccess("ovrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArrays");
            }
        }

        public static bool OvrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArraysWithTangents(
            in ovrGpuSkinningRecti texelsInCombinedTex,
            UInt32 combinedTexSlice,
            UInt32 combinedTexWidth,
            UInt32 combinedTexHeight,
            in ovrAvatar2Vector3f unaffectedVertTexCoordInCombinedTex,
            UInt32 meshVertCount,
            UInt32 morphTargetAffectedVertCount,
            /*const*/ IntPtr meshVertIndexToAffectedVertIndex,  // Int32[]
            IntPtr resultPositionTexCoords, // float results in a raw byte array
            IntPtr resultNormalTexCoords,    // float results in a raw byte array
            IntPtr resultTangentTexCoords    // float results in a raw byte array
        )
        {
            unsafe
            {
                return ovrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArraysWithTangents(in texelsInCombinedTex
                    , combinedTexSlice, combinedTexWidth, combinedTexHeight, in unaffectedVertTexCoordInCombinedTex
                    , meshVertCount, morphTargetAffectedVertCount, (Int32*)meshVertIndexToAffectedVertIndex
                    , (float*)resultPositionTexCoords, (float*)resultNormalTexCoords, (float*)resultTangentTexCoords)
                    .EnsureSuccess("ovrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArraysWithTangents");
            }
        }

        public static bool OvrGpuSkinning_IndirectionTextureInfoPopulateTextureData(
            UInt32 texWidth,
            UInt32 texHeight,
            UInt32 meshVertCount,
            /*const*/ IntPtr positionTexCoords,   // float[]
            /*const*/ IntPtr normalTexCoords,     // float[]
            IntPtr resultBuffer,   // byte results in a raw byte array
            UInt32 resultBufferSize
        )
        {
            unsafe
            {
                return ovrGpuSkinning_IndirectionTextureInfoPopulateTextureData(texWidth, texHeight, meshVertCount
                    , (float*)positionTexCoords, (float*)normalTexCoords
                    , (byte*)resultBuffer, resultBufferSize)
                    .EnsureSuccess("ovrGpuSkinning_IndirectionTextureInfoPopulateTextureData");
            }
        }

        public static bool OvrGpuSkinning_IndirectionTextureInfoPopulateTextureDataWithTangents(
            UInt32 texWidth,
            UInt32 texHeight,
            UInt32 meshVertCount,
            /*const*/ IntPtr positionTexCoords,   // float[]
            /*const*/ IntPtr normalTexCoords,     // float[]
            /*const*/ IntPtr tangentTexCoords,    // float[]
            IntPtr resultBuffer,   // byte results in a raw byte array
            UInt32 resultBufferSize
        )
        {
            unsafe
            {
                return ovrGpuSkinning_IndirectionTextureInfoPopulateTextureDataWithTangents(texWidth, texHeight, meshVertCount
                    , (float*)positionTexCoords, (float*)normalTexCoords, (float*)tangentTexCoords
                    , (byte*)resultBuffer, resultBufferSize)
                    .EnsureSuccess("ovrGpuSkinning_IndirectionTextureInfoPopulateTextureDataWithTangents");
            }
        }


        public static ovrGpuSkinningTextureDesc OvrGpuSkinning_JointTextureDesc(
            UInt32 maxTexDimension,
            UInt32 numMeshVerts
        )
        {
            if (ovrGpuSkinning_JointTextureDesc(maxTexDimension, numMeshVerts, out var texDesc)
                .EnsureSuccess("ovrGpuSkinning_JointTextureDesc"))
            {
                return texDesc;
            }
            return default;
        }


        public static bool OvrGpuSkinning_JointEncodeTextureData(
            in ovrGpuSkinningTextureDesc desc,
            UInt32 numMeshVerts,
            IntPtr jointIndices, // ovrAvatar2Vector4us[]
            IntPtr jointWeights,  // ovrAvatar2Vector4f[]
            IntPtr resultBuffer,
            UInt32 resultBufferSize
        )
        {
            unsafe
            {
                return (ovrGpuSkinning_JointEncodeTextureData(in desc, numMeshVerts
                    , (ovrAvatar2Vector4us*)jointIndices, (ovrAvatar2Vector4f*)jointWeights, (byte*)resultBuffer, resultBufferSize)
                    .EnsureSuccess("ovrGpuSkinning_JointEncodeTextureData"));
            }
        }

        public static ovrGpuSkinningBufferDesc OvrGpuSkinning_JointWeightsBufferDesc(
            UInt32 numMeshVerts
        )
        {
            if (ovrGpuSkinning_JointWeightsBufferDesc(numMeshVerts, out var bufferDesc)
                .EnsureSuccess("ovrGpuSkinning_JointWeightsBufferDesc"))
            {
                return bufferDesc;
            }

            return default;
        }


        public static ovrGpuSkinningBufferDesc OvrGpuSkinning_JointIndicesBufferDesc(
            UInt32 numMeshVerts, ovrGpuSkinningEncodingPrecision encodingPrecision
        )
        {
            if (ovrGpuSkinning_JointIndicesBufferDesc(numMeshVerts, encodingPrecision, out var bufferDesc)
                .EnsureSuccess("ovrGpuSkinning_JointIndicesBufferDesc"))
            {
                return bufferDesc;
            }

            return default;
        }

        public static bool OvrGpuSkinning_EncodeJointWeightsBufferData(
            in ovrGpuSkinningBufferDesc desc,
            /* const */ IntPtr jointWeights, // ovrAvatar2Vector4f[]
            /* const */ IntPtr vertexIndexReordering, // UInt16*
            IntPtr resultBuffer // byte*
        )
        {
            unsafe
            {
                return ovrGpuSkinning_EncodeJointWeightsBufferData(
                        desc,
                        (ovrAvatar2Vector4f*)jointWeights,
                        (UInt16*)vertexIndexReordering,
                        (byte*)resultBuffer)
                    .EnsureSuccess("ovrGpuSkinning_EncodeJointWeightsBufferData");
            }
        }

        public static bool OvrGpuSkinning_EncodeJointIndicesBufferData(
            in ovrGpuSkinningBufferDesc desc,
            /* const */ IntPtr jointIndices, // ovrAvatar2Vector4us[]
            /* const */ IntPtr vertexIndexReordering, // UInt16*
            IntPtr resultBuffer // byte*
        )
        {
            unsafe
            {
                return ovrGpuSkinning_EncodeJointIndicesBufferData(
                        desc,
                        (ovrAvatar2Vector4us*)jointIndices,
                        (UInt16*)vertexIndexReordering,
                        (byte*)resultBuffer)
                    .EnsureSuccess("ovrGpuSkinning_EncodeJointIndicesBufferData");
            }
        }

        //-----------------------------------------------------------------
        //
        // Atlas Packer
        //

        // enum only used for type-safety
        public enum AtlasPackerId : Int32 { Invalid = -1 };

        // a copy of OVR::GpuSkinning::AtlasPacker::PackingAlgorithm
        public enum AtlasPackerPackingAlgortihm : Int32
        {
            Runtime,
            Preprocess,
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrTextureLayoutResult
        {
            public Int32 x, y;
            public Int32 w, h;
            public UInt32 texSlice;

            public static readonly ovrTextureLayoutResult INVALID_LAYOUT = new ovrTextureLayoutResult
            {
                x = 0,
                y = 0,
                w = 0,
                h = 0,
                texSlice = 0,
            };

            public ovrGpuSkinningRecti ExtractRectiOnly()
            {
                return new ovrGpuSkinningRecti(x, y, w, h);
            }

            public bool IsValid => x >= 0 && y >= 0 && w > 0 && h > 0;
        }

        //-----------------------------------------------------------------
        //
        // GpuSkinning_AtlasPacker
        //

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrGpuSkinningRecti
        {
            public int x, y;
            public int w, h;

            public ovrGpuSkinningRecti(int x_, int y_, int w_, int h_) { x = x_; y = y_; w = w_; h = h_; }
        }

        public static bool OvrGpuSkinning_AtlasPackerDestroy(
            AtlasPackerId id
        )
        {
            return ovrGpuSkinning_AtlasPackerDestroy(id)
                .EnsureSuccess("ovrGpuSkinning_AtlasPackerDestroy");
        }

        public static ovrGpuSkinningHandle ovrGpuSkinning_AtlasPackerResultsForBlock(
            AtlasPackerId id,
            UInt32 width,
            UInt32 height)
        {
            if (ovrGpuSkinning_AtlasPackerAddBlock(id, width, height, out var newHandle)
                .EnsureSuccess("ovrGpuSkinning_AtlasPackerAddBlock"))
            {
                return newHandle;
            }
            return ovrGpuSkinningHandle.AtlasPackerId_Invalid;
        }

        public static ovrTextureLayoutResult OvrGpuSkinning_AtlasPackerResultsForBlock(
            AtlasPackerId id,
            ovrGpuSkinningHandle handle)
        {
            if (ovrGpuSkinning_AtlasPackerResultsForBlock(id, handle, out var result)
                .EnsureSuccess("ovrGpuSkinning_AtlasPackerResultsForBlock"))
            {
                return result;
            }
            return default;
        }

        public static bool OvrGpuSkinning_AtlasPackerRemoveBlock(
            AtlasPackerId id,
            ovrGpuSkinningHandle handle)
        {
            return ovrGpuSkinning_AtlasPackerRemoveBlock(id, handle)
                .EnsureSuccess("ovrGpuSkinning_AtlasPackerRemoveBlock");
        }

        //-----------------------------------------------------------------
        //
        // GpuSkinningJointTextureInfo
        //

        // enum only used for type-safety
        public enum ovrGpuSkinningJointTextureInfoId : Int32
        {
            Invalid = 0
        };

        //-----------------------------------------------------------------
        //
        // GpuSkinningNeutralPoseEncoder
        //

        public static ovrGpuSkinningTextureDesc OvrGpuSkinning_NeutralPoseTextureDesc(
            UInt32 maxTexDimension,
            UInt32 numMeshVerts,
            bool hasTangents)
        {
            if (ovrGpuSkinning_NeutralPoseTextureDesc(
                maxTexDimension, numMeshVerts, hasTangents, out var newTexDesc)
                .EnsureSuccess("ovrGpuSkinning_NeutralPoseTextureDesc"))
            {
                return newTexDesc;
            }
            return default;
        }

        public static bool OvrGpuSkinning_NeutralPoseEncodeTextureData(
            in ovrGpuSkinningTextureDesc desc,
            UInt32 numMeshVerts,
            IntPtr neutralPositions, // ovrAvatar2Vector3f[]
            IntPtr neutralNormals, // ovrAvatar2Vector3f[]
            IntPtr neutralTangents, // ovrAvatar2Vector4f[]
            IntPtr resultBuffer,
            UInt32 resultBufferSize)
        {
            unsafe
            {
                return ovrGpuSkinning_NeutralPoseEncodeTextureData(
                    in desc, numMeshVerts, (ovrAvatar2Vector3f*)neutralPositions, (ovrAvatar2Vector3f*)neutralNormals
                    , (ovrAvatar2Vector4f*)neutralTangents, (byte*)resultBuffer, resultBufferSize)
                    .EnsureSuccess("ovrGpuSkinning_NeutralPoseEncodeTextureData");
            }
        }

        public static bool OvrGpuSkinning_NeutralPoseEncodeBufferData(
            UInt32 numMeshVerts,
            IntPtr neutralPositions, // ovrAvatar2Vector3f[]
            IntPtr neutralNormals, // ovrAvatar2Vector3f[]
            IntPtr neutralTangents, // ovrAvatar2Vector4f[]
            ovrGpuSkinningEncodingPrecision precision,
            bool alignVec4,
            IntPtr resultBuffer,
            UInt32 resultBufferSize)
        {
            unsafe
            {
                return ovrGpuSkinning_NeutralPoseEncodeBufferData(numMeshVerts,
                        (ovrAvatar2Vector3f*)neutralPositions, (ovrAvatar2Vector3f*)neutralNormals,
                        (ovrAvatar2Vector4f*)neutralTangents, precision, alignVec4, (byte*)resultBuffer,
                        resultBufferSize)
                    .EnsureSuccess("ovrGpuSkinning_NeutralPoseEncodeBufferData");
            }
        }

        public static ovrGpuSkinningBufferDesc OvrGpuSkinning_NeutralPositionsBufferDesc(
            UInt32 numMeshVerts,
            ovrGpuSkinningEncodingPrecision encodingPrecision)
        {
            if (ovrGpuSkinning_NeutralPositionsBufferDesc(
                    numMeshVerts,
                    encodingPrecision,
                    out var newDesc)
                .EnsureSuccess("ovrGpuSkinning_NeutralPositionsBufferDesc"))
            {
                return newDesc;
            }

            return default;
        }

        public static ovrGpuSkinningBufferDesc OvrGpuSkinning_NeutralNormalsBufferDesc(
            UInt32 numMeshVerts,
            ovrGpuSkinningEncodingPrecision encodingPrecision)
        {
            if (ovrGpuSkinning_NeutralNormalsBufferDesc(
                    numMeshVerts,
                    encodingPrecision,
                    out var newDesc)
                .EnsureSuccess("ovrGpuSkinning_NeutralNormalsBufferDesc"))
            {
                return newDesc;
            }

            return default;
        }

        public static ovrGpuSkinningBufferDesc OvrGpuSkinning_NeutralTangentsBufferDesc(
            UInt32 numMeshVerts,
            ovrGpuSkinningEncodingPrecision encodingPrecision)
        {
            if (ovrGpuSkinning_NeutralTangentsBufferDesc(
                    numMeshVerts,
                    encodingPrecision,
                    out var newDesc)
                .EnsureSuccess("ovrGpuSkinning_NeutralTangentsBufferDesc"))
            {
                return newDesc;
            }

            return default;
        }

        public static bool OvrGpuSkinning_EncodeNeutralPositionsBufferData(
            in ovrGpuSkinningBufferDesc desc,
            /* const */ IntPtr neutralPositions, // ovrAvatar2Vector3f*
            /* const */ IntPtr vertexIndexReordering, // UInt16*
            IntPtr resultBuffer) // byte*
        {
            unsafe
            {
                return ovrGpuSkinning_EncodeNeutralPositionsBufferData(
                        desc,
                        (ovrAvatar2Vector3f*)neutralPositions,
                        (UInt16*)vertexIndexReordering,
                        (byte*)resultBuffer)
                    .EnsureSuccess("ovrGpuSkinning_EncodeNeutralPositionsBufferData");
            }
        }

        public static bool OvrGpuSkinning_EncodeNeutralNormalsBufferData(
            in ovrGpuSkinningBufferDesc desc,
            /* const */ IntPtr neutralNormals, // ovrAvatar2Vector3f*
            /* const */ IntPtr vertexIndexReordering, // UInt16*
            IntPtr resultBuffer) // byte*
        {
            unsafe
            {
                return ovrGpuSkinning_EncodeNeutralNormalsBufferData(
                        desc,
                        (ovrAvatar2Vector3f*)neutralNormals,
                        (UInt16*)vertexIndexReordering,
                        (byte*)resultBuffer)
                    .EnsureSuccess("ovrGpuSkinning_EncodeNeutralNormalsBufferData");
            }
        }

        public static bool OvrGpuSkinning_EncodeNeutralTangentsBufferData(
            in ovrGpuSkinningBufferDesc desc,
            /* const */ IntPtr neutralTangents, // ovrAvatar2Vector4f*
            /* const */ IntPtr vertexIndexReordering, // UInt16*
            IntPtr resultBuffer) // byte*
        {
            unsafe
            {
                return ovrGpuSkinning_EncodeNeutralTangentsBufferData(
                        desc,
                        (ovrAvatar2Vector4f*)neutralTangents,
                        (UInt16*)vertexIndexReordering,
                        (byte*)resultBuffer)
                    .EnsureSuccess("ovrGpuSkinning_EncodeNeutralTangentsBufferData");
            }
        }

        //-----------------------------------------------------------------
        //
        // GpuSkinningJointTextureInfo
        //

        #region extern methods

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_Initialize();

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_Shutdown();

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_AtlasPackerCreate(
            UInt32 atlasWidth,
            UInt32 atlasHeight,
            AtlasPackerPackingAlgortihm packingAlgorithm,
            out AtlasPackerId createdId
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_AtlasPackerDestroy(
            AtlasPackerId id
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_AtlasPackerAddBlock(
            AtlasPackerId id,
            UInt32 width,
            UInt32 height,
            out ovrGpuSkinningHandle newHandle
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_AtlasPackerResultsForBlock(
            AtlasPackerId id,
            ovrGpuSkinningHandle handle,
            out ovrTextureLayoutResult result
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_AtlasPackerRemoveBlock(
            AtlasPackerId id,
            ovrGpuSkinningHandle handle
        );

        //-----------------------------------------------------------------
        //
        // GpuMorphTargetTextureInfo
        //

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrGpuSkinningResult ovrGpuSkinning_MorphTargetEncodeMeshVertToAffectedVert(
            UInt32 maxTexDimension,
            UInt32 numMeshVerts,
            UInt32 numMorphTargets,
            ovrGpuSkinningEncodingPrecision texType,
            /* const */ IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            /* const */ IntPtr deltaNormalsArray,    // ovrAvatar2Vector3f**
            Int32* meshVertToAffectedVert,
            out ovrGpuMorphTargetTextureDesc newMorphTargetInfo
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrGpuSkinningResult ovrGpuSkinning_MorphTargetEncodeMeshVertToAffectedVertWithTangents(
            UInt32 maxTexDimension,
            UInt32 numMeshVerts,
            UInt32 numMorphTargets,
            ovrGpuSkinningEncodingPrecision texType,
            /* const */ IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            /* const */ IntPtr deltaNormalsArray,    // ovrAvatar2Vector3f**
            /* const */ IntPtr deltaTangentsArray,    // ovrAvatar2Vector3f**
            Int32* meshVertToAffectedVert,
            out ovrGpuMorphTargetTextureDesc newMorphTargetInfo
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrGpuSkinningResult ovrGpuSkinning_MorphTargetEncodeTextureData(
            /* const */ in ovrGpuMorphTargetTextureDesc newMorphTargetInfo,
            /* const */ Int32* meshVertToAffectedVert,
            ovrGpuSkinningEncodingPrecision texType,
            /* const */ IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            /* const */ IntPtr deltaNormalsArray,    // ovrAvatar2Vector3f**
            byte* result
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrGpuSkinningResult ovrGpuSkinning_MorphTargetEncodeTextureDataWithTangents(
            /* const */ in ovrGpuMorphTargetTextureDesc newMorphTargetInfo,
            /* const */ Int32* meshVertToAffectedVert,
            ovrGpuSkinningEncodingPrecision texType,
            /* const */ IntPtr deltaPositionsArray, // ovrAvatar2Vector3f**
            /* const */ IntPtr deltaNormalsArray,    // ovrAvatar2Vector3f**
            /* const */ IntPtr deltaTangentsArray,    // ovrAvatar2Vector3f**
            byte* result
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrGpuSkinningResult ovrGpuSkinning_MorphTargetGetBufferMetaData(
            UInt32 numMeshVerts,
            UInt32 numMorphTargets,
            ovrGpuSkinningEncodingPrecision encodingPrecision,
            /* const */ IntPtr deltaPositionsArray,
            /* const */ IntPtr deltaNormalsArray,
            /* const */ IntPtr jointWeights,
            UInt16* vertexIndexReordering,
            out ovrGpuMorphTargetBufferDesc newMorphTargetInfo);

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrGpuSkinningResult ovrGpuSkinning_MorphTargetGetBufferMetaDataWithTangents(
            UInt32 numMeshVerts,
            UInt32 numMorphTargets,
            ovrGpuSkinningEncodingPrecision encodingPrecision,
            /* const */ IntPtr deltaPositionsArray,
            /* const */ IntPtr deltaNormalsArray,
            /* const */ IntPtr deltaTangentsArray,
            /* const */ IntPtr jointWeights,
            UInt16* vertexIndexReordering,
            out ovrGpuMorphTargetBufferDesc newMorphTargetInfo);

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrGpuSkinningResult ovrGpuSkinning_MorphTargetEncodeBufferData(
            /* const */ in ovrGpuMorphTargetBufferDesc morphTargetInfo,
            /* const */ UInt16* vertexIndexReordering,
            /* const */ IntPtr deltaPositionsArray,
            /* const */ IntPtr deltaNormalsArray,
            byte* result);

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrGpuSkinningResult ovrGpuSkinning_MorphTargetEncodeBufferDataWithTangents(
            /* const */ in ovrGpuMorphTargetBufferDesc morphTargetInfo,
            /* const */ UInt16* vertexIndexReordering,
            /* const */ IntPtr deltaPositionsArray,
            /* const */ IntPtr deltaNormalsArray,
            /* const */ IntPtr deltaTangentsArray,
            byte* result);

        //-----------------------------------------------------------------
        //
        // GpuSkinningIndirectionTextureInfo
        //

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_IndirectionTextureInfoTexCoordsSizeInBytes(out UInt32 coordSizeInBytes);

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_IndirectionTextureInfoTexelSizeInBytes(out UInt32 texelSizeInBytes);

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrGpuSkinningResult ovrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArrays(
            in ovrGpuSkinningRecti texelsInCombinedTex,
            UInt32 combinedTexSlice,
            UInt32 combinedTexWidth,
            UInt32 combinedTexHeight,
            in ovrAvatar2Vector3f unaffectedVertTexCoordInCombinedTex,
            UInt32 meshVertCount,
            UInt32 morphTargetAffectedVertCount,
            /*const*/ Int32* meshVertIndexToAffectedVertIndex,  // Int32[]
            float* resultPositionTexCoords, // float results in a raw byte array
            float* resultNormalTexCoords    // float results in a raw byte array
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrGpuSkinningResult ovrGpuSkinning_IndirectionTextureInfoPopulateTextureCoordinateArraysWithTangents(
            in ovrGpuSkinningRecti texelsInCombinedTex,
            UInt32 combinedTexSlice,
            UInt32 combinedTexWidth,
            UInt32 combinedTexHeight,
            in ovrAvatar2Vector3f unaffectedVertTexCoordInCombinedTex,
            UInt32 meshVertCount,
            UInt32 morphTargetAffectedVertCount,
            /*const*/ Int32* meshVertIndexToAffectedVertIndex,  // Int32[]
            float* resultPositionTexCoords, // float results in a raw byte array
            float* resultNormalTexCoords,   // float results in a raw byte array
            float* resultTangetTexCoords    // float results in a raw byte array
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrGpuSkinningResult ovrGpuSkinning_IndirectionTextureInfoPopulateTextureData(
            UInt32 texWidth,
            UInt32 texHeight,
            UInt32 meshVertCount,
            /*const*/ float* positionTexCoords,   // float[]
            /*const*/ float* normalTexCoords,     // float[]
            byte* resultBuffer,   // byte results in a raw byte array
            UInt32 resultBufferSize
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrGpuSkinningResult ovrGpuSkinning_IndirectionTextureInfoPopulateTextureDataWithTangents(
            UInt32 texWidth,
            UInt32 texHeight,
            UInt32 meshVertCount,
            /*const*/ float* positionTexCoords,   // float[]
            /*const*/ float* normalTexCoords,     // float[]
            /*const*/ float* tangentTexCoords,    // float[]
            byte* resultBuffer,   // byte results in a raw byte array
            UInt32 resultBufferSize
        );

        //-----------------------------------------------------------------
        //
        // GpuSkinningNeutralPoseEncoder
        //

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_NeutralPoseTextureDesc(
            UInt32 maxTexDimension,
            UInt32 numMeshVerts,
            bool hasTangents,
            out ovrGpuSkinningTextureDesc newTextureDesc
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrGpuSkinningResult ovrGpuSkinning_NeutralPoseEncodeTextureData(
            in ovrGpuSkinningTextureDesc desc,
            UInt32 numMeshVerts,
            /*const*/ ovrAvatar2Vector3f* neutralPositions, // ovrAvatar2Vector3f[]
            /*const*/ ovrAvatar2Vector3f* neutralNormals, // ovrAvatar2Vector3f[]
            /*const*/ ovrAvatar2Vector4f* neutralTangents, // ovrAvatar2Vector4f[]
            byte* resultBuffer,
            UInt32 resultBufferSize
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrGpuSkinningResult ovrGpuSkinning_NeutralPoseEncodeBufferData(
            UInt32 numMeshVerts,
            /*const*/ ovrAvatar2Vector3f* neutralPositions, // ovrAvatar2Vector3f[]
            /*const*/ ovrAvatar2Vector3f* neutralNormals, // ovrAvatar2Vector3f[]
            /*const*/ ovrAvatar2Vector4f* neutralTangents, // ovrAvatar2Vector4f[]
            ovrGpuSkinningEncodingPrecision precision,
            bool alignVec4,
            byte* resultBuffer,
            UInt32 resultBufferSize
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_NeutralPositionsBufferDesc(
            UInt32 numMeshVerts,
            ovrGpuSkinningEncodingPrecision encodingPrecision,
            out ovrGpuSkinningBufferDesc bufferDesc
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_NeutralNormalsBufferDesc(
            UInt32 numMeshVerts,
            ovrGpuSkinningEncodingPrecision encodingPrecision,
            out ovrGpuSkinningBufferDesc bufferDesc
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_NeutralTangentsBufferDesc(
            UInt32 numMeshVerts,
            ovrGpuSkinningEncodingPrecision encodingPrecision,
            out ovrGpuSkinningBufferDesc bufferDesc
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrGpuSkinningResult ovrGpuSkinning_EncodeNeutralPositionsBufferData(
            in ovrGpuSkinningBufferDesc desc,
            /* const */ ovrAvatar2Vector3f* neutralPositions,
            /* const */ UInt16* vertexIndexReordering,
            byte* resultBuffer
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrGpuSkinningResult ovrGpuSkinning_EncodeNeutralNormalsBufferData(
            in ovrGpuSkinningBufferDesc desc,
            /* const */ ovrAvatar2Vector3f* neutralNormals,
            /* const */ UInt16* vertexIndexReordering,
            byte* resultBuffer
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrGpuSkinningResult ovrGpuSkinning_EncodeNeutralTangentsBufferData(
            in ovrGpuSkinningBufferDesc desc,
            /* const */ ovrAvatar2Vector4f* neutralTangents,
            /* const */ UInt16* vertexIndexReordering,
            byte* resultBuffer
        );

        //-----------------------------------------------------------------
        //
        // GpuSkinningJointEncoder
        //

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_JointTextureDesc(
            UInt32 maxTexDimension,
            UInt32 numMeshVerts,
            out ovrGpuSkinningTextureDesc texDesc
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrGpuSkinningResult ovrGpuSkinning_JointEncodeTextureData(
            in ovrGpuSkinningTextureDesc desc,
            UInt32 numMeshVerts,
            /* const */ ovrAvatar2Vector4us* jointIndices,
            /* const */ ovrAvatar2Vector4f* jointWeights,
            byte* resultBuffer,
            UInt32 resultBufferSize
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_JointWeightsBufferDesc(
            UInt32 numMeshVerts,
            out ovrGpuSkinningBufferDesc bufferDesc
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrGpuSkinningResult ovrGpuSkinning_JointIndicesBufferDesc(
            UInt32 numMeshVerts,
            ovrGpuSkinningEncodingPrecision encodingPrecision,
            out ovrGpuSkinningBufferDesc bufferDesc
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrGpuSkinningResult ovrGpuSkinning_EncodeJointWeightsBufferData(
            in ovrGpuSkinningBufferDesc desc,
            /* const */ ovrAvatar2Vector4f* jointWeights,  // ovrAvatar2Vector4f[]
            /* const */ UInt16* vertexIndexReordering,
            byte* resultBuffer
        );

        [DllImport(GpuSkinningLibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrGpuSkinningResult ovrGpuSkinning_EncodeJointIndicesBufferData(
            in ovrGpuSkinningBufferDesc desc,
            /* const */ ovrAvatar2Vector4us* jointIndices, // ovrAvatar2Vector4us[]
            /* const */ UInt16* vertexIndexReordering,
            byte* resultBuffer
        );


        #endregion // extern methods
    }
}
