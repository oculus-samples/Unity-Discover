#define OVR_ENABLE_VERTEX_REPACKING

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

/// @file OvrAvatarPrimitive.cs

namespace Oculus.Avatar2
{
    /**
     * Encapsulates a mesh associated with an avatar asset.
     * Asynchronously loads the mesh and its material and
     * converts it to a Unity Mesh and Material.
     * A primitive may be shared across avatar levels of detail
     * and across avatar renderables.
     * @see OvrAvatarRenderable
     */
    public sealed class OvrAvatarPrimitive : OvrAvatarAsset<CAPI.ovrAvatar2Primitive>
    {
        private const string primitiveLogScope = "ovrAvatarPrimitive";
        //:: Internal

        private const int LOD_INVALID = -1;

        /// Name of the asset this mesh belongs to.
        /// The asset name is established when the asset is loaded.
        public override string assetName => shortName;

        /// Type of asset (e.e. "OvrAvatarPrimitive", "OvrAvatarImage")
        public override string typeName => primitiveLogScope;

        /// Name of this primitive.
        public readonly string name = null;

        ///
        /// Short name of this primitive.
        /// Defaults to the asset name.
        /// @see assetName
        ///
        public readonly string shortName = null;

        /// Unity Material used by this primitive.
        public Material material { get; private set; } = null;

        /// Unity Mesh used by this primitive.
        public Mesh mesh { get; private set; } = null;

        /// True if this primitive has a computed bounding volume.
        public bool hasBounds { get; private set; }

        /// Triangle and vertex counts for this primitive.
        public ref readonly AvatarLODCostData CostData => ref _costData;

        /// Gets the GPU skinning version of this primitive.
        public OvrAvatarGpuSkinnedPrimitive gpuPrimitive { get; private set; } = null;

        public OvrAvatarComputeSkinnedPrimitive computePrimitive { get; private set; }
#pragma warning disable CA2213 // Disposable fields should be disposed - it is, but the linter is confused
        private OvrAvatarGpuSkinnedPrimitiveBuilder gpuPrimitiveBuilder = null;
#pragma warning restore CA2213 // Disposable fields should be disposed

        ///
        /// Index of highest quality level of detail this primitive belongs to.
        /// One primitive may be used by more than one level of detail.
        /// This is the lowest set bit in @ref CAPI.ovrAvatar2EntityLODFlags provided from native SDK.
        ///
        public uint HighestQualityLODIndex => (uint)lod;

        ///
        /// LOD bit flags for this primitive.
        /// These flags indicate which levels of detail this primitive is used by.
        /// @see HighestQualityLODIndex
        ///
        public CAPI.ovrAvatar2EntityLODFlags lodFlags { get; private set; }

        ///
        /// Type of shader being used by this primitive.
        /// The shader type depends on what part of the avatar is being shaded.
        ///
        public OvrAvatarShaderManagerMultiple.ShaderType shaderType { get; private set; }

        private OvrAvatarShaderConfiguration _shaderConfig;

        // MeshInfo, only tracked for cleanup on cancellation
        private MeshInfo _meshInfo;

        // NOTE: Once this is initialized, it should not be "reset" even if the Primitive is disposed
        // Other systems may need to reference this data during shutdown, and it's a PITA if they each have to make copies
        private AvatarLODCostData _costData = default;

        // TODO: A primitive can technically belong to any number of LODs with gaps in between.
        private int lod = LOD_INVALID;

        // TODO: Make this debug only
        public Int32[] joints;

        ///
        /// Get which body parts of the avatar this primitive is used by.
        /// These are established when the primitive is loaded.
        ///
        public CAPI.ovrAvatar2EntityManifestationFlags manifestationFlags { get; private set; }

        ///
        /// Get which view(s) (first person, third person) this primitive applies to.
        /// These are established when the primitive is loaded.
        ///
        public CAPI.ovrAvatar2EntityViewFlags viewFlags { get; private set; }

        ///
        /// If the user wants only a subset of the mesh, as specified by
        /// indices, these flags will control which submeshes are included.
        /// NOTE: In the current implementation all verts are downloaded,
        /// but the indices referencing them are excluded.
        ///
        public CAPI.ovrAvatar2EntitySubMeshInclusionFlags subMeshInclusionFlags { get; private set; }

        ///
        /// If the user wants to load and render advanced features, they can specify them in the
        /// high quality flags.
        ///
        public CAPI.ovrAvatar2EntityHighQualityFlags highQualityFlags { get; private set; }

        /// True if this primitive has joints (is skinned).
        public bool HasJoints => JointCount > 0;

        /// True if this primitive has blend shapes (morph targets).
        public bool HasMorphs => morphTargetCount > 0;

        /// Number of joints affecting this primitive.
        public UInt32 JointCount => joints != null ? (uint)joints.Length : 0;

        /// Number of vertices in this primitive's mesh.
        public UInt32 meshVertexCount => _meshVertexCount;

        /// Number of vertices affected by morph targets.
        // TODO: Accurate count of vertices affected by morph targets
        // Assumes that if there are morph targets, all verts are affected by morphs
        public UInt32 morphVertexCount => HasMorphs ? meshVertexCount : 0;

        /// Number of triangles in this primitive.
        public UInt32 triCount { get; private set; }

        /// Number of morph targets affecting this primitive.
        public UInt32 morphTargetCount => _morphTargetCount;

        /// True if this primitive has tangents for each vertex.
        public bool hasTangents { get; private set; }

        private UInt32 bufferVertexCount => _bufferVertexCount;

        /// True if this primitive has finished loading.
        public override bool isLoaded
        {
            get => base.isLoaded && meshLoaded && materialLoaded && gpuSkinningLoaded;
        }

        // Indicates that this Primitive no longer needs access to CAPI asset data and the resource can be released
        internal bool hasCopiedAllResourceData => !(_needsMeshData || _needsMorphData || _needsImageData);

        // Vertex count for the entire asset buffer, may include data for multiple primitives
        private UInt32 _bufferVertexCount = UInt32.MaxValue;

        // Vertex count for this mesh's primitive
        private UInt32 _meshVertexCount = UInt32.MaxValue;
        private UInt32 _morphTargetCount = UInt32.MaxValue;

        // Task thread completion checks
        private bool meshLoaded = false;
        private bool materialLoaded = false;
        private bool gpuSkinningLoaded = false;

        // Resource copy status
        private bool _needsMeshData = true;
        private bool _needsMorphData = true;
        private bool _needsImageData = true;

        // TODO: Remove via better state management
        private bool _hasCancelled = false;

        // Cancellation token for Tasks
#pragma warning disable CA2213 // Disposable fields should be disposed -> It is, but the linter is confused
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
#pragma warning restore CA2213 // Disposable fields should be disposed

        // Async load coroutines for cancellation
        private OvrTime.SliceHandle _loadMeshAsyncSliceHandle;
        private OvrTime.SliceHandle _loadMaterialAsyncSliceHandle;

        [Flags]
        private enum VertexFormat : uint
        {
            VF_POSITION = 1,
            VF_NORMAL = 2,
            VF_TANGENT = 4,
            VF_COLOR = 8,
            VF_TEXCOORD0 = 16,
            VF_COLOR_ORMT = 32,
            VF_BONE_WEIGHTS = 64,
            VF_BONE_INDICES = 128,
        }
        // This isn't really necessary since '0' is the default. The reason to include it is to make it clear which
        // stream is being used and where to start making changes to support multiple streams.
        private const int VF_STREAM_ID = 0;

        private struct VertexBuffer
        {
            public VertexFormat vertexFormat;
            public int vertexCount;
            public int vertexStride;
            public List<VertexAttributeDescriptor> vertexLayout;

            public NativeArray<byte> vertices;
        }

        // Data shared across threads
        private sealed class MeshInfo : IDisposable
        {
            // Core Mesh
            public NativeArray<Color> colors;
            public NativeArray<Color> colorsORMT;
            public NativeArray<float> subMeshTypes;

            public NativeArray<Color32> meshColors;

            public NativeArray<UInt16> triangles;

            private NativeArray<Vector3> verts_;

            // New vertex format.
            public VertexBuffer vertexBuffer;

            // NOTE: Held during GPUPrimitiveBuilding
            public NativeArray<Vector3> verts
            {
                get => verts_;
                set
                {
                    verts_ = value;
                    pendingMeshVerts_ = true;
                    vertexCount = value.IsCreated ? (uint)value.Length : 0U;
                }
            }

            // NOTE: Held during GPUPrimitiveBuilding
            private NativeArray<Vector3> normals_;

            public NativeArray<Vector3> normals
            {
                get => normals_;
                set
                {
                    normals_ = value;
                    pendingMeshNormals_ = true;
                }
            }

            // NOTE: Held during GPUPrimitiveBuilding
            private NativeArray<Vector4> tangents_;

            public NativeArray<Vector4> tangents
            {
                get => tangents_;
                set
                {
                    tangents_ = value;
                    pendingMeshTangents_ = true;
                    hasTangents = value.IsCreated && value.Length > 0;
                }
            }

            public NativeArray<Vector2> texCoords;

            // Documentation for `SetBoneWeights(NativeArray)` is... lacking
            // - https://docs.unity3d.com/ScriptReference/Mesh.SetBoneWeights.html
            private BoneWeight[] boneWeights_;

            public BoneWeight[] boneWeights
            {
                get => boneWeights_;
                set
                {
                    boneWeights_ = value;
                    pendingMeshBoneWeights_ = true;
                }
            }

            // Skin
            // As of 2020.3, no NativeArray bindPoses setter
            public Matrix4x4[] bindPoses;

            // Track vertex count after verts has been freed
            public uint vertexCount { get; private set; }
            public bool hasTangents { get; private set; }

            public void WillBuildGpuPrimitive()
            {
                pendingGpuPrimitive_ = true;
                pendingNeutralPoseTex_ = vertexCount > 0;
            }

            public void WillBuildComputePrimitive()
            {
                _pendingComputePrimitive = true;
                _pendingNeutralPoseBuffers = vertexCount > 0;
            }

            public void DidBuildGpuPrimitive()
            {
                pendingGpuPrimitive_ = false;
                if (CanResetVerts) { verts_.Reset(); }
                if (CanResetNormals) { normals_.Reset(); }
                if (CanResetTangents) { tangents_.Reset(); }
                if (CanResetBoneWeights) { boneWeights = null; }
            }

            public void DidBuildComputePrimitive()
            {
                _pendingComputePrimitive = false;
                if (CanResetVerts) { verts_.Reset(); }
                if (CanResetNormals) { normals_.Reset(); }
                if (CanResetTangents) { tangents_.Reset(); }
                if (CanResetBoneWeights) { boneWeights = null; }
            }

            public void NeutralPoseTexComplete()
            {
                pendingNeutralPoseTex_ = false;
                if (CanResetVerts) { verts_.Reset(); }
                if (CanResetNormals) { normals_.Reset(); }
                if (CanResetTangents) { tangents_.Reset(); }
            }

            public void NeutralPoseBuffersComplete()
            {
                _pendingNeutralPoseBuffers = false;
                if (CanResetVerts) { verts_.Reset(); }
                if (CanResetNormals) { normals_.Reset(); }
                if (CanResetTangents) { tangents_.Reset(); }
            }

            public void CancelledBuildPrimitives()
            {
                DidBuildGpuPrimitive();
                DidBuildComputePrimitive();
                NeutralPoseTexComplete();
                NeutralPoseBuffersComplete();
            }

            public void MeshVertsComplete()
            {
                pendingMeshVerts_ = false;
                if (CanResetVerts) { verts_.Reset(); }
            }

            public void MeshNormalsComplete()
            {
                pendingMeshNormals_ = false;
                if (CanResetNormals) { normals_.Reset(); }
            }

            public void MeshTangentsComplete()
            {
                pendingMeshTangents_ = false;
                if (CanResetTangents) { tangents_.Reset(); }
            }

            public void MeshBoneWeightsComplete()
            {
                pendingMeshBoneWeights_ = false;
                if (CanResetBoneWeights) { boneWeights = null; }
            }

            public bool HasPendingPrimitives => pendingGpuPrimitive_ || _pendingComputePrimitive;
            private bool HasPendingNeutralPoses => pendingNeutralPoseTex_ || _pendingNeutralPoseBuffers;

