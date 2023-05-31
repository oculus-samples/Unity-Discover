using System;
using System.Runtime.InteropServices;

using Unity.Collections;

namespace Oculus.Avatar2
{
    public partial class CAPI
    {
        //-----------------------------------------------------------------
        // For the following ovrAvatar2Primitive_GetVertex* and ovrAvatar2MorphTarget_GetVertex* functions,
        // the buffer, bytes, and stride can be set to 0 to check the existence of that attribute.
        // If it is not available, the result will be ovrAvatar2Result_DataNotAvailable
        //

        //-----------------------------------------------------------------
        //
        // Diagnostic
        //
        //

        private const string assetLogScope = "OvrAvatarAPI_Asset";

        // Get the number of assets which are still loading
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern UInt32 ovrAvatar2Asset_GetNumberOutstandingAssets();

        // Get the number of assets which are still loading for an entity
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern UInt32 ovrAvatar2Asset_GetEntityPendingCount(ovrAvatar2EntityId entityId);

        //-----------------------------------------------------------------
        //
        // Load Request
        //
        //

        public enum ovrAvatar2LoadRequestState : Int32
        {
            /// Invalid state
            None = 0

            ,

            /// User Avatar specification requested
            PendingSpecification

            ,

            /// CDN asset load in progress (network or from cache)
            CdnLoad

            ,

            /// Loading assets from URI
            LoadFromUri

            ,

            /// Loading assets from memory
            LoadFromMemory

            ,

            /// Parsing asset files after load
            ParseAsset

            ,

            /// Awaiting "ready to render" from client
            ClientLoad

            ,

            /// Load successful; assets have been applied to the entity. hierarchyVersion/allNodesVersion likely changed
            Success

            ,

            /// Failed; see LoadFailedReason for details
            Failed

            ,

            /// Cancelled before completion (e.g. entity destroyed)
            Cancelled
        }

        public enum ovrAvatar2LoadRequestFailure : Int32
        {
            None = 0
            , CdnLoadInProgress
            , NoAssetProfile
            , SpecRequestFailed
            , SpecParseFailed
            , SpecRequestCancelled
            , MissingAvatar
            , SpecHadInvalidAnimSet
            , SpecHadInvalidModel
            , InvalidUri
            , AssetNotFound
            , MismatchedLoadFilters
            , HttpLoadFailed
            , CacheLoadFailed
            , DiskLoadFailed
            , ParseFailed
            , Unknown
            ,
        }

        public enum ovrAvatar2LoadRequestType : Int32
        {
            User
            , Memory
            , Uri
            ,
        }

        ///
        /// Provides information about the state of an asset load from a specific call to LoadUser
        /// or a similar function.
        public struct ovrAvatar2LoadRequestInfo
        {
            public ovrAvatar2LoadRequestId id;
            public ovrAvatar2EntityId entityId;
            public ovrAvatar2LoadRequestState state;
            public ovrAvatar2LoadRequestFailure failedReason;
            public ovrAvatar2LoadRequestType type;
            public Int64 responseCode;
        }

        ///
        /// Get the state of a specific load request
        /// \param loadRequestId to load - obtained from a call to LoadUser or similar function
        /// \param loadRequestInfo (out)
        /// \return result code
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_GetLoadRequestInfo(
            ovrAvatar2LoadRequestId loadRequestId, out ovrAvatar2LoadRequestInfo loadRequestInfo);

        //-----------------------------------------------------------------
        //
        // Resource
        //
        //

        public enum ovrAvatar2AssetStatus : Int32
        {
            ovrAvatar2AssetStatus_LoadFailed = 0
            , ovrAvatar2AssetStatus_Loaded
            , ovrAvatar2AssetStatus_Unloaded
            , ovrAvatar2AssetStatus_Updated
            ,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Asset_Resource
        {
            public ovrAvatar2AssetStatus status;
            public ovrAvatar2Id assetID; // The asset id
        }

        /// Release a resource after it has been loaded by the client application
        /// Can be called from any thread
        /// \param resourceID provided by ovrAvatar2Asset_ResourceCallback
        /// \return result code
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result ovrAvatar2Asset_ReleaseResource(ovrAvatar2Id resourceId);

