using System;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

using static Oculus.Avatar2.OvrAvatarHelperExtensions;
/// @file OvrAvatarAPI_Entity.cs

namespace Oculus.Avatar2
{
    // TODO: This should be a static class
    public partial class CAPI
    {
        private const string entityLogScope = "OvrAvatarAPI_Entity";

        internal static ovrAvatar2EntityLoadNetworkSettings SpecificationNetworkSettings = ovrAvatar2_DefaultEntityNetworkSettings();
        internal static ovrAvatar2EntityLoadNetworkSettings AssetNetworkSettings = ovrAvatar2_DefaultEntityNetworkSettings();

        //-----------------------------------------------------------------
        //
        // Creation / Destruction
        //
        //

        [Flags]
        [System.Serializable]
        ///
        /// Describes avatar rendering and animation capabilities.
        ///
        public enum ovrAvatar2EntityFeatures : Int32
        {
            // Empty features flag, usually used for error signaling
            /* None value isn't needed in C# and conflicts w/ some Unity inspector logic for Flags */
            // None = 0,

            // Reserved for future use
            [InspectorName(null)]
            ReservedExtra = 1 << 0,

            /// Render avatar geometry
            Rendering_Prims = 1 << 1,

            /// Perform skinning on avatar
            Rendering_SkinningMatrices = 1 << 2,

            /// Calculate object space transforms (needed for GPU Skinning)
            Rendering_ObjectSpaceTransforms = 1 << 3,

            /// Allow avatar animation
            Animation = 1 << 4,

            ///  Use default avatar model
            UseDefaultModel = 1 << 5,

            /// Use default animation hierarchy
            UseDefaultAnimHierarchy = 1 << 6,

            ///  Do not use.
            AnalyticIk = 1 << 7,

            /// Use default facial animations
            UseDefaultFaceAnimations = 1 << 8,

            /// Display controllers in avatar hands (not implemented yet)
            ShowControllers = 1 << 9,

            /// Reproportions avatar hand bones according to tracking information in hand tracking mode
            HandScaling = 1 << 10,

            /// Allows to control the leg end-effector transforms using a two-bone IK solver.
            LegIk = 1 << 11,

            // Base set of features needed for entity rendering
            Rendering = Rendering_Prims | Rendering_SkinningMatrices,

            // Collection of all current feature flags
            All = Rendering_Prims | Rendering_SkinningMatrices | Rendering_ObjectSpaceTransforms | Animation
                         | UseDefaultModel | UseDefaultAnimHierarchy | AnalyticIk | UseDefaultFaceAnimations
                         | ShowControllers | HandScaling | LegIk,

            // Preset collection of feature flags for standard local avatar use case
            Preset_Default = Rendering | Animation | UseDefaultModel | UseDefaultAnimHierarchy
                             | UseDefaultFaceAnimations | HandScaling,

            // Preset collection for using AnalyticIk/SimpleIk
            Preset_AllIk = Preset_Default | AnalyticIk,

            // Preset for minimum functional local avatar
            Preset_Minimal = Rendering | Animation,

            // Preset for common remote avatar usage
            Preset_Remote = Rendering | UseDefaultModel,

            [InspectorName(null)]
            First = ReservedExtra,
            [InspectorName(null)]
            Last = LegIk,
        }