            private bool CanResetVerts => !HasPendingPrimitives && !HasPendingNeutralPoses && !pendingMeshVerts_;
            private bool CanResetNormals => !HasPendingPrimitives && !HasPendingNeutralPoses && !pendingMeshNormals_;
            private bool CanResetTangents => !HasPendingPrimitives && !HasPendingNeutralPoses && !pendingMeshTangents_;
            private bool CanResetBoneWeights => !HasPendingPrimitives && !pendingMeshBoneWeights_;

            private bool pendingMeshVerts_ = false;
            private bool pendingMeshTangents_ = false;
            private bool pendingMeshNormals_ = false;
            private bool pendingMeshBoneWeights_ = false;

            private bool pendingGpuPrimitive_ = false;

            private bool pendingNeutralPoseTex_ = false;

            private bool _pendingComputePrimitive = false;
            private bool _pendingNeutralPoseBuffers = false;

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool isDispose)
            {
                boneWeights = null;
                bindPoses = null;

                triangles.Reset();

                verts_.Reset();
                normals_.Reset();
                tangents_.Reset();
                texCoords.Reset();

                colors.Reset();
                colorsORMT.Reset();
                subMeshTypes.Reset();

                OvrAvatarLog.Assert(isDispose, primitiveLogScope);
            }

            ~MeshInfo()
            {
                OvrAvatarLog.LogError("Finalized MeshInfo", primitiveLogScope);
                Dispose(false);
            }
        }

        private class MaterialInfo
        {
            public CAPI.ovrAvatar2MaterialTexture[] texturesData = null;
            public CAPI.ovrAvatar2Image[] imageData = null;
            public bool hasMetallic = false;
        }

        // TODO: Look into readonly struct, this doesn't appear to be shared across threads
        private struct MorphTargetInfo
        {
            public readonly string name;

            // TODO: Maybe make these NativeArrays too?
            public readonly Vector3[] targetPositions;
            public readonly Vector3[] targetNormals;
            public readonly Vector3[] targetTangents;

            public MorphTargetInfo(string nameIn, Vector3[] posIn, Vector3[] normIn, Vector3[] tanIn)
            {
                this.name = nameIn;
                this.targetPositions = posIn;
                this.targetNormals = normIn;
                this.targetTangents = tanIn;
            }
        }

        internal OvrAvatarPrimitive(OvrAvatarResourceLoader loader, in CAPI.ovrAvatar2Primitive primitive) : base(
            primitive.id, primitive)
        {
            // TODO: Can we defer this until later as well?
            mesh = new Mesh();

            // Name

            unsafe
            {
                const int bufferSize = 1024;
                byte* nameBuffer = stackalloc byte[bufferSize];
                var result = CAPI.ovrAvatar2Asset_GetPrimitiveName(assetId, nameBuffer, bufferSize);
                if (result.IsSuccess())
                {
                    string meshPrimitiveName = Marshal.PtrToStringAnsi((IntPtr)nameBuffer);
                    if (!string.IsNullOrEmpty(meshPrimitiveName)) { name = meshPrimitiveName; }
                }
                else { OvrAvatarLog.LogWarning($"GetPrimitiveName {result}", primitiveLogScope); }
            }

            if (name == null) { name = "Mesh" + primitive.id; }

            mesh.name = name;
            shortName = name.Replace("Primitive", "p");
        }

        // Must *not* be called more than once
        private bool _startedLoad = false;

        internal void StartLoad(OvrAvatarResourceLoader loader)
        {
            Debug.Assert(!_startedLoad);
            Debug.Assert(!_loadMeshAsyncSliceHandle.IsValid);
            Debug.Assert(!_loadMaterialAsyncSliceHandle.IsValid);

            _startedLoad = true;

            var vertCountResult =
                CAPI.ovrAvatar2VertexBuffer_GetVertexCount(data.vertexBufferId, out _bufferVertexCount);
            if (!vertCountResult.EnsureSuccess("ovrAvatar2VertexBuffer_GetVertexCount", primitiveLogScope))
            {
                _bufferVertexCount = 0;
                _needsMeshData = false;
            }

            var morphResult = CAPI.ovrAvatar2Result.Unknown;
            //primitives might not have a morph target
            if (data.morphTargetBufferId != CAPI.ovrAvatar2MorphTargetBufferId.Invalid)
            {
                morphResult =
                    CAPI.ovrAvatar2VertexBuffer_GetMorphTargetCount(data.morphTargetBufferId, out _morphTargetCount);
                //but if they do, then getting the ocunt shouldn't fail
                morphResult.EnsureSuccess("ovrAvatar2VertexBuffer_GetMorphTargetCount", primitiveLogScope);
            }

            if (morphResult.IsFailure())
            {
                _morphTargetCount = 0;
                _needsMorphData = false;
            }

            _loadMeshAsyncSliceHandle = OvrTime.Slice(LoadMeshAsync());
            _loadMaterialAsyncSliceHandle = OvrTime.Slice(LoadMaterialAsync(loader, loader.resourceId));

            // there are additional flags which will be set before publicly reporting `isLoaded`
            base.isLoaded = true;
        }

        private bool _CanCleanupCancellationToken =>
            !_loadMeshAsyncSliceHandle.IsValid && !_loadMaterialAsyncSliceHandle.IsValid;