        /// Notify the Avatar runtime that the client application is ready to render the resource
        /// Can be called from any thread
        /// \param resourceID provided by ovrAvatar2Asset_ResourceCallback
        /// \return result code
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result ovrAvatar2Asset_ResourceReadyToRender(ovrAvatar2Id resourceID);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern unsafe ovrAvatar2Result ovrAvatar2Asset_GetName(
            ovrAvatar2Id id, byte* nameBuffer, UInt32 bufferByteSize);


        internal static bool OvrAvatarAsset_ReleaseResource(ovrAvatar2Id resourceId)
        {
            return ovrAvatar2Asset_ReleaseResource(resourceId)
                .EnsureSuccess("ovrAvatar2Asset_ReleaseResource");
        }

        internal static bool OvrAvatarAsset_ResourceReadyToRender(ovrAvatar2Id resourceId)
        {
            // TODO: Call this at an appropriate later time when all Unity side processing is complete
            return ovrAvatar2Asset_ResourceReadyToRender(resourceId)
                .EnsureSuccess("ovrAvatar2Asset_ResourceReadyToRender");
        }


        //
        // Primitive
        //

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_GetPrimitiveCount(
            ovrAvatar2Id resourceId, out UInt32 count);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_GetPrimitiveByIndex(
            ovrAvatar2Id resourceId, UInt32 primitiveIndex, out ovrAvatar2Primitive primitive);

        //
        // Image
        //

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_GetImageCount(
            ovrAvatar2Id resourceId, out UInt32 count);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_GetImageByIndex(
            ovrAvatar2Id resourceId, UInt32 imageIndex, out ovrAvatar2Image image);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_GetImageDataByIndex(
            ovrAvatar2Id resourceId, UInt32 imageIndex, /*byte[]*/ IntPtr buffer, UInt32 bufferSize);


        //-----------------------------------------------------------------
        //
        // Image
        //
        //

        /**
         * Enumerates the possible image formats
         * for an avatar texture.
         * @see ovrAvatar2Image
         */
        public enum ovrAvatar2ImageFormat : UInt32
        {
            ///<summary>Invalid image format</summary>
            Invalid = 0,
            RGBA32 = 0xe3dd9a1e, ///< RGBA 32bit uncompressed texture
            DXT1 = 0xb9ee766e, ///< DXT1/BC1 compressed texture
            DXT5 = 0x9a853814, ///< DXT5/BC3 compressed texture
            BC5U = 0xcee1cf1a, ///< BC5 compressed texture (unsigned)
            BC5S = 0x57c603f3, ///< BC5 compressed texture (signed)
            BC7U = 0xaa33790d, ///< BC7 compressed texture (unsigned)
            ASTC_RGBA_4x4 = 0xdc2b8f4c, ///< ASTC 4x4 compressed texture
            ASTC_RGBA_6x6 = 0xbd4fed74, ///< ASTC 6x6 compressed texture
            ASTC_RGBA_8x8 = 0xa75606e7, ///< ASTC 8x8 compressed texture
            ASTC_RGBA_12x12 = 0x7dfcb3d0, ///< ASTC 12x12 compressed texture
        }

        /**
         * Describes a 2D image used by an avatar asset.
         * The image is referenced by a unique ID.
         * @see ovrAvatar2ImageFormat
         */
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Image
        {
            public ovrAvatar2Id id; ///< unique id used to reference this image
            public ovrAvatar2ImageFormat format; ///< Image format type

            public UInt32 sizeX; ///< Image X dimension
            public UInt32 sizeY; ///< Image Y dimension
            public UInt32 mipCount; ///< Number of mipmap levels
            public UInt32 imageDataSize; ///< Image buffer size
        };


        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern unsafe CAPI.ovrAvatar2Result ovrAvatar2Asset_GetImageName(
            ovrAvatar2Id primitiveId, byte* nameBuffer, UInt32 bufferByteSize);

        //-----------------------------------------------------------------
        //
        // Primitive
        //
        //