        ///
        /// Configures what characteristics will be loaded (level of detail, rendering characteristics, point of view).
        /// Exclusion from this filter will mean the setting is not loaded, saving memory and load time.
        /// NOTE: These values cannot be changed after OvrAvatarEntity.CreateEntity is called.
        /// @see ovrAvatar2EntityLODFlags
        /// @see ovrAvatar2EntityManifestationFlags
        /// @see ovrAvatar2EntityViewFlags
        ///
        [System.Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2EntityFilters
        {
            [EnumMask]
            public ovrAvatar2EntityLODFlags lodFlags; // unsigned ovrAvatar2EntityLODFlags
            [EnumMask]
            public ovrAvatar2EntityManifestationFlags manifestationFlags; // unsigned ovrAvatar2EntityManifestationFlags
            [EnumMask]
            public ovrAvatar2EntityViewFlags viewFlags; // unsigned ovrAvatar2EntityViewFlags
            [EnumMask]
            public ovrAvatar2EntitySubMeshInclusionFlags subMeshInclusionFlags; // unsigned ovrAvatar2EntitySubMeshInclusionFlags
            [EnumMask]
            public ovrAvatar2EntityHighQualityFlags highQualityFlags; // unsigned ovrAvatar2EntityHighQaulityFlags
        }


        ///
        /// Avatar creation configuration.
        /// Specifies overall level of detail, body parts to display,
        /// rendering and animation characteristics and the
        /// avatar's point of view.
        /// @see ovrAvatar2EntityLODFlags
        /// @see ovrAvatar2EntityManifestationFlags
        /// @see ovrAvatar2EntityFeatures
        /// @see ovrAvatar2EntityFilters
        ///
        [System.Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2EntityCreateInfo
        {
            [EnumMask]
            public ovrAvatar2EntityFeatures features;
            public ovrAvatar2EntityFilters renderFilters;

            // Abstract exact structure a bit for dependent code... since this is still public :X
            public ovrAvatar2EntityLODFlags lodFlags
            {
                get => renderFilters.lodFlags;
                set => renderFilters.lodFlags = value;
            }

            // TODO: Check for sensible input settings
            public bool IsValid => true;
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result ovrAvatar2Entity_Create(in ovrAvatar2EntityCreateInfo info, out ovrAvatar2EntityId entityId);

        /// Destroy an entity, releasing all related memory
        /// \param entity to destroy
        /// \return result code
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result ovrAvatar2Entity_Destroy(ovrAvatar2EntityId entityId);


        //-----------------------------------------------------------------
        //
        // LODs
        //
        //

        /// Gets the available level of details of the entity
        /// \param entity to query
        /// \param level of detail flags
        /// \return result code
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetAvailableLodFlags(
            ovrAvatar2EntityId entityId, out UInt32 lodFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetLodFlags(
            ovrAvatar2EntityId entityId, out ovrAvatar2EntityLODFlags lodFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_SetLodFlags(
            ovrAvatar2EntityId entityId, ovrAvatar2EntityLODFlags lodflags);

        //-----------------------------------------------------------------
        //
        // Manifestations
        //
        //

        /// Gets the available manifestations of the entity
        /// \param entity to query
        /// \param manifestation flags
        /// \return result code
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetAvailableManifestationFlags(
            ovrAvatar2EntityId entityId, out UInt32 manifestationFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetManifestationFlags(
            ovrAvatar2EntityId entityId, out ovrAvatar2EntityManifestationFlags manifestationflags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_SetManifestationFlags(
            ovrAvatar2EntityId entityId, ovrAvatar2EntityManifestationFlags manifestation);

        //-----------------------------------------------------------------
        //
        // View
        //
        //

        /// Gets the available views of the entity
        /// \param entity to query
        /// \param pointer to view flags
        /// \return result code
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetAvailableViewFlags(
            ovrAvatar2EntityId entityId, out UInt32 viewFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetViewFlags(
            ovrAvatar2EntityId entityId, out ovrAvatar2EntityViewFlags viewFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_SetViewFlags(
            ovrAvatar2EntityId entityId, ovrAvatar2EntityViewFlags viewflags);

        //-----------------------------------------------------------------
        //
        // SubMeshInclusions
        //
        //

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetSubMeshInclusionFlags(
            ovrAvatar2EntityId entityId, out ovrAvatar2EntitySubMeshInclusionFlags subMeshInclusionFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_SetSubMeshInclusionFlags(
            ovrAvatar2EntityId entityId, ovrAvatar2EntitySubMeshInclusionFlags subMeshInclusionFlags);

        //-----------------------------------------------------------------
        //
        // HighQuality
        //
        //

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetHighQualityFlags(
            ovrAvatar2EntityId entityId, out ovrAvatar2EntityHighQualityFlags highQualityFlags);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_SetHighQualityFlags(
            ovrAvatar2EntityId entityId, ovrAvatar2EntityHighQualityFlags highQualityFlags);

        //-----------------------------------------------------------------
        //
        // Pose
        //
        //

        /// Query the current pose for the an entity
        /// \param entity to query
        /// \param pose structure to fill out
        /// \param pose version to fill out (scoped to the entity)
        /// \return result code
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetPose(
            ovrAvatar2EntityId entityId, out ovrAvatar2Pose posePtr, out ovrAvatar2HierarchyVersion hierarchyVersion);

        /// Updates the current pose for an entity.
        /// Does not add or remove joints, but will update the joints.
        /// You can provide only partian information like just the transforms to update only those parts.
        /// \param entity to query
        /// \param pose data
        /// \return result code
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_UpdatePose(ovrAvatar2EntityId entityId, in ovrAvatar2Pose posePtr);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_SetRoot(ovrAvatar2EntityId entityId, ovrAvatar2Transform root);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe ovrAvatar2Result ovrAvatar2Entity_SetRoots(ovrAvatar2EntityId* entityIds, ovrAvatar2Transform* roots, uint numEntities);

        //-----------------------------------------------------------------
        //
        // Loading / Unloading
        //
        //

        internal struct ovrAvatar2EntityLoadNetworkSettings
        {
            public UInt32 timeoutMS; // If a request is not completed in this time, retry
            public UInt32 lowSpeedTimeSeconds; // If download speed is below lowSpeedLimitBytesPerSecond for this long, retry
            public UInt32 lowSpeedLimitBytesPerSecond; // see lowSpeedTimeSeconds
        }

        /// Setup ovrAvatar2EntityLoadNetworkSettings info with default values.
        ///
        private static ovrAvatar2EntityLoadNetworkSettings ovrAvatar2_DefaultEntityNetworkSettings()
        {
            ovrAvatar2EntityLoadNetworkSettings settings;
            settings.timeoutMS = 0U;
            settings.lowSpeedTimeSeconds = 30U;
            settings.lowSpeedLimitBytesPerSecond = 60U;
            return settings;
        }

        /// Note that loadFilters and numLodsWithMorphs work together. Morph targets will only be
        /// loaded for the highest numLodsWithMorphs. This is taken from the available lods
        /// specified by loadFilters.lodFlags. i.e. if loadFilters.lodFlags was to specify only
        /// loading lods 0,2, and 4, and numLodsWithMorphs was 2. Then lods 0 and 2 would have
        /// morph targets, and lod 4 would not.
        ///
        internal struct ovrAvatar2EntityLoadSettings
        {
            public ovrAvatar2EntityFilters loadFilters;
            public ovrAvatar2EntityLoadNetworkSettings loadSpecificationNetworkSettings;
            public ovrAvatar2EntityLoadNetworkSettings loadAssetNetworkSettings;
            // 0 for no textures, minimum 80*1024 if > 0, UINT_MAX for no limit
            public UInt32 maxTextureMemoryBytes;
            public UInt32 numLodsWithMorphs;
        }

        /// Setup ovrAvatar2EntityLoadSettings info with default values.
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2EntityLoadSettings ovrAvatar2Entity_DefaultLoadSettings();

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2EntityLoadSettings ovrAvatar2Entity_MinimumLoadSettings();

        internal static ovrAvatar2EntityFilters OvrAvatar2_DefaultLoadFilters()
        {
            return ovrAvatar2_DefaultEntityLoadFilters();
        }

        static private ovrAvatar2EntityFilters ovrAvatar2_DefaultEntityLoadFilters()
        {
            return new ovrAvatar2EntityFilters
            {
                lodFlags = ovrAvatar2EntityLODFlags.All,
                manifestationFlags = ovrAvatar2EntityManifestationFlags.All,
                viewFlags = ovrAvatar2EntityViewFlags.All,
                subMeshInclusionFlags = ovrAvatar2EntitySubMeshInclusionFlags.All,
                highQualityFlags = CAPI.ovrAvatar2EntityHighQualityFlags.None
            };
        }

        internal static ovrAvatar2EntityLoadSettings OvrAvatar2_GetLoadSettings()
        {
            var loadSettings = ovrAvatar2Entity_DefaultLoadSettings();
            loadSettings.loadSpecificationNetworkSettings = SpecificationNetworkSettings;
            loadSettings.loadAssetNetworkSettings = AssetNetworkSettings;
            return loadSettings;
        }

        internal static ovrAvatar2EntityLoadSettings OvrAvatar2_GetFastLoadSettings()
        {
            var loadSettings = ovrAvatar2Entity_MinimumLoadSettings();
            loadSettings.loadSpecificationNetworkSettings = SpecificationNetworkSettings;
            loadSettings.loadAssetNetworkSettings = AssetNetworkSettings;
            return loadSettings;
        }

        /// Load assets described in a GLB file into the entity
        /// \param ovrAvatar2Entity to load into
        /// \param the uri to the GLB file
        ///        From file: "file://<path.glb>"
        ///        From zip file: "zip://<path.glb>"
        ///        From content delivery network (cdn): "cdn://<path.glb>"
        /// \param loadSettings for this load
        /// \param (out) loadRequestID to retrieve status from ovrAvatar2Asset_GetLoadRequestInfo
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern ovrAvatar2Result ovrAvatar2Entity_LoadUri(
            ovrAvatar2EntityId entityId, string path, ovrAvatar2EntityLoadSettings loadSettings, out ovrAvatar2LoadRequestId requestId);

        /// Load assets described in a GLB file into the entity
        /// \param ovrAvatar2Entity to load into
        /// \param the uri to the GLB file
        ///        From file: "file://<path.glb>"
        ///        From zip file: "zip://<path.glb>"
        ///        From content delivery network (cdn): "cdn://<path.glb>"
        /// \param (out) loadRequestID to retrieve status from ovrAvatar2Asset_GetLoadRequestInfo
        public static ovrAvatar2Result OvrAvatarEntity_LoadUri(ovrAvatar2EntityId entityId, string path, out ovrAvatar2LoadRequestId requestId)
        {
            var defaultLoadSettings = OvrAvatar2_GetLoadSettings();
            return ovrAvatar2Entity_LoadUri(entityId, path, defaultLoadSettings, out requestId);
        }


        /// \param the uri to the GLB file
        ///        From file: "file://<path.glb>"
        ///        From zip file: "zip://<path.glb>"
        ///        From content delivery network (cdn): "cdn://<path.glb>"
        /// \param load filters for this load
        /// \param (out) loadRequestID to retrieve status from ovrAvatar2Asset_GetLoadRequestInfo
        public static ovrAvatar2Result OvrAvatarEntity_LoadUriWithFilters(ovrAvatar2EntityId entityId, string uri, in ovrAvatar2EntityFilters loadFilters, out ovrAvatar2LoadRequestId requestId)
        {
            var loadSettings = OvrAvatar2_GetLoadSettings();
            loadSettings.loadFilters = loadFilters;
            return ovrAvatar2Entity_LoadUri(entityId, uri, loadSettings, out requestId);
        }

        /// \param the uri to the GLB file
        ///        From file: "file://<path.glb>"
        ///        From zip file: "zip://<path.glb>"
        ///        From content delivery network (cdn): "cdn://<path.glb>"
        /// \param load filters for this load
        /// \param (out) loadRequestID to retrieve status from ovrAvatar2Asset_GetLoadRequestInfo
        public static ovrAvatar2Result OvrAvatarEntity_LoadUriWithFiltersFast(ovrAvatar2EntityId entityId, string uri, in ovrAvatar2EntityFilters loadFilters, out ovrAvatar2LoadRequestId requestId)
        {
            var loadSettings = OvrAvatar2_GetFastLoadSettings();
            loadSettings.loadFilters.manifestationFlags = loadFilters.manifestationFlags;
            loadSettings.loadFilters.subMeshInclusionFlags = loadFilters.subMeshInclusionFlags;
            loadSettings.loadFilters.viewFlags = loadFilters.viewFlags;
            loadSettings.loadFilters.highQualityFlags = CAPI.ovrAvatar2EntityHighQualityFlags.None;
            return ovrAvatar2Entity_LoadUri(entityId, uri, loadSettings, out requestId);
        }

        /// \param the uri to the GLB file
        ///        From file: "file://<path.glb>"
        ///        From zip file: "zip://<path.glb>"
        ///        From content delivery network (cdn): "cdn://<path.glb>"
        /// \param LODFlags representing which LODs to load
        /// \param (out) loadRequestID to retrieve status from ovrAvatar2Asset_GetLoadRequestInfo
        public static ovrAvatar2Result OvrAvatarEntity_LoadUriWithLODFilter(ovrAvatar2EntityId entityId, string uri, ovrAvatar2EntityLODFlags lodFilter, out ovrAvatar2LoadRequestId requestId)
        {
            var renderFilter = ovrAvatar2_DefaultEntityLoadFilters();
            renderFilter.lodFlags = lodFilter;
            return OvrAvatarEntity_LoadUriWithFilters(entityId, uri, in renderFilter, out requestId);
        }

        /// Load assets described in an in-memory GLB file into the entity.
        /// \param ovrAvatar2Entity to load into
        /// \param pointer to the beginning of a memory buffer
        /// \param the length of the memory buffer
        /// \param name what should be used to reference this glb
        /// \param loadSettings for this load
        /// \param (out) loadRequestID to retrieve status from ovrAvatar2Asset_GetLoadRequestInfo
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern ovrAvatar2Result ovrAvatar2Entity_LoadMemory(
            ovrAvatar2EntityId entityId, IntPtr data, UInt32 dataSize, string name, ovrAvatar2EntityLoadSettings loadSettings, out ovrAvatar2LoadRequestId requestId);

        public static ovrAvatar2Result OvrAvatarEntity_LoadMemory(
            ovrAvatar2EntityId entityId, IntPtr data, UInt32 dataSize, string name, out ovrAvatar2LoadRequestId requestId)
        {
            var defaultLoadSettings = OvrAvatar2_GetLoadSettings();
            return ovrAvatar2Entity_LoadMemory(entityId, data, dataSize, name, defaultLoadSettings, out requestId);
        }

        public static ovrAvatar2Result OvrAvatarEntity_LoadMemoryWithFilters(
            ovrAvatar2EntityId entityId, IntPtr data, UInt32 dataSize, string name, in ovrAvatar2EntityFilters loadFilters, out ovrAvatar2LoadRequestId requestId)
        {
            var loadSettings = OvrAvatar2_GetLoadSettings();
            loadSettings.loadFilters = loadFilters;
            return ovrAvatar2Entity_LoadMemory(entityId, data, dataSize, name, loadSettings, out requestId);
        }

        /// Load assets described by a user's specification.
        /// Note: Only one user's assets may be on an entity at a time.
        /// Note: This can be called to load updated assets after ovrAvatar2_HasAvatarChanged returns true
        /// \param ovrAvatar2Entity to load into
        /// \param userID to load from
        /// \param loadSettings for this load
        /// \param (out) loadRequestID to retrieve status from ovrAvatar2Asset_GetLoadRequestInfo
        /// \return result code
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result ovrAvatar2Entity_LoadUser(
            ovrAvatar2EntityId entityId, UInt64 userId, ovrAvatar2EntityLoadSettings loadSettings, out ovrAvatar2LoadRequestId requestId);

        public static ovrAvatar2Result OvrAvatarEntity_LoadUser(ovrAvatar2EntityId entityId, UInt64 userId, out ovrAvatar2LoadRequestId requestId)
        {
            var defaultLoadSettings = OvrAvatar2_GetLoadSettings();
            return ovrAvatar2Entity_LoadUser(entityId, userId, defaultLoadSettings, out requestId);
        }

        public static ovrAvatar2Result OvrAvatarEntity_LoadUserWithFilters(ovrAvatar2EntityId entityId, UInt64 userId, in ovrAvatar2EntityFilters loadFilters, out ovrAvatar2LoadRequestId requestId)
        {
            var loadSettings = OvrAvatar2_GetLoadSettings();
            loadSettings.loadFilters = loadFilters;
            return ovrAvatar2Entity_LoadUser(entityId, userId, loadSettings, out requestId);
        }

        public static ovrAvatar2Result OvrAvatarEntity_LoadUserWithFiltersFast(ovrAvatar2EntityId entityId, UInt64 userId, in ovrAvatar2EntityFilters loadFilters, out ovrAvatar2LoadRequestId requestId)
        {
            var loadSettings = OvrAvatar2_GetFastLoadSettings();
            loadSettings.loadFilters.manifestationFlags = loadFilters.manifestationFlags;
            loadSettings.loadFilters.subMeshInclusionFlags = loadFilters.subMeshInclusionFlags;
            loadSettings.loadFilters.viewFlags = loadFilters.viewFlags;
            loadSettings.loadFilters.highQualityFlags = CAPI.ovrAvatar2EntityHighQualityFlags.None;
            return ovrAvatar2Entity_LoadUser(entityId, userId, loadSettings, out requestId);
        }

        /// Unload the default model from the entity
        /// \param ovrAvatar2Entity to unload the default model from
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result ovrAvatar2Entity_UnloadDefaultModel(ovrAvatar2EntityId entityId);

        public static bool OvrAvatar2Entity_UnloadDefaultModel(ovrAvatar2EntityId entityId)
        {
            return ovrAvatar2Entity_UnloadDefaultModel(entityId)
                .EnsureSuccess("ovrAvatar2Entity_UnloadDefaultModel");
        }

        /// Unload an asset loaded via ovrAvatar2Entity_LoadUri()
        /// \param ovrAvatar2Entity to unload the asset from
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern ovrAvatar2Result ovrAvatar2Entity_UnloadUri(ovrAvatar2EntityId entityId, string uri);
        public static bool OvrAvatar2Entity_UnloadUri(ovrAvatar2EntityId entityId, string uri)
        {
            return ovrAvatar2Entity_UnloadUri(entityId, uri)
                .EnsureSuccess("ovrAvatar2Entity_UnloadUri");
        }

        /// Unload an asset loaded via ovrAvatar2Entity_LoadMemory()
        /// \param ovrAvatar2Entity to unload the asset from
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private unsafe static extern ovrAvatar2Result ovrAvatar2Entity_UnloadMemory(ovrAvatar2EntityId entityId, /*const*/ char* name);

        /// Unload an asset loaded via ovrAvatar2Entity_LoadUser()
        /// \param ovrAvatar2Entity to unload the asset from
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result ovrAvatar2Entity_UnloadUser(ovrAvatar2EntityId entityId);

        //-----------------------------------------------------------------
        //
        // Queries
        //
        //

        /// Get the number of loaded assets on an entity
        /// \param ovrAvatar2Entity to query
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern UInt32 ovrAvatar2Entity_GetNumLoadedAssets(ovrAvatar2EntityId entityId);

        public static uint OvrAvatar2Entity_GetNumLoadedAssets(ovrAvatar2EntityId entityId)
        {
            return ovrAvatar2Entity_GetNumLoadedAssets(entityId);
        }

        [System.Serializable]
        public enum ovrAvatar2EntityAssetType : Int32
        {
            SystemDefaultModel = 0,
            SystemOther = 1,
            Other = 2,
        }

        /// Get the asset types of the loaded assets on an entity
        /// \param ovrAvatar2Entity to query
        /// \param pointer to array to populate (should be sized by ovrAvatar2Entity_GetNumLoadedAssets)
        /// \param size of the typesBuffer
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrAvatar2Result ovrAvatar2Entity_GetLoadedAssetTypes(
            ovrAvatar2EntityId entityId,
            ovrAvatar2EntityAssetType* typesBuffer,
            UInt32 bufferSize);

        internal static NativeArrayDisposeWrapper<ovrAvatar2EntityAssetType>
            OvrAvatar2Entity_GetLoadedAssetTypes_NativeArray(ovrAvatar2EntityId entityId)
        {
            uint numAssets = OvrAvatar2Entity_GetNumLoadedAssets(entityId);
            if (numAssets > 0)
            {
                var assetTypes = new NativeArray<ovrAvatar2EntityAssetType>((int)numAssets
                    , Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                try
                {
                    unsafe
                    {
                        var assetTypesPtr = (ovrAvatar2EntityAssetType*)assetTypes.GetUnsafePtr();
                        var bufferSize = assetTypes.GetEnumBufferSize(sizeof(ovrAvatar2EntityAssetType));
                        if (ovrAvatar2Entity_GetLoadedAssetTypes(entityId, assetTypesPtr, bufferSize)
                            .EnsureSuccess("ovrAvatar2Entity_GetLoadedAssetTypes"))
                        {
                            return assetTypes;
                        }
                    }
                }
                catch { }
                assetTypes.Dispose();
            }
            return default;
        }

        public static ovrAvatar2EntityAssetType[] OvrAvatar2Entity_GetLoadedAssetTypes(ovrAvatar2EntityId entityId)
        {
            using (var assetTypes
                = OvrAvatar2Entity_GetLoadedAssetTypes_NativeArray(entityId))
            {
                return assetTypes.ToArray();
            }
        }


        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetStatus(ovrAvatar2EntityId entityId);


        //-----------------------------------------------------------------
        //
        // Active
        //
        //

        /// Set whether the an entity will update its render prims.
        /// \param entity to get status of
        /// \param acitve value
        /// \return result code
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_SetActive(
            ovrAvatar2EntityId entityId,
            [MarshalAs(UnmanagedType.U1)]
            bool active);

        /// Get whether the an entity will update its render prims.
        /// \param entity to get status of
        /// \param pointer to the where the active flag should be stored
        /// \return result code
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Entity_GetActive(
            ovrAvatar2EntityId entityId,
            [MarshalAs(UnmanagedType.U1)]
            out bool isActive);

        /// Get whether the an entity will update its render prims.
        /// \param entity to get status of
        /// \param pointer to the where the active flag should be stored
        /// \return result code
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe ovrAvatar2Result ovrAvatar2Entity_GetActives(
            ovrAvatar2EntityId* entityIds,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U1)]
            bool* isActives,
            uint numIds);

        //-----------------------------------------------------------------
        //
        // Debug
        //
        //

        /// Get name for provided `nodeId`
        /// \param entity to get status of
        /// \param `ovrAvatar2NodeId` with unknown name
        /// \param `char*` output buffer for name
        /// \param length in bytes of name buffer
        /// \return result code
        ///
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern unsafe ovrAvatar2Result ovrAvatar2Entity_GetNodeName(
            ovrAvatar2EntityId entityId,
            ovrAvatar2NodeId nodeId,
            byte* nameBuffer,
            UInt32 nameBufferSize,
            out UInt32 nameLength);

        public static string OvrAvatar2Entity_GetNodeName(ovrAvatar2EntityId entityId, ovrAvatar2NodeId nodeId)
        {
            unsafe
            {
                const int bufferSize = 256;
                var nameBuffer = stackalloc byte[bufferSize];
                if (ovrAvatar2Entity_GetNodeName(entityId, nodeId, nameBuffer, bufferSize, out var nameLen)
                    .EnsureSuccess("ovrAvatar2Entity_GetNodeName"))
                {
                    return Marshal.PtrToStringAnsi((IntPtr)nameBuffer, (int)nameLen);
                }
            }

            return null;
        }

        /// Query `ovrAvatar2NodeId`s for provided `jointTypes`
        /// \param entity to get status of
        /// \param pointer to JointType values to query
        /// \param length of `jointTypes` array
        /// \param pointer to output array of `ovrAvatar2NodeId`s,
        ///     length must be greater than `jointTypeCount`
        /// \return result code
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern ovrAvatar2Result ovrAvatar2Entity_QueryJointTypeNodes(
            ovrAvatar2EntityId entityId,
            /*const*/ ovrAvatar2JointType* jointTypes,
            UInt32 jointTypeCount,
            ovrAvatar2NodeId* nodeIds);

        public static ovrAvatar2NodeId[] OvrAvatar2Entity_QueryJointTypeNodes(ovrAvatar2EntityId entityId, ovrAvatar2JointType[] jointTypes, UnityEngine.Object logContext = null)
        {
            var jointTypesLen = jointTypes.Length;
            var jointTypesHandle = GCHandle.Alloc(jointTypes, GCHandleType.Pinned);
            try
            {
                unsafe
                {
                    var jointTypesPtr = (ovrAvatar2JointType*)jointTypesHandle.AddrOfPinnedObject();
                    using var result =
                        OvrAvatar2Entity_QueryJointTypeNodes(entityId, jointTypesPtr, jointTypesLen, logContext);
                    return result.ToArray();
                }
            }
            finally
            {
                jointTypesHandle.Free();
            }
        }

        public static ovrAvatar2NodeId[] OvrAvatar2Entity_QueryJointTypeNodes(
            ovrAvatar2EntityId entityId, NativeSlice<ovrAvatar2JointType> jointTypes, UnityEngine.Object logContext = null)
        {
            var jointTypesLen = jointTypes.Length;
            unsafe
            {
                var jointTypesPtr = (ovrAvatar2JointType*)jointTypes.GetUnsafeReadOnlyPtr();
                using var result = OvrAvatar2Entity_QueryJointTypeNodes(entityId, jointTypesPtr, jointTypesLen, logContext);
                return result.ToArray();
            }
        }

        public static NativeArrayDisposeWrapper<ovrAvatar2NodeId> OvrAvatar2Entity_QueryJointTypeNodes_NativeArray(ovrAvatar2EntityId entityId, in NativeArray<ovrAvatar2JointType> jointTypes, UnityEngine.Object logContext = null)
        {
            unsafe
            {
                return OvrAvatar2Entity_QueryJointTypeNodes(entityId, jointTypes.GetPtr(), jointTypes.Length, logContext);
            }
        }

        private static unsafe NativeArrayDisposeWrapper<ovrAvatar2NodeId> OvrAvatar2Entity_QueryJointTypeNodes(
            ovrAvatar2EntityId entityId, ovrAvatar2JointType* jointTypesPtr, int jointTypesLen, UnityEngine.Object logContext = null)
        {
            var nodeIdsOutput = new NativeArray<ovrAvatar2NodeId>(jointTypesLen, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var queryResult = ovrAvatar2Entity_QueryJointTypeNodes(entityId, jointTypesPtr, (UInt32)jointTypesLen, nodeIdsOutput.GetPtr());

            // Log appropriate error/warning, if error - return null
            if (!queryResult.EnsureSuccessOrWarning(ovrAvatar2Result.LegacyJointTypeFallback
                , "enable `ovrAvatar2EntityFeatures.UseDefaultAnimHierarchy` in `OvrAvatarEntity.creationInfo.features`"
                , "ovrAvatar2Entity_QueryJointTypeNodes", entityLogScope, logContext)
                && queryResult != ovrAvatar2Result.LegacyJointTypeFallback)
            {
                nodeIdsOutput.Dispose();
                return default; // effectively, `null`
            }

            return nodeIdsOutput;
        }
    }
}