        private void _TryCleanupCancellationToken()
        {
            if (!_CanCleanupCancellationToken)
            {
                // Cancellation called while timesliced operations in progress
                return;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private bool AreAllTasksCancelled()
        {
            bool allCancelled = true;

            for (int idx = 0; idx < _apiTasks.Length; ++idx)
            {
                ref Task task = ref _apiTasks[idx];
                if (task != null)
                {
                    if (task.IsCompleted)
                    {
                        task.Dispose();
                        task = null;
                    }
                    else
                    {
                        OvrAvatarLog.LogDebug(
                            $"Cancelled Task {task} is still running",
                            primitiveLogScope);
                        allCancelled = false;
                    }
                }
            }

            if (allCancelled && _apiTasks.Length > 0) { _apiTasks = Array.Empty<Task>(); }

            if (_texturesDataTask != null)
            {
                if (_texturesDataTask.IsCompleted)
                {
                    _texturesDataTask.Dispose();
                    _texturesDataTask = null;
                }
                else
                {
                    OvrAvatarLog.LogError(
                        $"Cancelled Task {_texturesDataTask} is still running",
                        primitiveLogScope);
                    allCancelled = false;
                }
            }

            return allCancelled;
        }

        protected override void _ExecuteCancel()
        {
            OvrAvatarLog.Assert(!_hasCancelled);
            // TODO: Remove this check, this should not be possible
            if (_hasCancelled)
            {
                OvrAvatarLog.LogError($"Double cancelled primitive {name}", primitiveLogScope);
                return;
            }

            // TODO: We can probably skip all of this if cancellation token is null
            _cancellationTokenSource?.Cancel();

            if (_loadMeshAsyncSliceHandle.IsValid)
            {
                OvrAvatarLog.LogDebug($"Stopping LoadMeshAsync slice {shortName}", primitiveLogScope);
                bool didCancel = _loadMeshAsyncSliceHandle.Cancel();
                OvrAvatarLog.Assert(didCancel, primitiveLogScope);
            }

            if (_loadMaterialAsyncSliceHandle.IsValid)
            {
                OvrAvatarLog.LogDebug($"Stopping LoadMaterialAsync slice {shortName}", primitiveLogScope);
                bool didCancel = _loadMaterialAsyncSliceHandle.Cancel();
                OvrAvatarLog.Assert(didCancel, primitiveLogScope);
            }

            if (AreAllTasksCancelled())
            {
                _FinishCancel();
            }
            else
            {
                OvrTime.Slice(_WaitForCancellation());
            }

            _hasCancelled = true;
        }

        private IEnumerator<OvrTime.SliceStep> _WaitForCancellation()
        {
            // Wait for all tasks to complete before proceeding with cleanup
            while (!AreAllTasksCancelled()) { yield return OvrTime.SliceStep.Delay; }

            // Finish cancellation, Dispose of Tasks and Tokens
            _FinishCancel();

            // Ensure any misc assets created during cancellation window are properly disposed
            Dispose(true);
        }

        private void _FinishCancel()
        {
            if (gpuPrimitiveBuilder != null)
            {
                OvrAvatarLog.LogDebug($"Stopping gpuPrimitiveBuilder {shortName}", primitiveLogScope);

                gpuPrimitiveBuilder.Dispose();
                gpuPrimitiveBuilder = null;
            }

            _needsImageData = _needsMeshData = _needsMorphData = false;
            _TryCleanupCancellationToken();
        }

        protected override void Dispose(bool disposing)
        {
            _loadMeshAsyncSliceHandle.Clear();
            _loadMaterialAsyncSliceHandle.Clear();

            if (!(mesh is null))
            {
                if (disposing) { Mesh.Destroy(mesh); }
                else
                {
                    OvrAvatarLog.LogError(
                        $"Mesh asset was not destroyed before OvrAvatarPrimitive ({name}, {assetId}) was finalized",
                        primitiveLogScope);
                }

                mesh = null;
            }

            if (!(material is null))
            {
                if (disposing) { Material.Destroy(material); }
                else
                {
                    OvrAvatarLog.LogError(
                        $"Material asset was not destroyed before OvrAvatarPrimitive ({name}, {assetId}) was finalized",
                        primitiveLogScope);
                }

                material = null;
            }

            if (!(gpuPrimitive is null))
            {
                if (disposing) { gpuPrimitive.Dispose(); }
                else
                {
                    OvrAvatarLog.LogError(
                        $"OvrAvatarGPUSkinnedPrimitive asset was not destroyed before OvrAvatarPrimitive ({name}, {assetId}) was finalized"
                        ,
                        primitiveLogScope);
                }

                gpuPrimitive = null;
            }

            if (!(computePrimitive is null))
            {
                if (disposing) { computePrimitive.Dispose(); }
                else
                {
                    OvrAvatarLog.LogError(
                        $"OvrAvatarComputeSkinnedPrimitive asset was not destroyed before OvrAvatarPrimitive ({name}, {assetId}) was finalized"
                        ,
                        primitiveLogScope);
                }

                computePrimitive = null;
            }

            _meshInfo?.Dispose();

            joints = null;
            _shaderConfig = null;

            meshLoaded = false;
            materialLoaded = false;
        }

        //:: Main Thread Loading

        #region Main Thread Loading

        private Task[] _apiTasks = Array.Empty<Task>();

        private IEnumerator<OvrTime.SliceStep> LoadMeshAsync()
        {
            GetLodInfo();
            GetManifestationInfo();
            GetViewInfo();
            GetSubMeshInclusionInfo();
            GetHighQualityInfo();

            // load triangles
            // load mesh & morph targets
            // create unity mesh and/or gpu skinning resources

            var meshLodInfo = new MeshLodInfo();

            var gpuSkinning = OvrAvatarManager.Instance.OvrGPUSkinnerSupported;
            var computeSkinning = OvrAvatarManager.Instance.OvrComputeSkinnerSupported;
            var setupSkin = data.jointCount > 0;

            _meshInfo = new MeshInfo();
            var morphTargetInfo = new MorphTargetInfo[morphTargetCount];
            const int alwaysPerformedTasks = 2;
            var taskCount = alwaysPerformedTasks + (setupSkin ? 1 : 0);
            var tasks = new Task[taskCount];
            // for now, all tasks are "CAPI" tasks
            _apiTasks = tasks;

            tasks[0] = Task.Run(() => { RetrieveTriangles(meshLodInfo, _meshInfo); });
            if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

            var tasksAfterTriangles = new Task[morphTargetCount > 0 ? 2 : 1];
            tasksAfterTriangles[0] =
                tasks[0].ContinueWith(antecedent => RetrieveMeshData(_meshInfo, in meshLodInfo.repacker));
            if (morphTargetCount > 0)
            {
                tasksAfterTriangles[1] = tasks[0].ContinueWith(
                    antecedent =>
                        SetupMorphTargets(morphTargetInfo, in meshLodInfo.repacker));
            }

            tasks[1] = Task.WhenAll(tasksAfterTriangles);
            if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

            if (setupSkin)
            {
                tasks[2] = Task.Run(() => SetupSkin(ref _meshInfo));
            }
            else
            {
                joints = Array.Empty<int>();
            }

            if (gpuSkinning || computeSkinning)
            {
                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                gpuPrimitiveBuilder = new OvrAvatarGpuSkinnedPrimitiveBuilder(shortName, morphTargetCount);
            }
            else
            {
                // Don't need to wait for gpu skinning
                gpuSkinningLoaded = true;
            }

            if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

            CAPI.ovrAvatar2Vector3f minPos;
            CAPI.ovrAvatar2Vector3f maxPos;
            CAPI.ovrAvatar2Result result;
            if (setupSkin)
            {
                result = CAPI.ovrAvatar2Primitive_GetSkinnedMinMaxPosition(data.id, out minPos, out maxPos);
            }
            else
            {
                result = CAPI.ovrAvatar2Primitive_GetMinMaxPosition(data.id, out minPos, out maxPos);
            }

            hasBounds = false;
            Bounds? sdkBounds = null;
            if (result.IsSuccess())
            {
                Vector3 unityMin = minPos;
                Vector3 unityMax = maxPos;
                sdkBounds = new Bounds(Vector3.zero, unityMax - unityMin);
                hasBounds = true;
            }

            if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

            while (!AllTasksFinished(tasks))
            {
                if (AnyTasksFaulted(tasks))
                {
                    // Allow Slicer to cancel before CancelLoad is called or we will cancel during slice!
                    OvrAvatarLog.LogError("Task fault detected! Disposing resource.", primitiveLogScope);
                    OvrTime.PostCleanupToUnityMainThread(Dispose);
                    yield return OvrTime.SliceStep.Cancel;
                }

                yield return OvrTime.SliceStep.Delay;
            }

            _needsMeshData = false;
            _needsMorphData = false;

            if (AllTasksSucceeded(tasks))
            {
                _apiTasks = Array.Empty<Task>();

                hasTangents = _meshInfo.hasTangents;

                // TODO: Better way to setup this dependency, we need all preprocessing completed to build GPU resources though :/
                if (gpuPrimitiveBuilder != null)
                {
                    Array.Resize(ref tasks, 1);
                    tasks[0] = gpuPrimitiveBuilder.CreateSkinningPrimitivesHelperTask(
                        _meshInfo,
                        morphTargetInfo,
                        hasTangents,
                        gpuSkinning,
                        computeSkinning);

                    if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }
                }
                else
                {
                    tasks = Array.Empty<Task>();
                }

                // TODO: It would be ideal to pull this directly from nativeSDK - requires LOD buffer split
                _meshVertexCount = _meshInfo.vertexCount;

                // Apply mesh info on main thread
                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                // Create a vertex buffer using the format and stride.
                CreateVertexBuffer(_meshInfo);

                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                // Set the mesh vertex buffer parameters from the vertex buffer created above.
                // Note: final vertex buffer data is not set until the finalized below.
                var vertexBuffer = _meshInfo.vertexBuffer;
                mesh.SetVertexBufferParams(vertexBuffer.vertexCount, vertexBuffer.vertexLayout.ToArray());

                // if we extracted colors or submesh types from the model put them into the final mesh
                // pack the submesh type into the vertex color alpha because no parts of the avatars use alpha
                if (_meshInfo.colors.Length > 0 || _meshInfo.subMeshTypes.Length > 0)
                {
                    if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                    // TODO: Move into Task, `AssembleMeshColors` can all be done async
                    AssembleMeshColors(_meshInfo);
                }

                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }
                StripExcludedSubMeshes(ref _meshInfo.triangles);

                // get number of submeshes
                // foreach submesh, check to see if it is included
                // if it is not, then remove this range from the index buffer

                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }
                mesh.SetIndices(_meshInfo.triangles, MeshTopology.Triangles, 0, !hasBounds, 0);
                _meshInfo.triangles.Reset();

                // When UnitySMR is supported, include extra animation data
                if (OvrAvatarManager.Instance.UnitySMRSupported)
                {
                    if (setupSkin)
                    {
                        if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                        mesh.bindposes = _meshInfo.bindPoses;
                        _meshInfo.bindPoses = null;
                    }

                    foreach (var morphTarget in morphTargetInfo)
                    {
                        if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                        mesh.AddBlendShapeFrame(
                            morphTarget.name,
                            1,
                            morphTarget.targetPositions,
                            morphTarget.targetNormals,
                            morphTarget.targetTangents);
                    }
                }

                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                // Vertex buffer data.
                // Copy the vertex data into the vertex buffer array.
                CopyMeshDataIntoVertexBuffer(_meshInfo);
                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                // Call the mesh loaded callback before the native arrays are reset.
                InvokeOnMeshLoaded(mesh, _meshInfo);
                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                // Resets, these were peppered through the code above, but we needed the data to persist until the
                // copy above was called.
                _meshInfo.MeshVertsComplete();
                _meshInfo.meshColors.Reset();
                _meshInfo.texCoords.Reset();
                _meshInfo.colorsORMT.Reset();
                _meshInfo.MeshTangentsComplete();
                _meshInfo.MeshNormalsComplete();
                _meshInfo.MeshBoneWeightsComplete();

                // Upload vertex data to the mesh.
                mesh.SetVertexBufferData(vertexBuffer.vertices, 0, 0,
                    vertexBuffer.vertexCount * vertexBuffer.vertexStride, VF_STREAM_ID);
                mesh.UploadMeshData(true);
                // Mark the CPU vertex data as disposable.
                DisposeVertexBuffer(_meshInfo);

                // It seems that almost every vert data assignment will recalculate (and override) bounds - excellent engine...
                // So, we must delay this to the very end for no logical reason
                if (sdkBounds.HasValue)
                {
                    if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                    mesh.bounds = sdkBounds.Value;
                }

                if (gpuPrimitiveBuilder != null)
                {
                    if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                    // TODO: This is not ideal timing for this operation, but it does minimize disruption in this file which is key right now 3/3/2021
                    // - As of now, there really isn't any meaningful work that can be done off the main thread - pending D26787881
                    while (!AllTasksFinished(tasks)) { yield return OvrTime.SliceStep.Delay; }

                    // Main thread operations (currently almost all of it), sliced as best possible
                    if (gpuSkinning)
                    {
                        Profiler.BeginSample("Build GPUPrimitive");
                        gpuPrimitive = gpuPrimitiveBuilder.BuildPrimitive(_meshInfo, joints, hasTangents);
                        Profiler.EndSample();
                    }

                    if (computeSkinning)
                    {
                        Profiler.BeginSample("Build ComputePrimitive");
                        computePrimitive = gpuPrimitiveBuilder.BuildComputePrimitive(_meshInfo, hasTangents);
                        Profiler.EndSample();
                    }

                    while (gpuPrimitive != null && gpuPrimitive.IsLoading)
                    {
                        yield return OvrTime.SliceStep.Delay;
                    }

                    while (computePrimitive != null && computePrimitive.IsLoading)
                    {
                        yield return OvrTime.SliceStep.Delay;
                    }

                    if (gpuPrimitive != null && gpuPrimitive.MetaData.NumMorphTargetAffectedVerts == 0)
                    {
                        _morphTargetCount = 0;
                    }
                    else if (computePrimitive != null && computePrimitive.SourceMetaData.numMorphedVerts == 0)
                    {
                        _morphTargetCount = 0;
                    }

                    gpuSkinningLoaded = true;

                    gpuPrimitiveBuilder.Dispose();
                    gpuPrimitiveBuilder = null;
                }

                _costData = new AvatarLODCostData(this);
                meshLoaded = true;
            }
            else if (isCancelled)
            {
                // Ignore cancellation related exceptions
                OvrAvatarLog.LogDebug($"LoadMeshAsync was cancelled", primitiveLogScope);
            }
            else
            {
                // Log errors from Tasks
                foreach (var task in tasks)
                {
                    if (task.Status == TaskStatus.Faulted) { LogTaskErrors(task); }
                }
            }