        public enum ovrAvatar2AlphaMode : Int32
        {
            Opaque = 0,
            Mask = 1,
            Blend = 2,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Primitive
        {
            public ovrAvatar2Id id; // unique id used to reference this mesh
            public ovrAvatar2VertexBufferId vertexBufferId;
            public ovrAvatar2MorphTargetBufferId morphTargetBufferId;
            public UInt32 indexCount;
            public UInt16 minIndexValue;
            public UInt16 maxIndexValue;
            public ovrAvatar2AlphaMode alphaMode; // The alpha rendering mode of the primitive
            public UInt32 textureCount;
            public UInt32 jointCount;
            public UInt32 skeleton;
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern unsafe CAPI.ovrAvatar2Result ovrAvatar2Asset_GetPrimitiveName(
            ovrAvatar2Id primitiveId, byte* nameBuffer, UInt32 bufferByteSize);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Primitive_GetMinMaxPosition(
            ovrAvatar2Id primitiveId, out ovrAvatar2Vector3f minPosition, out ovrAvatar2Vector3f maxPosition);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Primitive_GetSkinnedMinMaxPosition(
            ovrAvatar2Id primitiveId, out ovrAvatar2Vector3f minPosition, out ovrAvatar2Vector3f maxPosition);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_GetLodFlags(
            ovrAvatar2Id primitiveId,
            out ovrAvatar2EntityLODFlags lodFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_GetManifestationFlags(
            ovrAvatar2Id primitiveId, out ovrAvatar2EntityManifestationFlags manifestationFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_GetViewFlags(
            ovrAvatar2Id primitiveId, out ovrAvatar2EntityViewFlags viewFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_GetSubMeshInclusionFlags(
            ovrAvatar2Id primitiveId, out ovrAvatar2EntitySubMeshInclusionFlags subMeshInclusionFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Asset_GetHighQualityFlags(
            ovrAvatar2Id primitiveId, out ovrAvatar2EntityHighQualityFlags highQualityFlags);

        //-----------------------------------------------------------------
        //
        // Index Buffer
        //
        //

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern CAPI.ovrAvatar2Result ovrAvatar2Primitive_GetIndexData(
            ovrAvatar2Id primitiveId, UInt16* indexBuffer, UInt32 bytes);

        public static bool OvrAvatar2Primitive_GetIndexData(
            ovrAvatar2Id primitiveId, in NativeArray<UInt16> indexBuffer, UInt32 bytes)
        {
            unsafe
            {
                return ovrAvatar2Primitive_GetIndexData(primitiveId, indexBuffer.GetPtr(), bytes)
                    .EnsureSuccess("ovrAvatar2Primitive_GetIndexData", assetLogScope);
            }
        }


        //-----------------------------------------------------------------
        //
        // Vertex Buffer
        //
        //

        // Get the vertex count of a vertex buffer
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetVertexCount(
            ovrAvatar2VertexBufferId id, out UInt32 vertexCount);

        // Get the minimum x,y,z and maximum x,y,z values for positions in the vertex buffer
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetMinMaxPosition(
            ovrAvatar2VertexBufferId id,
            out ovrAvatar2Vector3f minPosition,
            out ovrAvatar2Vector3f maxPosition);

        // Get vertex buffer position data
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetPositions(
            ovrAvatar2VertexBufferId vertexBufferId, /*ovrAvatar2Vector3f[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        // Get vertex buffer normal data
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetNormals(
            ovrAvatar2VertexBufferId vertexBufferId, /*ovrAvatar2Vector3f[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        // Get vertex buffer tangent data
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetTangents(
            ovrAvatar2VertexBufferId vertexBufferId, /*ovrAvatar2Vector4f[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        // Get vertex buffer color data
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetColors(
            ovrAvatar2VertexBufferId vertexBufferId, /*ovrAvatar2Vector4f[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        // Get vertex buffer ORMT "color" data
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetColorsORMT(
            ovrAvatar2VertexBufferId vertexBufferId, /*ovrAvatar2Vector4f[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        // Get vertex buffer texcoord0 data
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetTexCoord0(
            ovrAvatar2VertexBufferId vertexBufferId, /*ovrAvatar2Vector2f[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        // Get vertex buffer joint index data
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetJointIndices(
            ovrAvatar2VertexBufferId vertexBufferId, /*ovrAvatar2Vector4us[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        // Get vertex buffer joint weight data
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetJointWeights(
            ovrAvatar2VertexBufferId vertexBufferId, /*ovrAvatar2Vector4f[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        // Get vertex buffer submesh ID data
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetSubMeshIds(
            ovrAvatar2Id primitiveId, ovrAvatar2VertexBufferId vertexBufferId, /*Uint32[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetSubMeshIdsFloat(
            ovrAvatar2Id primitiveId, ovrAvatar2VertexBufferId vertexBufferId, /*float[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        // Get vertex buffer submesh types
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetSubMeshTypes(
            ovrAvatar2Id primitiveId, ovrAvatar2VertexBufferId vertexBufferId, /*Uint32[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2VertexBuffer_GetSubMeshTypesFloat(
            ovrAvatar2Id primitiveId, ovrAvatar2VertexBufferId vertexBufferId, /*float[]*/ IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        //-----------------------------------------------------------------
        //
        // Morph Target
        //
        //

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2MorphTarget_GetByName(
            ovrAvatar2MorphTargetBufferId primitiveId, string name, out UInt32 morphTargetIndex);


        // Get the morph target count of a vertex buffer
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2VertexBuffer_GetMorphTargetCount(
            ovrAvatar2MorphTargetBufferId id, out UInt32 morphTargetCount);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe ovrAvatar2Result ovrAvatar2MorphTarget_GetVertexPositions(
            ovrAvatar2MorphTargetBufferId primitiveId, UInt32 morphTargetIndex, ovrAvatar2Vector3f* buffer,
            UInt32 bytes, UInt32 stride);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe CAPI.ovrAvatar2Result ovrAvatar2MorphTarget_GetVertexNormals(
            ovrAvatar2MorphTargetBufferId primitiveId, UInt32 morphTargetIndex, ovrAvatar2Vector3f* buffer,
            UInt32 bytes, UInt32 stride);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe CAPI.ovrAvatar2Result ovrAvatar2MorphTarget_GetVertexTangents(
            ovrAvatar2MorphTargetBufferId primitiveId, UInt32 morphTargetIndex, ovrAvatar2Vector3f* buffer,
            UInt32 bytes, UInt32 stride);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern unsafe CAPI.ovrAvatar2Result ovrAvatar2Asset_GetMorphTargetName(
            ovrAvatar2MorphTargetBufferId primitiveId, UInt32 morphTargetIndex, byte* nameBuffer,
            UInt32 bufferByteSize);


        //-----------------------------------------------------------------
        //
        // Material
        //
        //

        public enum ovrAvatar2MaterialTextureType : Int32
        {
            BaseColor = 0, // sRGB color space, linear alpha
            Normal = 1, // Linear color space
            Occulusion = 2, // Linear color space
            MetallicRoughness = 3, // Linear color space
            Emissive = 4, // sRGB color space, linear alpha
            UsedInExtension = 5, // Handled by material extensions
        }

        // For MetallicRoughness, x = metallic factor, y = roughness factor
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2MaterialTexture
        {
            public ovrAvatar2MaterialTextureType type;
            public ovrAvatar2Vector4f factor;
            public ovrAvatar2Id imageId; // id of the image for the texture
        };

        public enum ovrAvatar2MaterialExtensionEntryType : Int32
        {
            ImageId = 0,
            Float = 1,
            Int = 2,
            Vector3f = 3,
            Vector4f = 4,
            Invalid = Int32.MaxValue,
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2MaterialExtensionEntry
        {
            public ovrAvatar2MaterialExtensionEntryType entryType;
            public UInt32 nameBufferSize; // with null terminator
            public UInt32 dataBufferSize;
        };

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Primitive_GetMaterialTextureByIndex(
            ovrAvatar2Id primitiveId, UInt32 materialTextureIndex, out ovrAvatar2MaterialTexture materialTexture);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern unsafe CAPI.ovrAvatar2Result ovrAvatar2Asset_GetPrimitiveMaterialName(
            ovrAvatar2Id primitiveId, byte* nameBuffer, UInt32 bufferByteSize);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern unsafe CAPI.ovrAvatar2Result ovrAvatar2Asset_DeduceMaterialSubMeshFromName(
            ovrAvatar2EntitySubMeshInclusionFlags* destType, byte* matName);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe CAPI.ovrAvatar2Result ovrAvatar2Primitive_GetNumMaterialExtensions(
            ovrAvatar2Id id,
            out UInt32 numExtensions);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern unsafe ovrAvatar2Result ovrAvatar2Primitive_GetMaterialExtensionName(
            ovrAvatar2Id id,
            UInt32 extensionIndex,
            byte* nameBuffer,
            UInt32* nameBufferSize);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Primitive_GetNumEntriesInMaterialExtensionByIndex(
            ovrAvatar2Id id,
            UInt32 extensionIndex,
            out UInt32 numEntries);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result ovrAvatar2Primitive_MaterialExtensionEntryMetaDataByIndex(
            ovrAvatar2Id id,
            UInt32 materialExtensionIndex,
            UInt32 entryIndex,
            out ovrAvatar2MaterialExtensionEntry entry);


        public static bool OvrAvatar2Primitive_MaterialExtensionEntryMetaDataByIndex(
            ovrAvatar2Id id,
            UInt32 materialExtensionIndex,
            UInt32 entryIndex,
            out ovrAvatar2MaterialExtensionEntry entry)
        {
            return ovrAvatar2Primitive_MaterialExtensionEntryMetaDataByIndex(id, materialExtensionIndex, entryIndex,
                out entry).EnsureSuccess("ovrAvatar2Primitive_MaterialExtensionEntryMetaDataByIndex", assetLogScope);
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe ovrAvatar2Result ovrAvatar2Primitive_MaterialExtensionEntryDataByIndex(
            ovrAvatar2Id id,
            UInt32 materialExtensionIndex,
            UInt32 entryIndex,
            byte* nameBuffer,
            UInt32 nameBufferSize,
            byte* dataBuffer,
            UInt32 dataBufferSize);

        public static unsafe bool OvrAvatar2Primitive_MaterialExtensionEntryDataByIndex(
            ovrAvatar2Id id,
            UInt32 materialExtensionIndex,
            UInt32 entryIndex,
            byte* nameBuffer,
            UInt32 nameBufferSize,
            byte* dataBuffer,
            UInt32 dataBufferSize)
        {
            return ovrAvatar2Primitive_MaterialExtensionEntryDataByIndex(id, materialExtensionIndex, entryIndex,
                    nameBuffer, nameBufferSize, dataBuffer, dataBufferSize)
                .EnsureSuccess("ovrAvatar2Primitive_MaterialExtensionEntryDataByIndex", assetLogScope);
        }

        //-----------------------------------------------------------------
        //
        // Joints
        //
        //

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2JointInfo
        {
            public Int32 jointIndex;
            public ovrAvatar2Matrix4f inverseBind;
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe ovrAvatar2Result ovrAvatar2Primitive_GetJointInfo(
            ovrAvatar2Id primitiveId, ovrAvatar2JointInfo* buffer, UInt32 bytes);


        //-----------------------------------------------------------------
        //
        // SubMesh
        //
        //

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2PrimitiveSubmesh
        {
            public UInt32 indexStart; // index into the index buffer of first primitive
            public UInt32 indexCount; // number of indices to use from the index buffer
            public UInt16 vertexStart; // lowest index value
            public UInt16 vertexCount; // highest index value - lowest index value
            public ovrAvatar2Vector2f minUVValues; // vertex UV min values
            public ovrAvatar2Vector2f maxUVValues; // vertex UV max values
            public ovrAvatar2Vector3f minValues; // vertex position min values
            public ovrAvatar2Vector3f maxValues; // vertex position max values

            public ovrAvatar2EntitySubMeshInclusionFlags
                inclusionFlags; // indicates the type of content in this submesh
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Primitive_GetSubMeshCount(
            ovrAvatar2Id primitiveId, out UInt32 subMeshCount);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Primitive_GetSubMeshByIndex(
            ovrAvatar2Id primitiveId, UInt32 subMeshIndex, out ovrAvatar2PrimitiveSubmesh subMesh);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern unsafe ovrAvatar2Result ovrAvatar2Primitive_GetSubMeshMaterialName(
            ovrAvatar2Id primitiveId, UInt32 subMeshIndex, byte* nameBuffer, UInt32 bufferByteSize);
    }
}