            _loadMeshAsyncSliceHandle.Clear();
            _TryCleanupCancellationToken();
        }

        private Task _texturesDataTask = null;

        private IEnumerator<OvrTime.SliceStep> LoadMaterialAsync(OvrAvatarResourceLoader loader,
            CAPI.ovrAvatar2Id resourceId)
        {
            // Info to pass between threads
            var materialInfo = new MaterialInfo();

            // Marshal texture data on separate thread
            _texturesDataTask = Task.Run(() => Material_GetTexturesData(resourceId, ref materialInfo));
            while (WaitForTask(_texturesDataTask, out var step))
            {
                yield return step;
            }

            _texturesDataTask = null;

            // The rest, unfortunately, must be on the main thread
            _needsImageData = !(materialInfo.texturesData is null) && !(materialInfo.imageData is null);
            if (_needsImageData)
            {
                // Check for images needed by this material. Request image loads on main thread and wait for them.
                uint numImages = (uint)materialInfo.imageData.Length;
                var images = new OvrAvatarImage[(int)numImages];

                for (uint imgIdx = 0; imgIdx < numImages; ++imgIdx)
                {
                    if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                    FindTexture(loader, materialInfo, images, imgIdx, resourceId);
                }

                _needsImageData = false;

                // Image load wait loop.

                // Wait until all images are fully loaded
                foreach (var image in images)
                {
                    if (image == null) { continue; }

                    while (!image.isLoaded)
                    {
                        if (!image.isCancelled)
                        {
                            // Loading in progress, delay next slice
                            yield return OvrTime.SliceStep.Delay;
                        }
                        else // isCancelled
                        {
                            OvrAvatarLog.LogVerbose(
                                $"Image {image} cancelled during resource load.",
                                primitiveLogScope);

                            // Resume checking next frame
                            // TODO: Switch to Wait, but currently no unit test - use Delay for now
                            // yield return OvrTime.SliceStep.Wait;
                            yield return OvrTime.SliceStep.Delay;

                            break; // move to next images
                        }
                    }
                }
            }

            if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

            // Configure shader manager and create material
            if (OvrAvatarManager.Instance == null || OvrAvatarManager.Instance.ShaderManager == null)
            {
                OvrAvatarLog.LogError(
                    $"ShaderManager must be initialized so that a shader can be specified to generate Avatar primitive materials.");
            }
            else
            {
                bool hasTextures = materialInfo.texturesData != null && materialInfo.texturesData.Length > 0;
                shaderType =
                    OvrAvatarManager.Instance.ShaderManager.DetermineConfiguration(
                        name, materialInfo.hasMetallic,
                        false, hasTextures);
                _shaderConfig = OvrAvatarManager.Instance.ShaderManager.GetConfiguration(shaderType);
            }

            if (_shaderConfig == null)
            {
                OvrAvatarLog.LogError($"Could not find config for shaderType {shaderType}", primitiveLogScope);
                yield break;
            }

            if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

            if (_shaderConfig.Material != null) { material = new Material(_shaderConfig.Material); }
            else
            {
                var shader = _shaderConfig.Shader;

                if (shader == null)
                {
                    OvrAvatarLog.LogError($"Could not find shader for shaderType {shaderType}", primitiveLogScope);
                    yield break;
                }

                material = new Material(shader);
            }

            material.name = name;

            // Create and apply textures
            foreach (var textureData in materialInfo.texturesData)
            {
                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

                // Find corresponding image
                if (OvrAvatarManager.GetOvrAvatarAsset(textureData.imageId, out OvrAvatarImage image))
                {
                    ApplyTexture(image.texture, textureData);
                }
                else
                {
                    OvrAvatarLog.LogError($"Could not find image {textureData.imageId}", primitiveLogScope);
                }
            }

            // TODO: Should this happen before applying textures?
            if (material != null)
            {
                // Finalize dynamically created material
                _shaderConfig.ApplyKeywords(material);
                _shaderConfig.ApplyFloatConstants(material);

                bool enableNormalMap = (highQualityFlags & CAPI.ovrAvatar2EntityHighQualityFlags.NormalMaps) != 0;
                bool enablePropertyHairMap = (highQualityFlags & CAPI.ovrAvatar2EntityHighQualityFlags.PropertyHairMap) != 0;
                if (enableNormalMap)
                {
                    material.EnableKeyword("HAS_NORMAL_MAP_ON");
                    material.SetFloat("HAS_NORMAL_MAP", 1.0f);
                }
                else
                {
                    material.DisableKeyword("HAS_NORMAL_MAP_ON");
                    material.SetFloat("HAS_NORMAL_MAP", 0.0f);
                }

                if (enablePropertyHairMap)
                {
                    material.EnableKeyword("ENABLE_HAIR_ON");
                    material.SetFloat("ENABLE_HAIR", 1.0f);
                }
                else
                {
                    material.DisableKeyword("ENABLE_HAIR_ON");
                    material.SetFloat("ENABLE_HAIR", 0.0f);
                }
            }

            LoadAndApplyExtensions();

            materialLoaded = true;
            _loadMaterialAsyncSliceHandle.Clear();
            _TryCleanupCancellationToken();
        }

        private bool LoadAndApplyExtensions()
        {
            var result = CAPI.ovrAvatar2Primitive_GetNumMaterialExtensions(assetId, out uint numExtensions);
            if (!result.IsSuccess())
            {
                OvrAvatarLog.LogError(
                    $"GetNumMaterialExtensions assetId:{assetId}, result:{result}"
                    , primitiveLogScope);
                return false;
            }

            bool success = true;
            for (uint extensionIdx = 0; extensionIdx < numExtensions; extensionIdx++)
            {
                if (OvrAvatarMaterialExtension.LoadExtension(
                        assetId,
                        extensionIdx,
                        out var extension))
                {
                    extension.ApplyEntriesToMaterial(material, _shaderConfig.ExtensionConfiguration);
                }
                else
                {
                    OvrAvatarLog.LogWarning(
                        $"Unable to load material extension at index {extensionIdx} for assetId:{assetId}",
                        primitiveLogScope);
                    success = false;
                }
            }

            return success;
        }

        private bool WaitForTask(Task task, out OvrTime.SliceStep step)
        {
            // TODO: isCancelled should be mostly unnecessary here.... mostly.
            if (isCancelled || task.Status == TaskStatus.Faulted)
            {
                step = OvrTime.SliceStep.Cancel;
                LogTaskErrors(task);
                return true;
            }

            if (!task.IsCompleted)
            {
                step = OvrTime.SliceStep.Delay;
                return true;
            }

            step = OvrTime.SliceStep.Continue;
            return false;
        }

        private bool AllTasksFinished(Task[] tasks)
        {
            foreach (Task task in tasks)
            {
                if (!task.IsCompleted) return false;
            }

            return true;
        }

        private bool AllTasksSucceeded(Task[] tasks)
        {
            foreach (Task task in tasks)
            {
                if (!task.IsCompleted || task.Status == TaskStatus.Faulted) return false;
            }

            return true;
        }

        private bool AnyTasksFaulted(Task[] tasks)
        {
            foreach (Task task in tasks)
            {
                if (task.Status == TaskStatus.Faulted) { return true; }
            }

            return false;
        }

        private void LogTaskErrors(Task task)
        {
            foreach (var e in task.Exception.InnerExceptions)
            {
                OvrAvatarLog.LogError($"{e.Message}\n{e.StackTrace}", primitiveLogScope);
            }
        }

        // Helper method for matching imageData to textureData, allows use of local references
        private void FindTexture(OvrAvatarResourceLoader loader, MaterialInfo materialInfo, OvrAvatarImage[] images,
            uint imageIndex, CAPI.ovrAvatar2Id resourceId)
        {
            ref readonly var imageData = ref materialInfo.imageData[imageIndex];

            for (var texIdx = 0; texIdx < materialInfo.texturesData.Length; ++texIdx)
            {
                ref readonly var textureData = ref materialInfo.texturesData[texIdx];

                if (textureData.imageId == imageData.id)
                {
                    OvrAvatarLog.LogVerbose(
                        $"Found match for image index {imageIndex} to texture index {texIdx}",
                        primitiveLogScope);
                    // Resolve the image now.
                    OvrAvatarImage image;
                    if (!OvrAvatarManager.GetOvrAvatarAsset(imageData.id, out image))
                    {
                        OvrAvatarLog.LogDebug($"Created image for id {imageData.id}", primitiveLogScope);
                        image = loader.CreateImage(in textureData, in imageData, imageIndex, resourceId);
                    }

                    OvrAvatarLog.Assert(image != null, primitiveLogScope);
                    images[imageIndex] = image;

                    break;
                }
            }

            if (images[imageIndex] == null)
            {
                OvrAvatarLog.LogWarning($"Failed to find textures data for image {imageData.id}", primitiveLogScope);
                // TODO: Assign some sort of fallback image?
            }
        }

        #endregion

        private void StripExcludedSubMeshes(ref NativeArray<ushort> triangles)
        {
            var ct = _cancellationTokenSource.Token;
            ct.ThrowIfCancellationRequested();

            if (subMeshInclusionFlags != CAPI.ovrAvatar2EntitySubMeshInclusionFlags.All)
            {
                uint subMeshCount = 0;
                var countResult = CAPI.ovrAvatar2Primitive_GetSubMeshCount(assetId, out subMeshCount);
                ct.ThrowIfCancellationRequested();
                if (countResult.IsSuccess())
                {
                    unsafe
                    {
                        for (uint subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                        {
                            CAPI.ovrAvatar2PrimitiveSubmesh subMesh;
                            var subMeshResult =
                                CAPI.ovrAvatar2Primitive_GetSubMeshByIndex(assetId, subMeshIndex, out subMesh);
                            ct.ThrowIfCancellationRequested();
                            if (subMeshResult.IsSuccess())
                            {
                                // TODO this should honor the _activeSubMeshesIncludeUntyped flag
                                // ^ This is not possible as that is an OvrAvatarEntity flag
                                var inclusionType = subMesh.inclusionFlags;
                                if ((inclusionType & subMeshInclusionFlags) == 0 &&
                                    inclusionType != CAPI.ovrAvatar2EntitySubMeshInclusionFlags.None)
                                {
                                    uint triangleIndex = subMesh.indexStart;
                                    for (uint triangleCount = 0; triangleCount < subMesh.indexCount; triangleCount++)
                                    {
                                        // current strategy is to degenerate the triangle...
                                        int triangleBase = (int)(triangleIndex + triangleCount);
                                        triangles[triangleBase] = 0;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void GetLodInfo()
        {
            lod = LOD_INVALID;

            var result = CAPI.ovrAvatar2Asset_GetLodFlags(assetId, out var lodFlag);
            if (result.IsSuccess())
            {
                lodFlags = lodFlag;

                // TODO: Handle lods as flags, not a single int. Until then, take the highest quality lod available (lowest bit)
                const UInt32 highBit = (UInt32)CAPI.ovrAvatar2EntityLODFlags.LOD_4;
                UInt32 flagValue = (UInt32)lodFlag;

                int i = 0, maskValue = 1 << 0;
                do
                {
                    if ((flagValue & maskValue) != 0)
                    {
                        lod = i;
                        break;
                    }

                    maskValue = 1 << ++i;
                } while (maskValue <= highBit);
            }
        }

        private void GetViewInfo()
        {
            var result = CAPI.ovrAvatar2Asset_GetViewFlags(assetId, out var flags);
            if (result.IsSuccess())
            {
                viewFlags = flags;
            }
            else
            {
                OvrAvatarLog.LogWarning($"GetViewFlags Failed: {result}", primitiveLogScope);
            }
        }

        private void GetManifestationInfo()
        {
            var result = CAPI.ovrAvatar2Asset_GetManifestationFlags(assetId, out var flags);
            if (result.IsSuccess())
            {
                manifestationFlags = flags;
            }
            else
            {
                OvrAvatarLog.LogWarning($"GetManifestationFlags Failed: {result}", primitiveLogScope);
            }
        }

        private void GetSubMeshInclusionInfo()
        {
            // sub mesh inclusion flags used at this stage will work as load filters,
            // they must be specified in the creationInfo of the AvatarEntity before loading.
            var result = CAPI.ovrAvatar2Asset_GetSubMeshInclusionFlags(assetId, out var flags);
            if (result.IsSuccess())
            {
                subMeshInclusionFlags = flags;
            }
            else
            {
                OvrAvatarLog.LogWarning($"GetSubMeshInclusionInfo Failed: {result}", primitiveLogScope);
            }
        }

        private void GetHighQualityInfo()
        {
            var result = CAPI.ovrAvatar2Asset_GetHighQualityFlags(assetId, out var flags);
            if (result.IsSuccess())
            {
                highQualityFlags = flags;
            }
            else
            {
                OvrAvatarLog.LogWarning($"GetHighQualityFlags Failed: {result}", primitiveLogScope);
            }
        }

        /////////////////////////////////////////////////
        //:: Vertex Buffer API
        // TODO: Factor out into its own file, currently meshInfo access is required for that.

        private void CreateVertexBuffer(MeshInfo meshInfo)
        {
            // Apply mesh info on main thread
            // Get the vertex format information from the fetched vertex data.
            // We need to build a dynamic layout based on the actual data present.
            var vertexCount = (int)meshInfo.vertexCount;
            var vertexFormat = GetVertexFormat(meshInfo, out var vertexStride);
            var vertexSizeInBytes = vertexStride * vertexCount;

            // Create a vertex buffer using the format and stride.
            meshInfo.vertexBuffer = new VertexBuffer
            {
                vertexFormat = vertexFormat,
                vertexLayout = CreateVertexLayout(vertexFormat),
                vertexCount = vertexCount,
                vertexStride = vertexStride,
                vertices = new NativeArray<byte>(vertexSizeInBytes, _nativeAllocator, _nativeArrayInit),
            };
        }

        private VertexFormat GetVertexFormat(MeshInfo meshInfo, out int vertexStride)
        {
            // TODO: Support different attribute formats rather than hardcoding them. This will be useful for quantizing
            // vertex data to reduce vertex shader read bandwidth.
            // TODO: Use constants for vector and color sizes.
            VertexFormat vertexFormat = VertexFormat.VF_POSITION;
            vertexStride = 3 * sizeof(float);   // assume that all vertex formats have a position.
            if (OvrAvatarManager.Instance.UnitySMRSupported)
            {
                if (meshInfo.normals.Length > 0)
                {
                    vertexFormat |= VertexFormat.VF_NORMAL;
                    vertexStride += 3 * sizeof(float);
                }
                if (meshInfo.hasTangents && meshInfo.tangents.Length > 0)
                {
                    vertexFormat |= VertexFormat.VF_TANGENT;
                    vertexStride += 4 * sizeof(float);
                }
            }

            if (meshInfo.colors.Length > 0 || meshInfo.subMeshTypes.Length > 0)
            {
                vertexFormat |= VertexFormat.VF_COLOR;
                vertexStride += 4;
            }
            if (meshInfo.texCoords.Length > 0)
            {
                vertexFormat |= VertexFormat.VF_TEXCOORD0;
                vertexStride += 2 * sizeof(float);
            }
            if (meshInfo.colorsORMT.Length > 0)
            {
                vertexFormat |= VertexFormat.VF_COLOR_ORMT;
                vertexStride += 4 * sizeof(float);
            }
            if (data.jointCount > 0 && OvrAvatarManager.Instance.UnitySMRSupported)
            {
                vertexFormat |= (VertexFormat.VF_BONE_WEIGHTS | VertexFormat.VF_BONE_INDICES);
                vertexStride += 4 * sizeof(float);    // weights
                vertexStride += 4;    // bone indices
            }

            OvrAvatarLog.LogVerbose($"Vertex Format = {vertexFormat}, Stride = {vertexStride}", primitiveLogScope);
            return vertexFormat;
        }

        private List<VertexAttributeDescriptor> CreateVertexLayout(VertexFormat format)
        {
            // TODO: Support different attribute formats rather than hardcoding them. This will be useful for quantizing
            // vertex data to reduce vertex shader read bandwidth.
            var vertexLayout = new List<VertexAttributeDescriptor>();
            // Note: Unity expects vertex attributes to exist in a specific order, any deviation causes an error.
            // Order: Position, Normal, Tangent, Color, TexCoord0, TexCoord1, ..., BlendWeights, BlendIndices
            if ((format & VertexFormat.VF_POSITION) == VertexFormat.VF_POSITION)
            {
                vertexLayout.Add(new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32,
                    3, VF_STREAM_ID));
            }
            if ((format & VertexFormat.VF_NORMAL) == VertexFormat.VF_NORMAL)
            {
                vertexLayout.Add(new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3,
                    VF_STREAM_ID));
            }
            if ((format & VertexFormat.VF_TANGENT) == VertexFormat.VF_TANGENT)
            {
                vertexLayout.Add(new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32,
                    4, VF_STREAM_ID));
            }
            if ((format & VertexFormat.VF_COLOR) == VertexFormat.VF_COLOR)
            {
                vertexLayout.Add(new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4,
                    VF_STREAM_ID));
            }
            if ((format & VertexFormat.VF_TEXCOORD0) == VertexFormat.VF_TEXCOORD0)
            {
                vertexLayout.Add(new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32,
                    2, VF_STREAM_ID));
            }
            if ((format & VertexFormat.VF_COLOR_ORMT) == VertexFormat.VF_COLOR_ORMT)
            {
                vertexLayout.Add(new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32,
                    4, VF_STREAM_ID));
            }
            if ((format & VertexFormat.VF_BONE_WEIGHTS) == VertexFormat.VF_BONE_WEIGHTS)
            {
                vertexLayout.Add(new VertexAttributeDescriptor(VertexAttribute.BlendWeight,
                    VertexAttributeFormat.Float32, 4, VF_STREAM_ID));
            }
            if ((format & VertexFormat.VF_BONE_INDICES) == VertexFormat.VF_BONE_INDICES)
            {
                vertexLayout.Add(new VertexAttributeDescriptor(VertexAttribute.BlendIndices,
                    VertexAttributeFormat.UInt8, 4, VF_STREAM_ID));
            }

            return vertexLayout;
        }

        private void CopyMeshDataIntoVertexBuffer(MeshInfo meshInfo)
        {
            var vertexBuffer = meshInfo.vertexBuffer;
            var vertexFormat = vertexBuffer.vertexFormat;
            var vertexStride = vertexBuffer.vertexStride;

            unsafe
            {
                var vertices = vertexBuffer.vertices.GetPtr();
                for (int i = 0; i < vertexBuffer.vertexCount; i++)
                {
                    byte* outBuffer = &vertices[vertexStride * i];
                    int offset = 0;
                    if ((vertexFormat & VertexFormat.VF_POSITION) == VertexFormat.VF_POSITION)
                    {
                        Vector3* outPos = (Vector3*)&outBuffer[offset];
                        *outPos = meshInfo.verts[i];
                        offset += 3 * sizeof(float);
                    }

                    if ((vertexFormat & VertexFormat.VF_NORMAL) == VertexFormat.VF_NORMAL)
                    {
                        Vector3* outNrm = (Vector3*)&outBuffer[offset];
                        *outNrm = meshInfo.normals[i];
                        offset += 3 * sizeof(float);
                    }

                    if ((vertexFormat & VertexFormat.VF_TANGENT) == VertexFormat.VF_TANGENT)
                    {
                        Vector4* outTangent = (Vector4*)&outBuffer[offset];
                        *outTangent = meshInfo.tangents[i];
                        offset += 4 * sizeof(float);
                    }

                    if ((vertexFormat & VertexFormat.VF_COLOR) == VertexFormat.VF_COLOR)
                    {
                        Color32* outColor = (Color32*)&outBuffer[offset];
                        *outColor = meshInfo.meshColors[i];
                        offset += 4;
                    }

                    if ((vertexFormat & VertexFormat.VF_TEXCOORD0) == VertexFormat.VF_TEXCOORD0)
                    {
                        Vector2* outUv = (Vector2*)&outBuffer[offset];
                        *outUv = meshInfo.texCoords[i];
                        offset += 2 * sizeof(float);
                    }

                    if ((vertexFormat & VertexFormat.VF_COLOR_ORMT) == VertexFormat.VF_COLOR_ORMT)
                    {
                        Vector4* outColor = (Vector4*)&outBuffer[offset];
                        *outColor = meshInfo.colorsORMT[i];
                        offset += 4 * sizeof(float);
                    }

                    if ((vertexFormat & VertexFormat.VF_BONE_WEIGHTS) == VertexFormat.VF_BONE_WEIGHTS)
                    {
                        Vector4* outWeights = (Vector4*)&outBuffer[offset];
                        *outWeights = new Vector4(
                            meshInfo.boneWeights[i].weight0, meshInfo.boneWeights[i].weight1,
                            meshInfo.boneWeights[i].weight2, meshInfo.boneWeights[i].weight3);
                        offset += 4 * sizeof(float);
                    }

                    if ((vertexFormat & VertexFormat.VF_BONE_INDICES) == VertexFormat.VF_BONE_INDICES)
                    {
                        Color32* outIndices = (Color32*)&outBuffer[offset];
                        outIndices->r = (byte)meshInfo.boneWeights[i].boneIndex0;
                        outIndices->g = (byte)meshInfo.boneWeights[i].boneIndex1;
                        outIndices->b = (byte)meshInfo.boneWeights[i].boneIndex2;
                        outIndices->a = (byte)meshInfo.boneWeights[i].boneIndex3;
                        offset += 4;
                    }
                }
            }
        }

        private static void DisposeVertexBuffer(MeshInfo meshInfo)
        {
            meshInfo.vertexBuffer.vertices.Reset();
        }

        /////////////////////////////////////////////////
        //:: Build Mesh

        private void RetrieveTriangles(MeshLodInfo meshMetaInfo, MeshInfo meshInfo)
        {
            // Get index buffer, we will use this to strip out data for other LODs
            meshInfo.triangles = CreateIndexBuffer(data, out meshMetaInfo.repacker);
            if (!meshInfo.triangles.IsCreated)
            {
                throw new Exception("RetrieveTriangles failed");
            }

            // TODO: Confirm topology - we only currently support triangle
            triCount = (uint)(meshInfo.triangles.Length / 3);
        }

        private void RetrieveMeshData(MeshInfo meshInfo, in VertexRepacker vertexRepacker)
        {
            var ct = _cancellationTokenSource.Token;
            ct.ThrowIfCancellationRequested();

            // Apply Data
            meshInfo.verts = CreateVertexPositions(in vertexRepacker, ct);
            meshInfo.normals = CreateVertexNormals(in vertexRepacker, ct);
            meshInfo.tangents = CreateVertexTangents(in vertexRepacker, ct);
            meshInfo.texCoords = CreateVertexTexCoords(in vertexRepacker, ct);

            meshInfo.colors = CreateVertexColors(in vertexRepacker, ct);
            meshInfo.colorsORMT = CreateVertexColorsORMT(in vertexRepacker, ct);
            meshInfo.subMeshTypes = CreateVertexSubMeshTypes(in vertexRepacker, ct);

            meshInfo.boneWeights = data.jointCount > 0 ? RetrieveBoneWeights(in vertexRepacker, ct) : null;
        }

        private void InvokeOnMeshLoaded(Mesh sourceMesh, MeshInfo meshInfo)
        {
            OvrAvatarLog.LogInfo($"InvokeOnAvatarMeshLoaded", "", OvrAvatarManager.Instance);
            Profiler.BeginSample("OvrAvatarManager::InvokeOnAvatarMeshLoaded Callbacks");
            try
            {
                if (OvrAvatarManager.Instance.HasMeshLoadListener)
                {
                    var destMesh = new OvrAvatarManager.MeshData(
                        sourceMesh.name,
                        sourceMesh.triangles,
                        // We want to decouple the GPU vertex representation from the CPU representation.
                        // So instead of reading from the mesh directly, we read from the internal mesh info.
                        (meshInfo.verts.IsCreated) ? meshInfo.verts.ToArray() : Array.Empty<Vector3>(),
                        (meshInfo.normals.IsCreated) ? meshInfo.normals.ToArray() : Array.Empty<Vector3>(),
                        (meshInfo.meshColors.IsCreated) ? meshInfo.meshColors.ToArray() : Array.Empty<Color32>(),
                        (meshInfo.texCoords.IsCreated) ? meshInfo.texCoords.ToArray() : Array.Empty<Vector2>(),
                        (meshInfo.tangents.IsCreated) ? meshInfo.tangents.ToArray() : Array.Empty<Vector4>(),
                        meshInfo.boneWeights,
                        // Bind poses are not part of the vertex data.
                        sourceMesh.bindposes
                    );
                    OvrAvatarManager.Instance.InvokeMeshLoadEvent(this, destMesh);
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogException(
                    "OnAvatarMeshLoaded user callback", e, primitiveLogScope,
                    OvrAvatarManager.Instance);
            }
            finally { Profiler.EndSample(); }
        }

        private void AssembleMeshColors(MeshInfo meshInfo)
        {
            uint vertCount = meshInfo.vertexCount;
            int colorsCount = meshInfo.colors.Length;
            int subMeshCount = meshInfo.subMeshTypes.Length;
            if (vertCount > 0 && (colorsCount > 0 || subMeshCount > 0))
            {
                var finalMeshColors
                    = new NativeArray<Color32>((int)vertCount, _nativeAllocator, _nativeArrayInit);

                try
                {
                    unsafe
                    {
                        Color* meshColors = colorsCount > 0 ? meshInfo.colors.GetPtr() : null;
                        float* subMeshTypes = subMeshCount > 0 ? meshInfo.subMeshTypes.GetPtr() : null;

                        Color32* finalColors = finalMeshColors.GetPtr();
                        Debug.Assert(finalColors != null);

                        for (uint vertIdx = 0; vertIdx < vertCount; vertIdx++)
                        {
                            var finalMeshColor = new Color32(255, 255, 255, 0);
                            if (vertIdx < colorsCount)
                            {
                                Debug.Assert(meshColors != null);

                                var originalColor = meshColors + vertIdx;
                                finalMeshColor.r = (byte)(originalColor->r * 255f);
                                finalMeshColor.g = (byte)(originalColor->g * 255f);
                                finalMeshColor.b = (byte)(originalColor->b * 255f);
                            }

                            if (vertIdx < subMeshCount)
                            {
                                Debug.Assert(subMeshTypes != null);

                                finalMeshColor.a = (byte)subMeshTypes[vertIdx];
                            }

                            finalColors[vertIdx] = finalMeshColor;
                        }
                    }
                    meshInfo.meshColors = finalMeshColors;
                }
                catch (Exception e)
                {
                    OvrAvatarLog.LogException("AssembleMeshColors", e, primitiveLogScope);

                    finalMeshColors.Reset();
                }
            }

            meshInfo.colors.Reset();
            meshInfo.subMeshTypes.Reset();
        }

        #region Retrieve Primitive Data

        private delegate CAPI.ovrAvatar2Result VertexBufferAccessor(
            CAPI.ovrAvatar2VertexBufferId vertexBufferId, IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        private delegate CAPI.ovrAvatar2Result VertexBufferAccessorWithPrimId(
            CAPI.ovrAvatar2Id primitiveId, CAPI.ovrAvatar2VertexBufferId vertexBufferId, IntPtr buffer, UInt32 bytes,
            UInt32 stride);

        private NativeArray<T> CreateVertexData<T>(in VertexRepacker repacker
            , VertexBufferAccessor accessor
            , string accessorName, CancellationToken ct) where T : unmanaged
        {
            ct.ThrowIfCancellationRequested();

            NativeArray<T> vertsBufferArray = default;
            try
            {
                vertsBufferArray = new NativeArray<T>((int)bufferVertexCount, _nativeAllocator, _nativeArrayInit);
                IntPtr vertsBuffer = vertsBufferArray.GetIntPtr();

                var elementSize = UnsafeUtility.SizeOf<T>();
                UInt32 stride = (UInt32)elementSize;
                UInt32 bufferSize = vertsBufferArray.GetBufferSize(elementSize);
                var result = accessor(
                    data.vertexBufferId, vertsBuffer, bufferSize, stride);

                if (repacker.NeedsRepacking)
                {
                    return CreateProcessResult(repacker, vertsBufferArray, accessorName, result, ct);
                }
                else
                {
                    switch (result)
                    {
                        case CAPI.ovrAvatar2Result.Success:
                            var resultBuffer = vertsBufferArray;
                            vertsBufferArray = default;
                            return resultBuffer;

                        case CAPI.ovrAvatar2Result.DataNotAvailable:
                            return default;

                        default:
                            OvrAvatarLog.LogError($"{accessorName} {result}", primitiveLogScope);
                            return default;
                    }
                }
            }
            finally { vertsBufferArray.Reset(); }
        }

        private NativeArray<T> CreateVertexDataWithPrimId<T>(in VertexRepacker repacker
            , VertexBufferAccessorWithPrimId accessor
            , string accessorName, CancellationToken ct) where T : unmanaged
        {
            ct.ThrowIfCancellationRequested();

            NativeArray<T> vertsBufferArray = default;
            try
            {
                vertsBufferArray = new NativeArray<T>((int)bufferVertexCount, _nativeAllocator, _nativeArrayInit);
                {
                    var elementSize = UnsafeUtility.SizeOf<T>();
                    var vertsBuffer = vertsBufferArray.GetIntPtr();
                    var bufferSize = vertsBufferArray.GetBufferSize(elementSize);
                    var stride = (UInt32)elementSize;

                    var result = accessor(data.id, data.vertexBufferId, vertsBuffer, bufferSize, stride);
                    if (repacker.NeedsRepacking)
                    {
                        return CreateProcessResult(repacker, vertsBufferArray, accessorName, result, ct);
                    }
                    else
                    {
                        var resultBuffer = vertsBufferArray;
                        vertsBufferArray = default;
                        return resultBuffer;
                    }
                }
            }
            finally { vertsBufferArray.Reset(); }
        }

        private NativeArray<Vector3> CreateVertexPositions(in VertexRepacker repacker, CancellationToken ct)
        {
            return CreateVertexData<Vector3>(
                in repacker
                , CAPI.ovrAvatar2VertexBuffer_GetPositions, "GetVertexPositions", ct);
        }

        private NativeArray<Vector3> CreateVertexNormals(in VertexRepacker repacker, CancellationToken ct)
        {
            return CreateVertexData<Vector3>(
                in repacker
                , CAPI.ovrAvatar2VertexBuffer_GetNormals, "GetVertexNormals", ct);
        }

        private NativeArray<Vector4> CreateVertexTangents(in VertexRepacker repacker, CancellationToken ct)
        {
            return CreateVertexData<Vector4>(
                in repacker
                , CAPI.ovrAvatar2VertexBuffer_GetTangents, "GetVertexTangents", ct);
        }

        private NativeArray<Color> CreateVertexColors(in VertexRepacker repacker, CancellationToken ct)
        {
            return CreateVertexData<Color>(
                in repacker
                , CAPI.ovrAvatar2VertexBuffer_GetColors, "GetVertexColors", ct);
        }

        private NativeArray<Color> CreateVertexColorsORMT(in VertexRepacker repacker, CancellationToken ct)
        {
            return CreateVertexData<Color>(
                in repacker
                , CAPI.ovrAvatar2VertexBuffer_GetColorsORMT, "GetVertexColorsORMT", ct);
        }

        private NativeArray<Vector2> CreateVertexTexCoords(in VertexRepacker repacker, CancellationToken ct)
        {
            return CreateVertexData<Vector2>(
                in repacker
                , CAPI.ovrAvatar2VertexBuffer_GetTexCoord0, "GetVertexTexCoord0", ct);
        }

        private NativeArray<float> CreateVertexSubMeshTypes(in VertexRepacker repacker, CancellationToken ct)
        {
            return CreateVertexDataWithPrimId<float>(
                in repacker
                , CAPI.ovrAvatar2VertexBuffer_GetSubMeshTypesFloat, "GetSubMeshTypesFloat", ct);
        }

        private BoneWeight[] RetrieveBoneWeights(in VertexRepacker repacker, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var vec4usStride = (UInt32)UnsafeUtility.SizeOf<CAPI.ovrAvatar2Vector4us>();
            var vec4fStride = (UInt32)UnsafeUtility.SizeOf<CAPI.ovrAvatar2Vector4f>();

            var indicesBuffer =
                new NativeArray<CAPI.ovrAvatar2Vector4us>((int)bufferVertexCount, _nativeAllocator, _nativeArrayInit);
            var weightsBuffer =
                new NativeArray<CAPI.ovrAvatar2Vector4f>((int)bufferVertexCount, _nativeAllocator, _nativeArrayInit);

            try
            {
                IntPtr indicesPtr = indicesBuffer.GetIntPtr();
                IntPtr weightsPtr = weightsBuffer.GetIntPtr();

                var indicesBufferSize = indicesBuffer.GetBufferSize(vec4usStride);
                var weightsBufferSize = weightsBuffer.GetBufferSize(vec4fStride);

                var result = CAPI.ovrAvatar2VertexBuffer_GetJointIndices(
                    data.vertexBufferId, indicesPtr, indicesBufferSize, vec4usStride);
                ct.ThrowIfCancellationRequested();
                if (result == CAPI.ovrAvatar2Result.DataNotAvailable) { return Array.Empty<BoneWeight>(); }
                else if (result != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogError($"GetVertexJointIndices {result}", primitiveLogScope);
                    return null;
                }

                result = CAPI.ovrAvatar2VertexBuffer_GetJointWeights(
                    data.vertexBufferId, weightsPtr,
                    weightsBufferSize, vec4fStride);
                ct.ThrowIfCancellationRequested();
                if (result == CAPI.ovrAvatar2Result.DataNotAvailable) { return Array.Empty<BoneWeight>(); }
                else if (result != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogError($"GetVertexJointWeights {result}", primitiveLogScope);
                    return null;
                }

                ct.ThrowIfCancellationRequested();

                using (var boneWeightsSrc =
                       new NativeArray<BoneWeight>((int)bufferVertexCount, _nativeAllocator, _nativeArrayInit))
                {
                    var boneWeights = boneWeightsSrc.Slice();

                    unsafe
                    {
                        var indices = (CAPI.ovrAvatar2Vector4us*)indicesBuffer.GetUnsafePtr();
                        var weights = (CAPI.ovrAvatar2Vector4f*)weightsBuffer.GetUnsafePtr();
                        for (int i = 0; i < bufferVertexCount; ++i)
                        {
                            ref CAPI.ovrAvatar2Vector4us jointIndex = ref indices[i];
                            ref CAPI.ovrAvatar2Vector4f jointWeight = ref weights[i];

                            var boneWeight = new BoneWeight
                            {
                                boneIndex0 = jointIndex.x,
                                boneIndex1 = jointIndex.y,
                                boneIndex2 = jointIndex.z,
                                boneIndex3 = jointIndex.w,
                                weight0 = jointWeight.x,
                                weight1 = jointWeight.y,
                                weight2 = jointWeight.z,
                                weight3 = jointWeight.w
                            };
                            boneWeights[i] = boneWeight;
                        }
                    }

                    ct.ThrowIfCancellationRequested();

                    return repacker.RepackAttribute(boneWeightsSrc);
                }
            }
            finally
            {
                indicesBuffer.Dispose();
                weightsBuffer.Dispose();
            }
        }

        private NativeArray<UInt16> CreateIndexBuffer(in CAPI.ovrAvatar2Primitive prim, out VertexRepacker repacker)
        {
            var ct = _cancellationTokenSource.Token;
            ct.ThrowIfCancellationRequested();

            NativeArray<UInt16> triBuffer = default;
            try
            {
                triBuffer = new NativeArray<UInt16>((int)data.indexCount, _nativeAllocator, _nativeArrayInit);

                UInt32 bufferSize = triBuffer.GetBufferSize(sizeof(UInt16));
                bool result = CAPI.OvrAvatar2Primitive_GetIndexData(assetId, in triBuffer, bufferSize);
                if (!result)
                {
                    repacker = default;
                    return default;
                }

                ct.ThrowIfCancellationRequested();
                // may steal triBuffer, pass it back as packedIndices, and set triBuffer to default
                repacker = VertexRepacker.Create(
                    ref triBuffer, in prim, _bufferVertexCount, out var packedIndices,
                    ct);
                return packedIndices;
            }
            finally { triBuffer.Reset(); }
        }

        #endregion

        /////////////////////////////////////////////////
        //:: Build Material

        #region Build Material

        private void Material_GetTexturesData(CAPI.ovrAvatar2Id resourceId, ref MaterialInfo materialInfo)
        {
            var ct = _cancellationTokenSource.Token;
            ct.ThrowIfCancellationRequested();

            CAPI.ovrAvatar2Result result;

            // Get data for all textures
            materialInfo.texturesData = new CAPI.ovrAvatar2MaterialTexture[data.textureCount];
            for (UInt32 i = 0; i < data.textureCount; ++i)
            {
                ref var materialTexture = ref materialInfo.texturesData[i];
                result = CAPI.ovrAvatar2Primitive_GetMaterialTextureByIndex(assetId, i, out materialTexture);
                ct.ThrowIfCancellationRequested();
                if (result != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogError($"GetMaterialTextureByIndex ({i}) {result}", primitiveLogScope);

                    materialInfo.texturesData[i] = default;
                    continue;
                }

                if (materialTexture.type == CAPI.ovrAvatar2MaterialTextureType.MetallicRoughness)
                {
                    materialInfo.hasMetallic = true;
                }
            }

            // Get data for all images
            result = CAPI.ovrAvatar2Asset_GetImageCount(resourceId, out UInt32 imageCount);
            ct.ThrowIfCancellationRequested();
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"GetImageCount {result}", primitiveLogScope);
                return;
            }

            materialInfo.imageData = new CAPI.ovrAvatar2Image[imageCount];

            for (UInt32 i = 0; i < imageCount; ++i)
            {
                ref var imageData = ref materialInfo.imageData[i];
                result = CAPI.ovrAvatar2Asset_GetImageByIndex(resourceId, i, out imageData);
                ct.ThrowIfCancellationRequested();
                if (result != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogError($"GetImageByIndex ({i}) {result}", primitiveLogScope);

                    materialInfo.imageData[i] = default;
                    continue;
                }
            }
        }

        private void ApplyTexture(Texture2D texture, CAPI.ovrAvatar2MaterialTexture textureData)
        {
            switch (textureData.type)
            {
                case CAPI.ovrAvatar2MaterialTextureType.BaseColor:
                    if (!string.IsNullOrEmpty(_shaderConfig.NameTextureParameter_baseColorTexture))
                        material.SetTexture(_shaderConfig.NameTextureParameter_baseColorTexture, texture);
                    material.SetColor(
                        _shaderConfig.NameColorParameter_BaseColorFactor,
                        _shaderConfig.UseColorParameter_BaseColorFactor ? textureData.factor : Color.white);
                    material.mainTexture = texture;
                    break;

                case CAPI.ovrAvatar2MaterialTextureType.Normal:
                    if (!string.IsNullOrEmpty(_shaderConfig.NameTextureParameter_normalTexture))
                        material.SetTexture(_shaderConfig.NameTextureParameter_normalTexture, texture);
                    break;

                case CAPI.ovrAvatar2MaterialTextureType.Emissive:
                    if (!string.IsNullOrEmpty(_shaderConfig.NameTextureParameter_emissiveTexture))
                        material.SetTexture(_shaderConfig.NameTextureParameter_emissiveTexture, texture);
                    break;

                case CAPI.ovrAvatar2MaterialTextureType.Occulusion:
                    if (!string.IsNullOrEmpty(_shaderConfig.NameTextureParameter_occlusionTexture))
                        material.SetTexture(_shaderConfig.NameTextureParameter_occlusionTexture, texture);
                    break;

                case CAPI.ovrAvatar2MaterialTextureType.MetallicRoughness:
                    if (!string.IsNullOrEmpty(_shaderConfig.NameTextureParameter_metallicRoughnessTexture))
                        material.SetTexture(_shaderConfig.NameTextureParameter_metallicRoughnessTexture, texture);
                    material.SetFloat(
                        _shaderConfig.NameFloatParameter_MetallicFactor,
                        _shaderConfig.UseFloatParameter_MetallicFactor ? textureData.factor.x : 1f);
                    material.SetFloat(
                        _shaderConfig.NameFloatParameter_RoughnessFactor,
                        _shaderConfig.UseFloatParameter_RoughnessFactor ? textureData.factor.y : 1f);
                    break;

                case CAPI.ovrAvatar2MaterialTextureType.UsedInExtension:
                    // Let extensions handle it
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        /////////////////////////////////////////////////
        //:: Build Other Data

        private void SetupMorphTargets(MorphTargetInfo[] morphTargetInfo, in VertexRepacker repacker)
        {
            var ct = _cancellationTokenSource.Token;
            ct.ThrowIfCancellationRequested();

            var sizeOfOvrVector3 = Marshal.SizeOf<CAPI.ovrAvatar2Vector3f>();
            UInt32 bufferSize = (UInt32)(sizeOfOvrVector3 * bufferVertexCount);
            UInt32 stride = (UInt32)sizeOfOvrVector3;

            if (morphTargetInfo.Length != morphTargetCount)
            {
                throw new Exception(
                    $"Incorrect morphTargetInfo[] size. Was {morphTargetInfo.Length}, but expected {morphTargetCount}");
            }

            unsafe
            {
                const int nameBufferLength = 255;
                byte* nameBuffer = stackalloc byte[nameBufferLength];
                for (UInt32 iMorphTarget = 0; iMorphTarget < morphTargetCount; ++iMorphTarget)
                {
                    // Would be nice if we had a single simple CAPI that returned what attributes were available, one call to get all 3
                    // we want to figure out which are available before we spend time allocating giant buffers.
                    var positionsResult =
                        CAPI.ovrAvatar2MorphTarget_GetVertexPositions(
                            data.morphTargetBufferId, iMorphTarget,
                            null, 0, stride);
                    if (!positionsResult.IsSuccess())
                    {
                        OvrAvatarLog.LogError(
                            $"MorphTarget_GetVertexPositions ({iMorphTarget}) {positionsResult}",
                            primitiveLogScope);
                        continue;
                    }

                    var normalsResult =
                        CAPI.ovrAvatar2MorphTarget_GetVertexNormals(
                            data.morphTargetBufferId, iMorphTarget,
                            null, 0, stride);
                    bool normalsAvailable = normalsResult.IsSuccess();
                    if (!normalsAvailable && normalsResult != CAPI.ovrAvatar2Result.DataNotAvailable)
                    {
                        OvrAvatarLog.LogError(
                            $"MorphTarget_GetVertexNormals ({iMorphTarget}) {normalsResult}",
                            primitiveLogScope);
                        continue;
                    }

                    var tangentsResult =
                        CAPI.ovrAvatar2MorphTarget_GetVertexTangents(
                            data.morphTargetBufferId, iMorphTarget,
                            null, 0, stride);
                    bool tangentsAvailable = tangentsResult.IsSuccess();
                    if (!tangentsAvailable && tangentsResult != CAPI.ovrAvatar2Result.DataNotAvailable)
                    {
                        OvrAvatarLog.LogError(
                            $"MorphTarget_GetVertexTangents ({iMorphTarget}) {tangentsResult}",
                            primitiveLogScope);
                        continue;
                    }

                    ct.ThrowIfCancellationRequested();

                    NativeArray<Vector3> positionsArray = default;
                    NativeArray<Vector3> normalsArray = default;
                    NativeArray<Vector3> tangentsArray = default;
                    try
                    {
                        // Positions
                        positionsArray =
                            new NativeArray<Vector3>((int)bufferVertexCount, _nativeAllocator, _nativeArrayInit);

                        positionsResult =
                            CAPI.ovrAvatar2MorphTarget_GetVertexPositions(
                                data.morphTargetBufferId, iMorphTarget,
                                positionsArray.CastOvrPtr(), bufferSize, stride);
                        ct.ThrowIfCancellationRequested();
                        if (!positionsResult.IsSuccess())
                        {
                            OvrAvatarLog.LogError(
                                $"MorphTarget_GetVertexPositions ({iMorphTarget}) {positionsResult}",
                                primitiveLogScope);
                            continue;
                        }

                        // Normals
                        if (normalsAvailable)
                        {
                            normalsArray = new NativeArray<Vector3>(
                                (int)bufferVertexCount, _nativeAllocator,
                                _nativeArrayInit);

                            normalsResult =
                                CAPI.ovrAvatar2MorphTarget_GetVertexNormals(
                                    data.morphTargetBufferId, iMorphTarget,
                                    normalsArray.CastOvrPtr(), bufferSize, stride);
                            ct.ThrowIfCancellationRequested();
                            normalsAvailable = normalsResult.IsSuccess();
                            if (!normalsAvailable && normalsResult != CAPI.ovrAvatar2Result.DataNotAvailable)
                            {
                                OvrAvatarLog.LogError(
                                    $"MorphTarget_GetVertexNormals ({iMorphTarget}) {normalsResult}",
                                    primitiveLogScope);
                                continue;
                            }
                        }

                        // Tangents
                        if (tangentsAvailable)
                        {
                            tangentsArray = new NativeArray<Vector3>(
                                (int)bufferVertexCount, _nativeAllocator,
                                _nativeArrayInit);

                            tangentsResult =
                                CAPI.ovrAvatar2MorphTarget_GetVertexTangents(
                                    data.morphTargetBufferId, iMorphTarget,
                                    tangentsArray.CastOvrPtr(),
                                    bufferSize, stride);
                            ct.ThrowIfCancellationRequested();
                            tangentsAvailable = tangentsResult.IsSuccess();
                            if (!tangentsAvailable && tangentsResult != CAPI.ovrAvatar2Result.DataNotAvailable)
                            {
                                OvrAvatarLog.LogError(
                                    $"MorphTarget_GetVertexTangents ({iMorphTarget}) {tangentsResult}",
                                    primitiveLogScope);
                                continue;
                            }
                        }

                        var nameResult =
                            CAPI.ovrAvatar2Asset_GetMorphTargetName(
                                data.morphTargetBufferId, iMorphTarget,
                                nameBuffer, nameBufferLength);
                        ct.ThrowIfCancellationRequested();

                        var name = string.Empty;
                        if (nameResult.IsSuccess())
                        {
                            name = Marshal.PtrToStringAnsi((IntPtr)nameBuffer);
                        }
                        else if (nameResult != CAPI.ovrAvatar2Result.NotFound)
                        {
                            OvrAvatarLog.LogError($"ovrAvatar2MorphTarget_GetName failed with {nameResult}"
                                , primitiveLogScope);
                        }

                        // If we failed to query the name, use the index
                        if (string.IsNullOrEmpty(name)) { name = "morphTarget" + iMorphTarget; }

                        // Add Morph Target
                        morphTargetInfo[iMorphTarget] = new MorphTargetInfo(
                            name,
                            repacker.RepackAttribute(positionsArray),
                            normalsAvailable ? repacker.RepackAttribute(normalsArray) : null,
                            tangentsAvailable ? repacker.RepackAttribute(tangentsArray) : null
                        );
                    }
                    finally
                    {
                        positionsArray.Reset();
                        normalsArray.Reset();
                        tangentsArray.Reset();
                    }
                }
            }
        }

        private void SetupSkin(ref MeshInfo meshInfo)
        {
            var ct = _cancellationTokenSource.Token;
            ct.ThrowIfCancellationRequested();

            var bindPoses = Array.Empty<Matrix4x4>();
            var buildJoints = Array.Empty<int>();

            var jointCount = data.jointCount;
            if (jointCount > 0)
            {
                using var jointsInfoArray =
                    new NativeArray<CAPI.ovrAvatar2JointInfo>(
                        (int)jointCount, _nativeAllocator,
                        _nativeArrayInit);
                unsafe
                {
                    var jointInfoBuffer = jointsInfoArray.GetPtr();
                    var result = CAPI.ovrAvatar2Primitive_GetJointInfo(
                        assetId, jointInfoBuffer,
                        jointsInfoArray.GetBufferSize());
                    ct.ThrowIfCancellationRequested();

                    if (result.EnsureSuccess("ovrAvatar2Primitive_GetJointInfo", primitiveLogScope))
                    {
                        buildJoints = new int[jointCount];
                        bindPoses = new Matrix4x4[jointCount];
                        for (int i = 0; i < jointCount; ++i)
                        {
                            var jointInfoPtr = jointInfoBuffer + i;
                            ref var bindPose = ref bindPoses[i];

                            buildJoints[i] = jointInfoPtr->jointIndex;
                            jointInfoPtr->inverseBind.CopyToUnityMatrix(out bindPose); //Convert to Matrix4x4
                        }
                    }
                } // unsafe
            }

            ct.ThrowIfCancellationRequested();
            meshInfo.bindPoses = bindPoses;
            joints = buildJoints;
        }

        private static NativeArray<T> CreateProcessResult<T>(in VertexRepacker repacker, in NativeArray<T> buffer,
            string capiCallName, CAPI.ovrAvatar2Result result, CancellationToken ct) where T : struct
        {
            ct.ThrowIfCancellationRequested();
            switch (result)
            {
                case CAPI.ovrAvatar2Result.Success:
                    return repacker.CreateRepackedAttributes(in buffer);

                case CAPI.ovrAvatar2Result.DataNotAvailable:
                    return default;

                default:
                    OvrAvatarLog.LogError($"{capiCallName} {result}", primitiveLogScope);
                    return default;
            }
        }

        private class MeshLodInfo
        {
            public VertexRepacker repacker;
        }

        // Unity doesn't currently have access to the buffer ranges for individual LODs, so we deduce them based on the index buffer
        private readonly struct VertexRepacker
        {
            private readonly BufferRange[] ranges;
            private readonly int totalVerts;
            private readonly bool needsRepacking;

            public bool NeedsRepacking => needsRepacking;

            public static VertexRepacker Create(ref NativeArray<UInt16> indices, in CAPI.ovrAvatar2Primitive prim,
                in uint vertexBufferSize, out NativeArray<UInt16> finalIndices, CancellationToken ct)
            {
                if (indices.Length > 0)
                {
                    int totalVerts;
                    bool needsRepacking;
                    var ranges = ProcessIndices(
                        ref indices, out totalVerts, out finalIndices, in prim,
                        in vertexBufferSize, out needsRepacking, ct);
                    return new VertexRepacker(ranges, totalVerts, needsRepacking);
                }
                else
                {
                    finalIndices = default;
                    return new VertexRepacker(null, 0, false);
                }
            }

            private VertexRepacker(BufferRange[] _ranges, int _totalVerts, bool _needsRepacking)
            {
                ranges = _ranges;
                totalVerts = _totalVerts;
                needsRepacking = _needsRepacking;
            }

            [Pure]
            public T[] RepackAttribute<T>(in NativeArray<T> buffer) where T : unmanaged
            {
                if (buffer.Length == 0) { return Array.Empty<T>(); }

                if (needsRepacking)
                {
                    T[] attrBuffer;
                    try { attrBuffer = new T[totalVerts]; }
                    catch (Exception e)
                    {
                        OvrAvatarLog.LogException("allocate primitive buffer (new T[len])", e, primitiveLogScope);
                        attrBuffer = null;
                    }

                    if (attrBuffer == null)
                    {
                        // Run GC to hopefully free memory for dynamic log strings
                        System.GC.Collect();

                        OvrAvatarLog.LogError(
                            $"Failed to allocate primitive buffer of type {typeof(T)} with length {totalVerts}"
                            , primitiveLogScope);
                        return Array.Empty<T>();
                    }

                    int packedOffset = 0;
                    for (var idx = 0; idx < ranges.Length; idx++)
                    {
                        ref readonly var range = ref ranges[idx];
                        range.CopyRelevantSlice(in buffer, attrBuffer, attrBuffer.Length, ref packedOffset);
                    }

                    return attrBuffer;
                }
                else { return buffer.ToArray(); }
            }

            [Pure]
            public NativeArray<T> CreateRepackedAttributes<T>(in NativeArray<T> buffer) where T : struct
            {
                Assert.IsTrue(needsRepacking);

                var packedResult = new NativeArray<T>(totalVerts, _nativeAllocator, _nativeArrayInit);

                int packedOffset = 0;
                for (int idx = 0; idx < ranges.Length; idx++)
                {
                    ref readonly var range = ref ranges[idx];
                    range.CopyRelevantSlice(in buffer, in packedResult, ref packedOffset);
                }

                return packedResult;
            }


            private static BufferRange[] ProcessIndices(ref NativeArray<UInt16> indices, out int totalVerts,
                out NativeArray<UInt16> convertedIndices, in CAPI.ovrAvatar2Primitive prim, in uint vertexBufferSize,
                out bool needsRepacking, CancellationToken ct)
            {
                Debug.Assert(indices.Length > 0);

                // TODO: prim.minIndexValue and prim.maxIndexValue dont seem to be working at the moment.
                // If they worked could save a bit of memory in code below, albeit not much.
                // Just have to make a max sized bit array for now.
                // I see some documentation online about a NativeBitArray in beta as well, might be interesting.
                var indicesUsed = new BitArray(UInt16.MaxValue + 1);
                int minIndex = UInt16.MaxValue;
                int maxIndex = 0;
                unsafe
                {
                    // Use C# unsafe pointers to avoid NativeArray indexer overhead
                    var indicesPtr = indices.GetPtr();
                    for (int idx = 0; idx < indices.Length; idx++)
                    {
                        var index = indicesPtr[idx];
                        indicesUsed.Set(index, true);
                        minIndex = Mathf.Min(minIndex, index);
                        maxIndex = Mathf.Max(maxIndex, index);
                    }
                }
                ct.ThrowIfCancellationRequested();

                var ranges = new List<BufferRange>();
                int rangeStart = minIndex;
                bool inRange = true;
                for (int idx = minIndex + 1; idx < maxIndex; idx++)
                {
                    if (inRange && !indicesUsed[idx])
                    {
                        // -1 because BufferRange is closed on both ends. current idx is first not in range
                        ranges.Add(new BufferRange(rangeStart, idx - 1));
                        inRange = false;
                    }
                    else if (!inRange && indicesUsed[idx])
                    {
                        rangeStart = idx;
                        inRange = true;
                    }
                }

                if (inRange) { ranges.Add(new BufferRange(rangeStart, maxIndex)); }
                ct.ThrowIfCancellationRequested();

                var rangesArray = ranges.ToArray();
                ranges.Clear();
                ct.ThrowIfCancellationRequested();

                needsRepacking = !(rangesArray.Length == 1 && rangesArray[0].FirstIndex == 0 &&
                                   rangesArray[0].LastIndex == (vertexBufferSize - 1));
                if (needsRepacking)
                {
                    // Shift indices to match repacked vert data
                    var indicesLen = indices.Length;
                    convertedIndices = new NativeArray<UInt16>(indicesLen, _nativeAllocator, _nativeArrayInit);

                    unsafe
                    {
                        var indicesPtr = indices.GetPtr();
                        var convertedPtr = convertedIndices.GetPtr();
                        totalVerts = 0;
                        int packIdx = 0;
                        for (int idx = 0; idx < rangesArray.Length; ++idx)
                        {
                            ref readonly var range = ref rangesArray[idx];
                            range.ShiftIndexBuffer(indicesPtr, convertedPtr
                                , ref packIdx, ref totalVerts
                                , indicesLen);
                        }
                    }
                }
                else
                {
                    totalVerts = (int)vertexBufferSize;
                    convertedIndices = indices;
                    indices = default;
                }

                return rangesArray;
            }

            private struct BufferRange
            {
                private readonly Int32 _firstIndex;
                private readonly Int32 _lastIndex;

                private readonly Int32 _sliceLength;

                public Int32 FirstIndex => _firstIndex;
                public Int32 LastIndex => _lastIndex;

                public BufferRange(Int32 vertexCount)
                {
                    _firstIndex = vertexCount;
                    _lastIndex = _sliceLength = 0;
                }

                public BufferRange(Int32 firstIndex, Int32 lastIndex)
                {
                    _firstIndex = firstIndex;
                    _lastIndex = lastIndex;
                    // TODO: replace _lastIndex w/ _stopIndex
                    _sliceLength = lastIndex - firstIndex + 1;
                }

                // JetBrains didn't read the docs for System.Diagnostics.Contracts.Pure :/
                // ReSharper disable once PureAttributeOnVoidMethod
                [Pure]
                public void CopyRelevantSlice<T>(in NativeArray<T> input, in NativeArray<T> output,
                    ref int outputOffset) where T : struct
                {
                    NativeArray<T>.Copy(input, _firstIndex, output, outputOffset, _sliceLength);
                    outputOffset += _sliceLength;
                }

                // JetBrains didn't read the docs for System.Diagnostics.Contracts.Pure :/
                // ReSharper disable once PureAttributeOnVoidMethod
                [Pure]
                public void CopyRelevantSlice<T>(in NativeArray<T> input, T[] output, int outputLength,
                    ref int outputOffset) where T : unmanaged
                {
                    NativeArray<T>.Copy(input, _firstIndex, output, outputOffset, _sliceLength);
                    outputOffset += _sliceLength;
                }

                // JetBrains didn't read the docs for System.Diagnostics.Contracts.Pure :/
                // ReSharper disable once PureAttributeOnVoidMethod
                [Pure]
                public unsafe void ShiftIndexBuffer(UInt16* srcPtr, UInt16* dstPtr,
                    ref Int32 destOffset, ref int totalVerts, int srcLen)
                {
                    Debug.Assert(srcLen > 0);

                    // TODO: Something less dumb, though this is extremely thorough.
                    unsafe
                    {
                        var srcEnd = srcPtr + srcLen;

                        Int32 offset = totalVerts - _firstIndex;
                        do
                        {
                            Int32 vertIdx = *srcPtr;
                            ++srcPtr;
                            if (IsInRange(vertIdx))
                            {
                                *dstPtr = (UInt16)(vertIdx + offset);
                            }

                            ++dstPtr;
                        } while (srcPtr < srcEnd);
                    }

                    totalVerts += _sliceLength;
                }

                // TODO: Remove - caller should determine range
                [Pure]
                private bool IsInRange(Int32 vertIdx)
                {
                    return _firstIndex <= vertIdx && vertIdx <= _lastIndex;
                }
            }
        }

        private sealed class OvrAvatarGpuSkinnedPrimitiveBuilder : IDisposable
        {
            NativeArray<IntPtr> deltaPositions;
            NativeArray<IntPtr> deltaNormals;
            NativeArray<IntPtr> deltaTangents;

            GCHandle[] morphPosHandles;
            GCHandle[] morphNormalHandles;
            GCHandle[] morphTangentHandles;

            Task createPrimitivesTask = null;

            private MeshInfo _meshInfo;

            readonly string shortName;
            readonly uint morphTargetCount;

            public OvrAvatarGpuSkinnedPrimitiveBuilder(string name, uint morphTargetCnt)
            {
                shortName = name;
                morphTargetCount = morphTargetCnt;
            }

            public Task CreateSkinningPrimitivesHelperTask(
                MeshInfo meshInfo,
                MorphTargetInfo[] morphTargetInfo,
                bool hasTangents,
                bool gpuSkinning,
                bool computeSkinning)
            {
                OvrAvatarLog.AssertConstMessage(
                    createPrimitivesTask == null
                    , "recreating gpu and/or compute primitive",
                    primitiveLogScope);

                _meshInfo = meshInfo;

                if (gpuSkinning) { _meshInfo.WillBuildGpuPrimitive(); }
                if (computeSkinning) { _meshInfo.WillBuildComputePrimitive(); }

                createPrimitivesTask = Task.Run(
                    () =>
                    {
                        // TODO: should get pointers to morph target data directly from Native

                        deltaPositions = new NativeArray<IntPtr>(
                            (int)morphTargetCount, _nativeAllocator, _nativeArrayInit);
                        deltaNormals = new NativeArray<IntPtr>(
                            (int)morphTargetCount, _nativeAllocator, _nativeArrayInit);
                        if (hasTangents)
                        {
                            deltaTangents =
                                new NativeArray<IntPtr>((int)morphTargetCount, _nativeAllocator, _nativeArrayInit);
                        }

                        morphPosHandles = new GCHandle[morphTargetCount];
                        morphNormalHandles = new GCHandle[morphTargetCount];
                        if (hasTangents) { morphTangentHandles = new GCHandle[morphTargetCount]; }

                        for (var i = 0; i < morphTargetCount; ++i)
                        {
                            morphPosHandles[i] = GCHandle.Alloc(
                                morphTargetInfo[i].targetPositions, GCHandleType.Pinned);
                            morphNormalHandles[i] = GCHandle.Alloc(
                                morphTargetInfo[i].targetNormals, GCHandleType.Pinned);

                            deltaPositions[i] = morphPosHandles[i].AddrOfPinnedObject();
                            deltaNormals[i] = morphNormalHandles[i].AddrOfPinnedObject();

                            if (hasTangents)
                            {
                                morphTangentHandles[i] =
                                    GCHandle.Alloc(morphTargetInfo[i].targetTangents, GCHandleType.Pinned);
                                deltaTangents[i] = morphTangentHandles[i].AddrOfPinnedObject();
                            }
                        }

                        createPrimitivesTask = null;
                    });
                return createPrimitivesTask;
            }

            public OvrAvatarGpuSkinnedPrimitive BuildPrimitive(MeshInfo meshInfo, Int32[] joints, bool hasTangents)
            {
                OvrAvatarLog.Assert(meshInfo == _meshInfo, primitiveLogScope);

                IntPtr neutralPositions = _meshInfo.verts.GetIntPtr();
                IntPtr neutralNormals = _meshInfo.normals.GetIntPtr();

                IntPtr deltaPosPtr = deltaPositions.GetIntPtr();
                IntPtr deltaNormPtr = deltaNormals.GetIntPtr();

                IntPtr neutralTangents = IntPtr.Zero;
                IntPtr deltaTanPtr = IntPtr.Zero;
                if (hasTangents)
                {
                    neutralTangents = _meshInfo.tangents.GetIntPtr();
                    deltaTanPtr = deltaTangents.GetIntPtr();
                }

                var primitive = new OvrAvatarGpuSkinnedPrimitive(
                    shortName, _meshInfo.vertexCount,
                    neutralPositions, neutralNormals, neutralTangents,
                    morphTargetCount, deltaPosPtr, deltaNormPtr, deltaTanPtr,
                    (uint)joints.Length, _meshInfo.boneWeights,
                    () => { _meshInfo.NeutralPoseTexComplete(); },
                    () =>
                    {
                        _meshInfo.DidBuildGpuPrimitive();
                        if (!_meshInfo.HasPendingPrimitives) { _meshInfo = null; }
                    });

                return primitive;
            }

            public OvrAvatarComputeSkinnedPrimitive BuildComputePrimitive(MeshInfo meshInfo, bool hasTangents)
            {
                OvrAvatarLog.Assert(meshInfo == _meshInfo, primitiveLogScope);

                IntPtr neutralPositions = _meshInfo.verts.GetIntPtr();
                IntPtr neutralNormals = _meshInfo.normals.GetIntPtr();

                IntPtr deltaPosPtr = deltaPositions.GetIntPtr();
                IntPtr deltaNormPtr = deltaNormals.GetIntPtr();

                IntPtr neutralTangents = IntPtr.Zero;
                IntPtr deltaTanPtr = IntPtr.Zero;
                if (hasTangents)
                {
                    neutralTangents = _meshInfo.tangents.GetIntPtr();
                    deltaTanPtr = deltaTangents.GetIntPtr();
                }

                var primitive = new OvrAvatarComputeSkinnedPrimitive(
                    shortName,
                    (int)_meshInfo.vertexCount,
                    neutralPositions,
                    neutralNormals,
                    neutralTangents,
                    (int)morphTargetCount,
                    deltaPosPtr,
                    deltaNormPtr,
                    deltaTanPtr,
                    _meshInfo.boneWeights,
                    () => { _meshInfo.NeutralPoseBuffersComplete(); },
                    () =>
                    {
                        _meshInfo.DidBuildComputePrimitive();
                        if (!_meshInfo.HasPendingPrimitives) { _meshInfo = null; }
                    });

                return primitive;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool isDisposing)
            {
                if (createPrimitivesTask != null)
                {
                    createPrimitivesTask.Wait();
                    createPrimitivesTask = null;
                }

                deltaPositions.Reset();
                deltaNormals.Reset();
                deltaTangents.Reset();

                FreeHandles(ref morphPosHandles);
                FreeHandles(ref morphNormalHandles);
                FreeHandles(ref morphTangentHandles);

                if (_meshInfo != null)
                {
                    _meshInfo.CancelledBuildPrimitives();
                    _meshInfo = null;
                }
            }

            private static void FreeHandles(ref GCHandle[] handles)
            {
                if (handles != null)
                {
                    foreach (var handle in handles)
                    {
                        if (handle.IsAllocated) { handle.Free(); }
                    }

                    handles = null;
                }
            }

            ~OvrAvatarGpuSkinnedPrimitiveBuilder()
            {
                Dispose(false);
            }
        }

        private const Allocator _nativeAllocator = Allocator.Persistent;
        private const NativeArrayOptions _nativeArrayInit = NativeArrayOptions.UninitializedMemory;
    }
}
