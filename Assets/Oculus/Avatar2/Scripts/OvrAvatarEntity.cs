// #define SUBMESH_DEBUGGING

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using UnityEngine.Android;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Application = UnityEngine.Application;

/**
 * @file OvrAvatarEntity.cs
 * This is the main file for a partial class.
 * Other functionality is split out into the files below:
 * - OvrAvatarEntity_Color.cs
 * - OvrAvatarEntity_Debug.cs
 * - OvrAvatarEntity_Loading.cs
 * - OvrAvatarEntity_LOD.cs
 * - OvrAvatarEntity_Rendering.cs
 * - OvrAvatarEntity_Streaming.cs
 */
namespace Oculus.Avatar2
{

    /**
     * Describes a single avatar with multiple levels of detail.
     *
     * An avatar entity encapsulates all of the assets associated with it.
     * It provides access to the avatar's skeleton, meshes and materials
     * and integrates input from tracking components to drive the
     * avatar's motion. This base class provides defaults for all of these.
     * To customize avatar behavior you must derive from OvrAvatarEntity.
     * Then you can supply your application's preferences.
     *
     * # Loading an Avatar #
     * An avatar may have many representations varying in complexity
     * and fidelity. You can select among these capabilities
     * by setting _creationInfo flags before calling [CreateEntity()](@ref OvrAvatarEntity.CreateEntity).
     *
     * You can load the avatar's assets from LoadUser to download the user's custom avatar.
     * You can also load from [zip files](@ref LoadAssetsFromZipSource),
     * [streaming assets](@ref LoadAssetsFromStreamingAssets] or
     * [directly from memory](@ref LoadAssetsFromData) from within your subclass
     * to provide application-specific avatar loading.
     *
     * The avatar's assets are loaded in the background so your application
     * will not display the avatar immediately after load. The @ref IsPendingAvatar
     * flag is set when loading starts and cleared when it has finished.
     */
    public partial class OvrAvatarEntity : MonoBehaviour
    {
        // If any variable is used across these files, it should be placed in this file

        //:: Constants
        private const string logScope = "entity";

        protected const CAPI.ovrAvatar2EntityFeatures UPDATE_POSE_FEATURES =
            CAPI.ovrAvatar2EntityFeatures.AnalyticIk |
            CAPI.ovrAvatar2EntityFeatures.Animation;
        protected const CAPI.ovrAvatar2EntityFeatures UPDATE_MOPRHS_FEATURES =
            CAPI.ovrAvatar2EntityFeatures.Animation |
            CAPI.ovrAvatar2EntityFeatures.UseDefaultFaceAnimations;

        //:: Internal Structs & Classes
        protected readonly struct SkeletonJoint : IComparable<SkeletonJoint>
        {
            public readonly string name;
            public readonly Transform transform;
            public readonly int parentIndex;
            public readonly CAPI.ovrAvatar2NodeId nodeId;

            public SkeletonJoint(string jointName, Transform tx, int parentIdx, CAPI.ovrAvatar2NodeId nodeIdentifier)
            {
                name = jointName;
                transform = tx;
                parentIndex = parentIdx;
                nodeId = nodeIdentifier;
            }

            public SkeletonJoint(in SkeletonJoint oldJoint, string newName, int parentIdx)
                : this(newName, oldJoint.transform, parentIdx, oldJoint.nodeId)
            { }

            public int CompareTo(SkeletonJoint other)
            {
                return other.parentIndex - parentIndex;
            }
        }

        // TODO: This should just include LodCost?
        protected sealed class PrimitiveRenderData : IDisposable
        {
            public PrimitiveRenderData(CAPI.ovrAvatar2NodeId mshNodeId
                , CAPI.ovrAvatar2Id primId
                , CAPI.ovrAvatar2PrimitiveRenderInstanceID renderInstId
                , OvrAvatarRenderable ovrRenderable, OvrAvatarPrimitive ovrPrim)
            {
                meshNodeId = mshNodeId;
                primitiveId = primId;
                instanceId = renderInstId;
                renderable = ovrRenderable;
                skinnedRenderable = ovrRenderable as OvrAvatarSkinnedRenderable;
                primitive = ovrPrim;
            }

            public readonly CAPI.ovrAvatar2NodeId meshNodeId;
            public readonly CAPI.ovrAvatar2Id primitiveId;
            public readonly CAPI.ovrAvatar2PrimitiveRenderInstanceID instanceId;
            public readonly OvrAvatarRenderable renderable;
            public readonly OvrAvatarSkinnedRenderable skinnedRenderable;
            public readonly OvrAvatarPrimitive primitive;

            public bool IsValid => instanceId != CAPI.ovrAvatar2PrimitiveRenderInstanceID.Invalid;

            public void Dispose()
            {
                if (renderable != null) { renderable.Dispose(); }
            }

            public override string ToString()
            {
                return $"nodeId:{meshNodeId},primId:{primitiveId},instId:{instanceId},renderable:{renderable.name},prim:{primitive.name}";
            }
        }

        //:: Public Properties

        public bool HasJoints => SkeletonJointCount > 0;

        // TODO: Remove and replace IsCreated and IsLoading with corresponding checks of LoadState?
        public bool IsCreated => entityId != CAPI.ovrAvatar2EntityId.Invalid;

        /// True if the model assets have been loaded, and are currently being applied to the avatar.
        public bool IsApplyingModels { get; private set; }

        /// True if the assets for an avatar are still loading.
        public bool IsPendingAvatar
        {
            get => IsPendingCdnAvatar || IsPendingZipAvatar;
        }

        /// True if the avatar is still loading the default avatar.
        public bool IsPendingDefaultModel { get; private set; }

        /// True if the avatar is still loading assets from a ZIP file.
        public bool IsPendingZipAvatar { get; private set; }

        /// True if the avatar is still loading assets from a CDN.
        public bool IsPendingCdnAvatar { get; private set; }

        /// True if the avatar has any avatar loaded other than the default avatar.
        public bool HasNonDefaultAvatar { get; private set; }

        /// Number of joints in the avatar skeleton.
        public int SkeletonJointCount => _skeleton.Length;

        // TODO: what does "active" mean?
        /// True if this entity is active.
        public bool EntityActive
        {
            get => _lastActive;
            set => _nextActive = value;
        }

        // External classes or overrides can add to this Action as needed.
        public event Action<bool> OnCulled;

        // The list of child GPU instances of the avatar
        private readonly List<GPUInstancedAvatar> GPUInstances = new List<GPUInstancedAvatar>();

        // Used to track fast loading of presets to ensure correct cleanup
        private readonly List<string> fastLoadPresetPaths = new List<string>();

        // Used to track when entity is loading full preset
        private bool isLoadingFullPreset = false;

        //:: Serialized Fields
        /**
         * Avatar creation configuration to use when creating this avatar.
         * Specifies overall level of detail, body parts to display,
         * rendering and animation characteristics and the avatar's point of view.
         *
         * If you are instantiating the same avatar asset more than once in the same
         * scene, you must use the same *renderFilter* for both. Using different
         * render filters on the same avatar asset is not supported.
         *
         * @see CAPI.ovrAvatar2EntityCreateInfo
         * @see CAPI.ovrAvatar2EntityLODFlags
         * @see CAPI.ovrAvatar2EntityManifestationFlags
         * @see CAPI.ovrAvatar2EntityFeatures
         * @see CAPI.ovrAvatar2EntityFilters
         */
        [Tooltip("Include the largest set of options that will be needed at runtime.")]
        [SerializeField]
        protected CAPI.ovrAvatar2EntityCreateInfo _creationInfo = new CAPI.ovrAvatar2EntityCreateInfo()
        {
            features = CAPI.ovrAvatar2EntityFeatures.Preset_Default,
            renderFilters = new CAPI.ovrAvatar2EntityFilters
            {
                lodFlags = CAPI.ovrAvatar2EntityLODFlags.All,
                manifestationFlags = CAPI.ovrAvatar2EntityManifestationFlags.Half,
                viewFlags = CAPI.ovrAvatar2EntityViewFlags.All,
                subMeshInclusionFlags = CAPI.ovrAvatar2EntitySubMeshInclusionFlags.All,
                highQualityFlags = CAPI.ovrAvatar2EntityHighQualityFlags.None
            }
        };

        [SerializeField]
        private CAPI.ovrAvatar2EntityViewFlags _activeView = CAPI.ovrAvatar2EntityViewFlags.FirstPerson;
        [SerializeField]
        private CAPI.ovrAvatar2EntityManifestationFlags _activeManifestation = CAPI.ovrAvatar2EntityManifestationFlags.Half;

        [Tooltip("These flags allow the original sub-meshes used to create the avatar to be enbled in the index buffer. This only works in Unity Editor. To use this for production, modify the CreationInfo.RenderFilters.SubMeshInclusionFlags above.")]
        [SerializeField]
        private CAPI.ovrAvatar2EntitySubMeshInclusionFlags _activeSubMeshes = CAPI.ovrAvatar2EntitySubMeshInclusionFlags.All;

        [Tooltip("If new sub-mesh types are introduced after this version of the SDK, theis flag determines if they are visible by default. If excluding only one mesh it's best to heep this on. If including ony one mesh it's best to keep this off.")]
        [SerializeField]
        private bool _activeSubMeshesIncludeUntyped = true;

        [SerializeField]
        private CAPI.ovrAvatar2EntityHighQualityFlags _activeHighQuality = CAPI.ovrAvatar2EntityHighQualityFlags.None;

        // Tracking
        [Header("Tracking Input")]
        [SerializeField]
        private OvrAvatarBodyTrackingBehavior _bodyTracking;

        [FormerlySerializedAs("_faceTrackingBehavior")]
        [SerializeField]
        private OvrAvatarFacePoseBehavior _facePoseBehavior;

        [FormerlySerializedAs("_eyeTrackingBehavior")]
        [SerializeField]
        private OvrAvatarEyePoseBehavior _eyePoseBehavior;

        [SerializeField]
        private OvrAvatarLipSyncBehavior _lipSync;

        [Header("Skeleton Output")]

        [Tooltip("Joints which will always have their Unity.Transform updated")]
        [SerializeField]
        protected CAPI.ovrAvatar2JointType[] _criticalJointTypes = Array.Empty<CAPI.ovrAvatar2JointType>();

        [Header("Experimental Features",order=30)]
        [SerializeField]
        [Tooltip("Enable experimental features on this Entity")]
        private DefaultableBool _useExperimentalFeatures = DefaultableBool.Default;

        // TODO: Decouple experimental systems from features once bugs are resolved
        protected bool UseExperimentalFeatures => _useExperimentalFeatures.GetValue(OvrAvatarManager.Instance.UseExperimentalSystems);

        private uint[] _unityUpdateJointIndices = Array.Empty<uint>(); // names with missing/inactive joints removed

        //:: Protected Variables
        protected internal virtual Transform _baseTransform => transform;

        protected CAPI.ovrAvatar2EntityId entityId { get; private set; } = CAPI.ovrAvatar2EntityId.Invalid;

        internal CAPI.ovrAvatar2EntityId internalEntityId => entityId;

        protected UInt64 _userId = 0;

        protected CAPI.ovrAvatar2EntityViewFlags ActiveView { get; set; }

        //:: Private Variables

        // Coroutine is started on OvrAvatarManager to prevent early stops from disabling the Monobehavior
        private Coroutine _loadingRoutineBacking;
        private Coroutine _loadingRoutine
        {
            get { return _loadingRoutineBacking; }
            set
            {
                if (_loadingRoutineBacking != null)
                {
                    if (value != null)
                    {
                        OvrAvatarLog.LogError("More than one loading function running simultaneously!", logScope, this);
                    }
                    StopCoroutine(_loadingRoutineBacking);
                }

                _loadingRoutineBacking = value;
            }
        }

        // Mappings
        protected PrimitiveRenderData[][] _visiblePrimitiveRenderers = Array.Empty<PrimitiveRenderData[]>();

        // TODO: This likely has no utility anymore? It's a mirror of _meshNodes w/ a different key
        protected readonly Dictionary<CAPI.ovrAvatar2PrimitiveRenderInstanceID, PrimitiveRenderData[]> _primitiveRenderables
            = new Dictionary<CAPI.ovrAvatar2PrimitiveRenderInstanceID, PrimitiveRenderData[]>();

        // Render Data
        private SkeletonJoint[] _skeleton = Array.Empty<SkeletonJoint>();

        private readonly Dictionary<CAPI.ovrAvatar2NodeId, uint> _nodeToIndex = new Dictionary<CAPI.ovrAvatar2NodeId, uint>();
        private readonly Dictionary<CAPI.ovrAvatar2JointType, CAPI.ovrAvatar2NodeId> _jointTypeToNodeId = new Dictionary<CAPI.ovrAvatar2JointType, CAPI.ovrAvatar2NodeId>();

        private bool _lastActive = true;
        private bool _nextActive = true;

        // Suppress "never used" warning in non-editor builds
#pragma warning disable 0169
        // Suppress "never assigned to" warning
#pragma warning disable 0649
        [System.Serializable]
        private struct DebugDrawing
        {
            public bool drawTrackingPose;
            public bool drawBoneNames;
            public bool drawSkelHierarchy;
            public bool drawSkelHierarchyInGame;
            public bool drawSkinTransformsInGame;
            public bool drawCriticalJoints;
            public Color skeletonColor;
        }

        [Header("Debug")]

        [SerializeField]
        private DebugDrawing _debugDrawing = new DebugDrawing
        { skeletonColor = Color.red };
#pragma warning restore 0649
#pragma warning restore 0169

        public bool TrackingPoseValid
        {
            get
            {
                if (IsCreated)
                {
                    var result = CAPI.ovrAvatar2Tracking_GetPoseValid(entityId, out var isValid);
                    if (result == CAPI.ovrAvatar2Result.Success)
                    {
                        return isValid;
                    }
                    OvrAvatarLog.LogError($"ovrAvatar2Tracking_GetPoseValid failed with {result}");
                    return false;
                }
                return false;
            }
        }

        private MaterialPropertyBlock _gpuInstanceMaterialProperties = null;

        /////////////////////////////////////////////////
        //:: Core Unity Functions

        #region Core Unity Functions

        protected virtual void Awake()
        {
            CreateEntity();

#if UNITY_EDITOR
#if UNITY_2019_3_OR_NEWER
            SceneView.duringSceneGui += OnSceneGUI;
#else
            SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Camera.onPostRender += OnCameraPostRender;
#endif
            InitAvatarLOD();
        }

        public void UpdateLODInternal()
        {
            if (!isActiveAndEnabled || !IsCreated)
            {
                return;
            }

            Profiler.BeginSample("OvrAvatarEntity::UpdateLODInternal");

            UpdateAvatarLODOverride();

            SendImportanceAndCost();

            Profiler.EndSample();
        }

        // Non-virtual update run before virtual method
        internal void PreSDKUpdateInternal(bool active, ref NativeArray<CAPI.ovrAvatar2Transform> transforms, uint nextTransformIndex)
        {
            // State validation
            // Validate sync of tracked state
            OvrAvatarLog.AssertConstMessage(active == _lastActive
                , "Inconsistent activity state", logScope, this);

            // Apply C# active/inactive state to nativeSDK
            if (_lastActive != _nextActive)
            {
                if (CAPI.ovrAvatar2Entity_SetActive(entityId, _nextActive)
                    .EnsureSuccess("ovrAvatar2Entity_SetActive", logScope, this))
                {
                    _lastActive = _nextActive;
                }
            }

            // If active, apply C# state changes to nativeSDK
            unsafe
            {
                void* transformsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(transforms);
                UnsafeUtility.WriteArrayElement<CAPI.ovrAvatar2Transform>(transformsPtr, (int)nextTransformIndex, _baseTransform.ToWorldOvrTransform().ConvertSpace());
            }
        }

        internal void PostSDKUpdateInternal(bool active)
        {
            // TODO: Notify other systems of state change?
            _lastActive = _nextActive = active;
        }

        // This behaviour is manually updated at a specific time during OvrAvatarManager::Update()
        // to prevent issues with Unity script update ordering
        internal void UpdateInternal(float dT)
        {
            if (!isActiveAndEnabled || !IsCreated) { return; }

            Profiler.BeginSample("OvrAvatarEntity::UpdateInternal");

            if (IsApplyingModels || IsPendingAvatar)
            {
                Profiler.BeginSample("OvrAvatarEntity::UpdateLoadingStatus");
                // TODO: What other errors should we be checking for?
                var result = entityStatus;

                // Occurs when spec download fails
                if (result == CAPI.ovrAvatar2Result.DataNotAvailable
                // Occurs when failing to parse glb asset
                    || result == CAPI.ovrAvatar2Result.InvalidData
                // Occurs when CDN download fails
                    || result == CAPI.ovrAvatar2Result.Unknown)
                {
#pragma warning disable 618
                    LoadState = LoadingState.Failed;
#pragma warning restore 618
                    IsApplyingModels = false;
                }
                Profiler.EndSample();
            }

            // Skip active render state updates if the entity is not active
            if (EntityActive)
            {
                Profiler.BeginSample("OvrAvatarEntity::EntityActiveRenderUpdate");
                EntityActiveRenderUpdate(dT);
                Profiler.EndSample();
            }

            // Only apply render updates if created and not loading
            if (IsCreated)
            {
                Profiler.BeginSample("OvrAvatarEntity::PerFrameRenderUpdate");
                PerFrameRenderUpdate(dT);
                Profiler.EndSample();
            }

            if (!IsLocal && useRenderLods && !IsApplyingModels)
            {
                Profiler.BeginSample("OvrAvatarEntity::ComputeNetworkLod");
                ComputeNetworkLod();
                Profiler.EndSample();
            }

            // accessing obsolete members
#pragma warning disable 618
            if (_lastInvokedLoadState != LoadState)
            {
                _lastInvokedLoadState = LoadState;

                LoadingStateChanged.Invoke(LoadState);
                EntityLoadingStateChanged.Invoke(this);
            }
#pragma warning restore 618

            Profiler.EndSample(); // "OvrAvatarEntity::UpdateInternal"
        }

        protected void OnDestroy()
        {
            bool shouldTearDown = IsCreated || CurrentState != AvatarState.None || _skeleton.Length > 0 ||
                                  IsApplyingModels || _loadingRoutine != null || _jointMonitor != null ||
                                  !(_avatarLOD is null);
            if (shouldTearDown)
            {
                Teardown();
            }
            OnDestroyCalled();

#if UNITY_EDITOR
#if UNITY_2019_3_OR_NEWER
            SceneView.duringSceneGui -= OnSceneGUI;
#else
            SceneView.onSceneGUIDelegate -= OnSceneGUI;
#endif
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Camera.onPostRender -= OnCameraPostRender;
#endif
        }

        protected virtual void OnDestroyCalled() { }


#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // Allows the tracking to up updated in the editor.
            SetBodyTracking(_bodyTracking);

            SetFacePoseProvider(_facePoseBehavior);
            SetEyePoseProvider(_eyePoseBehavior);

            SetLipSync(_lipSync);
            if (IsCreated)
            {
                SetActiveManifestation(_activeManifestation);
                SetActiveView(_activeView);
                SetActiveSubMeshInclusion(_activeSubMeshes);
                SetActiveHighQuality(_activeHighQuality);
                Hidden = Hidden;
            }
        }
#endif // UNITY_EDITOR

        #endregion

        /////////////////////////////////////////////////
        //:: Protected Functions

        #region Life Cycle

        /**
         * Initializes this OvrAvatarEntity instance.
         * An avatar may have many representations varying in complexity
         * and fidelity. These are described by @ref CAPI.ovrAvatar2EntityCreateInfo.
         * The value of @ref OvrAvatarEntity._creationInfo specifies the
         * avatar configuration to create this entity.
         *
         * You can customize entity creation and settings by extending this class and overriding
         * [ConfigureCreationInfo](@ref OvrAvatarEntity.ConfigureCreationInfo) or
         * [ConfigureEntity](@ref OvrAvatarEntity.ConfigureEntity)
         * @see CAPI.ovrAvatar2EntityCreateInfo
         * @see CAPI.ovrAvatar2EntityFeatures
         * @see CAPI.ovrAvatar2EntityManifestationFlags
         * @see CAPI.ovrAvatar2EntityViewFlags
         */
        protected void CreateEntity()
        {
            if (IsCreated)
            {
                OvrAvatarLog.LogWarning("Setup called on an entity that has already been set up.", logScope, this);
                return;
            }
            if (_material == null)
            {
                _material = new OvrAvatarMaterial();
            }

            var createInfoOverride = ConfigureCreationInfo();
            if (createInfoOverride.HasValue)
            {
                _creationInfo = createInfoOverride.Value;
            }

            if (UseExperimentalFeatures)
            {
                AddExperimentalFeatureFlags(ref _creationInfo.features);
            }

            IsPendingDefaultModel = HasAllFeatures(CAPI.ovrAvatar2EntityFeatures.UseDefaultModel);
            ValidateSkinningType();
            SetRequiredFeatures();

            entityId = CreateNativeEntity(in _creationInfo);
            if (entityId == CAPI.ovrAvatar2EntityId.Invalid)
            {
                OvrAvatarLog.LogError("Failed to create entity pointer.", logScope, this);
                IsPendingDefaultModel = false;
                return;
            }

            OvrAvatarManager.Instance.AddTrackedEntity(this);

#pragma warning disable 618
            LoadState = LoadingState.Created;
#pragma warning restore 618

            IsApplyingModels = false;
            CurrentState = AvatarState.Created;
            InvokeOnCreated();

            SetActiveView(_activeView);
            SetActiveManifestation(_activeManifestation);
            SetActiveSubMeshInclusion(_activeSubMeshes);
            SetActiveHighQuality(_activeHighQuality);

            if (IsLocal)
            {
                ConfigureLocalAvatarSettings();
            }
            else
            {
                SetStreamingPlayback(true);
            }

            OvrAvatarLog.LogDebug($"Entity {entityId} created as a {(IsLocal ? "local" : "remote")} avatar", logScope, this);

            SetBodyTracking(_bodyTracking);
            SetFacePoseProvider(_facePoseBehavior);
            SetEyePoseProvider(_eyePoseBehavior);
            SetLipSync(_lipSync);

            ConfigureEntity();

            // For right now, only GPU skinning supports motion smoothing
            if (UseGpuSkinning && UseMotionSmoothingRenderer)
            {
                var entityAnimator = new EntityAnimatorMotionSmoothing(this);

                _entityAnimator = entityAnimator;
                _interpolationValueProvider = entityAnimator;
            }
            else
            {
                _entityAnimator = new EntityAnimatorDefault(this);
            }

            if (UseGpuSkinning)
            {
                if (UseMotionSmoothingRenderer)
                {
                    _jointMonitor = OvrAvatarManager.Instance.UseCriticalJointJobs
                        ? new OvrAvatarEntitySmoothingJointJobMonitor(this, _interpolationValueProvider)
                        : new OvrAvatarEntitySmoothingJointMonitor(this, _interpolationValueProvider);
                }
                else
                {
                    _jointMonitor = new OvrAvatarEntityJointMonitor(this);
                }
            }
            else
            {
                // Only GpuSkinning supports MotionSmoothing, if we didn't/can't use that - indicate that it is off
                MotionSmoothingSettings = MotionSmoothingOptions.FORCE_OFF;
            }
        }

        /**
         * To entity settings, extend this class and override ConfigureEntity
         *
         * @code
         * class MyAvatar : public OvrAvatarEntity
         * {
         *     protected override void ConfigureEntity()
         *     {
         *
         *         // ex: set to third person
         *         SetActiveView(...);
         *         SetActiveManifestation(...);
         *         SetActiveSubMeshInclusion(...);
         *         SetBodyTracking(...);
         *         SetLipSync(...);
         *     }
         * @endcode
         *
        **/
        protected virtual void ConfigureEntity()
        {
            OvrAvatarLog.LogDebug("Base ConfigureEntity Invoked", logScope, this);
        }

        /**
         * To configure creation info on an entity, extend this class and override ConfigureCreationInfo
         *
         * @code
         * class MyAvatar : public OvrAvatarEntity
         * {
         *     protected override void ConfigureCreationInfo()
         *     {
         *        return new CAPI.ovrAvatar2EntityCreateInfo
         *           {
         *               features = CAPI.ovrAvatar2EntityFeatures.Preset_Default,
         *               renderFilters = new CAPI.ovrAvatar2EntityFilters
         *               {
         *                   lodFlags = CAPI.ovrAvatar2EntityLODFlags.All,
         *                   manifestationFlags = CAPI.ovrAvatar2EntityManifestationFlags.Half,
         *                   viewFlags = CAPI.ovrAvatar2EntityViewFlags.All,
         *                   subMeshInclusionFlags = CAPI.ovrAvatar2EntitySubMeshInclusionFlags.All
         *               }
         *           };
         *      }
         * };
         *
         *  It is necessary to set all of the members of
         *  @ref ovrAvatar2EntityCreateInfo. The default values are unspecified.
         * @endcode
         *
        **/
        protected virtual CAPI.ovrAvatar2EntityCreateInfo? ConfigureCreationInfo()
        {
            OvrAvatarLog.LogDebug("Base ConfigureCreationInfo Invoked", logScope, this);
            return null;
        }

        protected static void AddExperimentalFeatureFlags(ref CAPI.ovrAvatar2EntityFeatures featureFlags)
        {
            const CAPI.ovrAvatar2EntityFeatures reservedBit = (CAPI.ovrAvatar2EntityFeatures)1;
            featureFlags |= ~(CAPI.ovrAvatar2EntityFeatures.All | reservedBit);
        }

        public class GPUInstancedAvatar : MonoBehaviour
        {
            public Transform parentTransform = null;
            // TODO: this can be passed to the class as a configuration parameter
            private readonly Matrix4x4 _reflectionMatrix = new Matrix4x4(
                new Vector4(-1.0f, 0.0f, 0.0f, 0.0f),
                new Vector4(0.0f, 1.0f, 0.0f, 0.0f),
                new Vector4(0.0f, 0.0f, 1.0f, 0.0f),
                new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

            public void SetTransform(Vector3 position, Quaternion rotation)
            {
                transform.localPosition = position;

                // copy scale and rotation from the parent
                transform.localScale = parentTransform.localScale;
                transform.rotation = parentTransform.rotation;

                transform.rotation *= rotation;
            }

            public Matrix4x4 GetTransform()
            {
                return _reflectionMatrix * transform.localToWorldMatrix;
            }
        }

        public GameObject CreateGPUInstance()
        {
            // Will be used later during rendering
            if (_gpuInstanceMaterialProperties == null)
            {
                _gpuInstanceMaterialProperties = new MaterialPropertyBlock();
            }

            SetActiveView(CAPI.ovrAvatar2EntityViewFlags.ThirdPerson);
            var instance = new GameObject($"{name}_gpu_instance_{GPUInstances.Count}");
            var gpuInstancedAvatar = instance.AddComponent<GPUInstancedAvatar>();
            GPUInstances.Add(gpuInstancedAvatar);
            return instance;
        }

        public bool DestroyGPUInstance(GameObject instance)
        {
            OvrAvatarLog.Assert(instance != null, logScope, this);
            var gpuInstancedAvatar = instance.GetComponent<GPUInstancedAvatar>();
            if (gpuInstancedAvatar == null)
            {
                OvrAvatarLog.LogError("Attempted to destroy GPUInstance which does not have a GPUInstancedAvatar component", logScope, instance);
                return false;
            }
            return DestroyGPUInstance(gpuInstancedAvatar);
        }

        public bool DestroyGPUInstance(GPUInstancedAvatar instance)
        {
            OvrAvatarLog.Assert(instance != null, logScope, this);
            if (!GPUInstances.Remove(instance))
            {
                OvrAvatarLog.LogError($"Passed instance {instance} doesn't belong to current OvrAvatarEntity", logScope, instance);
                return false;
            }

            GPUInstancedAvatar.Destroy(instance);
            return true;
        }

        private OvrAvatarSkinnedRenderable findActiveRenderable()
        {
            OvrAvatarSkinnedRenderable activeRenderable = null;
            foreach (var keyval in _skinnedRenderables)
            {
                OvrAvatarSkinnedRenderable renderable = keyval.Value;
                if (renderable.isActiveAndEnabled)
                {
                    activeRenderable = renderable;
                    break;
                }
            }

            return activeRenderable;
        }

        private void UpdateGPUInstances()
        {
            if (GPUInstances.Count == 0)
            {
                return;
            }

            Profiler.BeginSample("OvrAvatarEntity::UpdateGPUInstances");

            var renderable = findActiveRenderable();
            if (renderable == null) { return; }

            renderable.GetRenderParameters(out var mesh, out var material, out var transform, _gpuInstanceMaterialProperties);
            if (mesh == null || material == null || transform == null)
            {
                return;
            }

            foreach (var instance in GPUInstances)
            {
                // TODO: this loop can be replaced with calling DrawMeshInstanced for batched GPU instancing
                // but as of 2/16/2022 this doesn't seem to work with the material we're using, perhaps
                // something is making it incompatible with batched instancing
                // Update parent transform so rotation and scale can be copied
                instance.parentTransform = transform;
                Graphics.DrawMesh(mesh, instance.GetTransform(), material, 0, null, 0, _gpuInstanceMaterialProperties);
            }

            Profiler.EndSample();
        }

        private readonly List<OvrAvatarSkinnedRenderable> _perFrameRenderableCache = new List<OvrAvatarSkinnedRenderable>();
        private void PerFrameRenderUpdate(float dT)
        {
            Debug.Assert(IsCreated);

            // Find the visible skinned renderables to update. Note if the renderers all have valid animation data
            _perFrameRenderableCache.Clear();
            var activeAndEnabledRenderables = _perFrameRenderableCache;
            bool doAllRenderersHaveValidAnimationData = true;
            foreach (var primRenderables in _visiblePrimitiveRenderers)
            {
                foreach (var primRenderable in primRenderables)
                {
                    var skinnedRenderable = primRenderable.skinnedRenderable;
                    if (skinnedRenderable is null || !skinnedRenderable.isActiveAndEnabled) { continue; }

                    activeAndEnabledRenderables.Add(skinnedRenderable);

                    if (doAllRenderersHaveValidAnimationData && !skinnedRenderable.IsAnimationDataCompletelyValid)
                    {
                        // Not valid animation data, thus all renderers' data is not valid
                        doAllRenderersHaveValidAnimationData = false;
                    }
                }
            }

            Profiler.BeginSample("OvrAvatarEntity::UpdateAnimationTime");
            _entityAnimator.UpdateAnimationTime(dT, doAllRenderersHaveValidAnimationData);
            Profiler.EndSample();

            Profiler.BeginSample("OvrAvatarEntity::JointMonitor::UpdateJoints");
            _jointMonitor?.UpdateJoints(dT);
            Profiler.EndSample();

            BroadcastPerFrameRenderUpdateToVisibleRenderables(activeAndEnabledRenderables);
            UpdateGPUInstances();
        }

        private void BroadcastPerFrameRenderUpdateToVisibleRenderables(in List<OvrAvatarSkinnedRenderable> visibleRenderables)
        {
            foreach (var skinnedRenderable in visibleRenderables)
            {
                skinnedRenderable.RenderFrameUpdate();
            }
        }

        private void EntityActiveRenderUpdate(float dT)
        {
            Debug.Assert(EntityActive && IsCreated);

            // Check that our pose is valid
            if (!QueryEntityPose(out var entityPose, out var hierarchyVersion)) { return; }
            OvrAvatarLog.Assert(hierarchyVersion != CAPI.ovrAvatar2HierarchyVersion.Invalid, logScope, this);

            if (!QueryEntityRenderState(out var renderState)) { return; }
            OvrAvatarLog.Assert(renderState.allNodesVersion != CAPI.ovrAvatar2EntityRenderStateVersion.Invalid, logScope, this);
            OvrAvatarLog.Assert(renderState.visibleNodesVersion != CAPI.ovrAvatar2EntityRenderStateVersion.Invalid, logScope, this);
            OvrAvatarLog.AssertConstMessage(hierarchyVersion == renderState.hierarchyVersion
                , "hierarchyVersion mismatch", logScope, this);

            // TODO: Checking this isn't really part of "RenderUpdate", move outside this method
            if (_currentHierarchyVersion != hierarchyVersion)
            {
                // Verify an update to this version isn't already in progress
                if (_targetHierarchyVersion != hierarchyVersion)
                {
                    // HACK: If a model is currently being applied, wait for it to finish to avoid race conditions
                    if (IsApplyingModels)
                    {
                        // TODO: Cancel current skeleton update if one is in progress
                        return;
                    }
                    // END-HACK: If a model is currently being applied, wait for it to finish to avoid race conditions
                    // Refresh current Unity version to match the current runtime version
                    QueueUpdateSkeleton(hierarchyVersion);
                }

                // We can not update our current render objects w/ the native runtime state (out of sync)
                // Leave Unity in previous state (freeze render updates)
                return;
            }

            // TODO: We shouldn't need to stall here, this matches "legacy" behavior more accurately though
            if (_currentVisibleNodesVersion != renderState.visibleNodesVersion)
            {
                LoadSync_CheckForNewRenderables_Internal(in renderState, out var result);

                if (result.newPrimitiveIds != null)
                {
                    // Something went wrong or we're loading primitives, block for primitive update
                    QueueBuildPrimitives();

                    // Leave previous state (freeze render updates)
                    return;
                }

                // TODO: Build Renderables before primitives finish loading?
                if (result.newRenderIndices != null)
                {
                    // Build new renderables synchronously and render this frame
                    // TODO: Might be worth timeslicing?
                    using (var newIdxArray = new NativeArray<UInt32>(result.newRenderIndices.Count
                        , Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                    {
                        newIdxArray.CopyFrom(result.newRenderIndices);

                        LoadSync_BuildPrimitives_Internal(in renderState
                            , newIdxArray, result.targetVisVersion);
                    }
                }

                // Update visibility flags
                UpdateVisibility(in renderState);
            }

            // Clean up nodes which are no longer in use
            // TODO: This likely isn't the best timing for this?
            UpdateAllNodes(in renderState);

            Profiler.BeginSample("OvrAvatarEntity::OnActiveRender");
            OnActiveRender(in renderState, in entityPose, hierarchyVersion, dT);
            Profiler.EndSample();
        }

        private void QueueUpdateSkeleton(CAPI.ovrAvatar2HierarchyVersion hierarchyVersion)
        {
            _targetHierarchyVersion = hierarchyVersion;

            OvrAvatarLog.Assert(!IsApplyingModels);
            // Reset load state
#pragma warning disable 618
            LoadState = LoadingState.Loading;
#pragma warning restore 618
            IsApplyingModels = true;

            OvrAvatarManager.Instance.QueueLoadAvatar(this, () =>
            {
                _loadingRoutine = OvrAvatarManager.Instance.StartCoroutine(LoadAsync_BuildSkeletonAndPrimitives());
            });
        }
        private void QueueBuildPrimitives()
        {
            OvrAvatarLog.Assert(!IsApplyingModels);
            // Reset load state
#pragma warning disable 618
            LoadState = LoadingState.Loading;
#pragma warning restore 618
            IsApplyingModels = true;

            OvrAvatarManager.Instance.QueueLoadAvatar(this, () =>
            {
                _loadingRoutine = OvrAvatarManager.Instance.StartCoroutine(LoadAsync_BuildPrimitives());
            });
        }

        // TODO*: Better naming when the lifecycle of the entity is fixed properly
        private void OnActiveRender(in CAPI.ovrAvatar2EntityRenderState renderState
            , in CAPI.ovrAvatar2Pose entityPose
            , CAPI.ovrAvatar2HierarchyVersion hierVersion
            , float dT)
        {
            OvrAvatarLog.AssertConstMessage(IsCreated
                , "OnActiveRender while not created", logScope, this);

            OvrAvatarLog.Assert(_currentHierarchyVersion == hierVersion, logScope, this);

            _entityAnimator.AddNewAnimationFrame(
                Time.time,
                dT,
                entityPose,
                renderState);

            // Call obsolete function
#pragma warning disable 618
            Profiler.BeginSample("OvrAvatarEntity::DidRender");
            DidRender();
            Profiler.EndSample();
#pragma warning restore 618
        }

        [Obsolete("DidRender is obsolete.", false)]
        protected virtual void DidRender() { }

        /**
         * Tear down this entity, stop all tracking and rendering
         * and free associated memory.
         * @see CreateEntity
         */
        public void Teardown()
        {
            System.Diagnostics.Debug.Assert(entityId != CAPI.ovrAvatar2EntityId.Invalid);

            if (CurrentState != AvatarState.None)
            {
                InvokePreTeardown();
            }

            // If not, we are shutting down and will skip some steps
            bool hasManagerInstance = OvrAvatarManager.hasInstance;

            OvrAvatarLog.LogDebug($"Tearing down entity {entityId}", logScope, this);
            if (_loadingRoutine != null)
            {
                StopCoroutine(_loadingRoutine);
                _loadingRoutine = null;

                if (hasManagerInstance)
                {
                    OvrAvatarManager.Instance.FinishedAvatarLoad();
                }
            }

            if (hasManagerInstance)
            {
                OvrAvatarManager.Instance.RemoveLoadRequests(this);
                if (IsApplyingModels)
                {
                    OvrAvatarManager.Instance.RemoveQueuedLoad(this);
                }

                if (IsCreated)
                {
                    if (!IsLocal)
                    {
                        SetStreamingPlayback(false);
                    }

                    OvrAvatarManager.Instance.RemoveTrackedEntity(this);
                }
            }

            if (IsCreated)
            {
                var didDestroy = DestroyNativeEntity();
                if (!didDestroy)
                {
                    OvrAvatarLog.LogWarning("Failed to destroy native entity", logScope, this);
                }
            }

#pragma warning disable 618
            LoadState = LoadingState.NotCreated;
#pragma warning restore 618
            IsApplyingModels = false;
            CurrentState = AvatarState.None;

            ResetLODRange();

            if (!(_avatarLOD is null))
            {
                TeardownLodCullingPoints();
                ShutdownAvatarLOD();
                _avatarLOD = null;
            }

            // TODO: These will get destroyed w/ LODObjects - though being explicit would be nice
            // This trips an error in Unity currently,
            /* "Can't destroy Transform component of 'S0_L2_M1_V1_optimized_geom,0'.
             * If you want to destroy the game object, please call 'Destroy' on the game object instead.
             * Destroying the transform component is not allowed." */
            //for (int i = 0; i < _primitiveRenderables.Length; ++i)
            //{
            //    ref readonly var primRend = ref _primitiveRenderables[i];
            //    DestroyRenderable(in primRend);
            //}
            _visiblePrimitiveRenderers = Array.Empty<PrimitiveRenderData[]>();
            _skinnedRenderables.Clear();
            _meshNodes.Clear();

            _jointMonitor?.Dispose();
            _jointMonitor = null;

            DestroySkeleton();

            _currentVisibleNodesVersion = _targetVisibleNodesVersion = CAPI.ovrAvatar2EntityRenderStateVersion.Invalid;
            _currentAllNodesVersion = _targetAllNodesVersion = CAPI.ovrAvatar2EntityRenderStateVersion.Invalid;
            _currentHierarchyVersion = _targetHierarchyVersion = CAPI.ovrAvatar2HierarchyVersion.Invalid;
        }

        #endregion

        #region Asset Loading Requests

        /**
         * Load avatar assets from a block of data.
         * @param data         C++ pointer to asset data.
         * @param size         byte size of asset data block.
         * @param callbackName name of function to call after data has loaded.
         * @see LoadAssetsFromZipSource
         */
        protected void LoadAssetsFromData(IntPtr data, UInt32 size, string callbackName)
        {
            if (!IsCreated)
            {
                OvrAvatarLog.LogError("Cannot load assets before entity has been created.", logScope, this);
                return;
            }

            CAPI.ovrAvatar2Result result = CAPI.OvrAvatarEntity_LoadMemoryWithFilters(entityId, data, size, callbackName, _creationInfo.renderFilters, out var loadRequestId);
            if (result.IsSuccess())
            {
                ClearFailedLoadState();
                OvrAvatarManager.Instance.RegisterLoadRequest(this, loadRequestId);
            }
            else
            {
                OvrAvatarLog.LogError($"Failed to load asset from data of size {size}. {result}", logScope, this);
            }
        }

        /**
         * Load avatar assets from a block of data.
         * @param data         byte array containing asset data.
         * @param callbackName name of function to call after data has loaded
         * @see LoadAssetsFromZipSource
         */
        protected void LoadAssetsFromData(byte[] data, string callbackName)
        {
            if (!IsCreated)
            {
                OvrAvatarLog.LogError("Cannot load assets before entity has been created.", logScope, this);
                return;
            }

            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            LoadAssetsFromData(handle.AddrOfPinnedObject(), (UInt32)data.Length, callbackName);
            handle.Free();
        }

        #endregion

        #region Getters/Setters

        /**
         * Gets the current avatar view flags (first person, third person).
         * It is possible for multiple flags to be true at once.
         * @returns CAPI.ovrAvatar2EntityViewFlags designating avatar viewpoint.
         * @see CAPI.ovrAvatar2EntityViewFlags
         * @see SetActiveView
         */
        protected CAPI.ovrAvatar2EntityViewFlags GetActiveView() => _activeView;

        /**
         * Selects the avatar viewpoint (first person, third person).
         * @param CAPI.ovrAvatar2EntityViewFlags designating avatar viewpoint.
         * @see CAPI.ovrAvatar2EntityViewFlags
         * @see GetActiveView
         */
        protected void SetActiveView(CAPI.ovrAvatar2EntityViewFlags view)
        {
            var result = CAPI.ovrAvatar2Entity_SetViewFlags(entityId, _hidden ? CAPI.ovrAvatar2EntityViewFlags.None : view);
            if (result.IsSuccess())
            {
                _activeView = view;
            }
            else
            {
                OvrAvatarLog.LogError($"SetViewFlags Failed: {result}");
            }
        }

        /**
         * Gets the body parts included for this avatar.
         * It is possible for multiple manifestations to be active at once
         * @returns CAPI.ovrAvatar2EntityManifestationFlags designating body parts.
         * @see CAPI.ovrAvatar2EntityManifestationFlags
         * @see SetActiveManifestation
         */
        protected CAPI.ovrAvatar2EntityManifestationFlags GetActiveManifestation() => _activeManifestation;

        /**
         * Selects the body parts to include for this avatar.
         * Loading the changes will be triggered on the next Update
         * @param CAPI.ovrAvatar2EntityManifestationFlags designating avatar body parts to include
         * @see CAPI.ovrAvatar2EntityManifestationFlags
         * @see GetActiveManifestation
         */
        protected void SetActiveManifestation(CAPI.ovrAvatar2EntityManifestationFlags manifestation)
        {
            if (IsCreated)
            {
                var result = CAPI.ovrAvatar2Entity_SetManifestationFlags(entityId, manifestation);
                if (result.IsSuccess())
                {
                    _activeManifestation = manifestation;
                }
                else
                {
                    OvrAvatarLog.LogError($"SetManifestationFlags Failed: {result}");
                }
            }
            else
            {
                _activeManifestation = manifestation;
            }
        }

        protected void SetActiveSubMeshInclusion(CAPI.ovrAvatar2EntitySubMeshInclusionFlags subMeshInclusion)
        {
            var result = CAPI.ovrAvatar2Entity_SetSubMeshInclusionFlags(entityId, subMeshInclusion);
            if (!result.EnsureSuccess("ovrAvatar2Entity_SetSubMeshInclusionFlags", logScope, this))
            {
                _activeSubMeshes = subMeshInclusion;

                // TODO: change this to a class member with a mx number of meshes
                List<UnityEngine.Rendering.SubMeshDescriptor> subMeshDescriptors = new List<UnityEngine.Rendering.SubMeshDescriptor>(64);
                foreach (PrimitiveRenderData[] renderDatas in _visiblePrimitiveRenderers)
                {
                    foreach (PrimitiveRenderData renderData in renderDatas)
                    {
                        OvrAvatarRenderable renderable = renderData.renderable;
                        MeshRenderer renderer = renderable.rendererComponent as MeshRenderer;
                        if (renderer != null)
                        {
                            MeshFilter filter = renderer.GetComponent<MeshFilter>();
                            Mesh mesh = filter.sharedMesh;
#if SUBMESH_DEBUGGING
                            OvrAvatarLog.LogInfo("BEFORE MeshInfo: " + mesh.triangles.Length + " triangles, " + mesh.subMeshCount + " submesh count", logScope);
                            for (int i = 0; i < mesh.subMeshCount; i++) {
                                UnityEngine.Rendering.SubMeshDescriptor desc = mesh.GetSubMesh(i);
                                OvrAvatarLog.LogInfo("BEFORE SubMeshInfo[" + i + "]: " + desc.indexStart + ", " + desc.indexCount, logScope);
                            }
#endif
                            int originalNumberIndices = renderable.originalNumberIndices;
                            UInt16[] originalIndexBuffer = renderable.originalIndexBuffer;
                            if (originalNumberIndices <= 0 && mesh.triangles.Length > 0)
                            {
                                renderable.originalNumberIndices = mesh.triangles.Length;
                                renderable.originalIndexBuffer = Array.ConvertAll(mesh.triangles, item => (UInt16)item);

                                originalNumberIndices = renderable.originalNumberIndices;
                                originalIndexBuffer = renderable.originalIndexBuffer;
                            }

                            if (originalNumberIndices <= 0)
                            {
                                // we can't continue at this point, it may be indicative that the model is still loading/unloaded
                            }
                            else if ((subMeshInclusion & CAPI.ovrAvatar2EntitySubMeshInclusionFlags.All) == CAPI.ovrAvatar2EntitySubMeshInclusionFlags.All)
                            {
                                int unitySubMeshIndex = 0;
                                UnityEngine.Rendering.SubMeshDescriptor desc = new UnityEngine.Rendering.SubMeshDescriptor(0, originalNumberIndices);
                                mesh.SetIndexBufferParams(originalNumberIndices, UnityEngine.Rendering.IndexFormat.UInt16);
                                mesh.SetIndexBufferData<UInt16>(originalIndexBuffer, 0, 0, originalNumberIndices);
                                mesh.subMeshCount = 1;
                                mesh.SetTriangles(originalIndexBuffer, unitySubMeshIndex);
                                mesh.SetSubMesh(unitySubMeshIndex, desc);
                            }
                            else
                            {
                                // each primitive has it's own submeshes so do a for loop on the subMeshes
                                uint avatarSdkSubMeshCount = 0;
                                var countResult = CAPI.ovrAvatar2Primitive_GetSubMeshCount(renderData.primitiveId, out avatarSdkSubMeshCount);
                                if (countResult.IsSuccess())
                                {
                                    unsafe
                                    {
                                        int totalSubMeshBufferSize = 0;

                                        for (uint subMeshIndex = 0; subMeshIndex < avatarSdkSubMeshCount; subMeshIndex++)
                                        {

                                            CAPI.ovrAvatar2PrimitiveSubmesh subMesh;
                                            var subMeshResult = CAPI.ovrAvatar2Primitive_GetSubMeshByIndex(renderData.primitiveId, subMeshIndex, out subMesh);
                                            if (subMeshResult.IsSuccess())
                                            {
                                                CAPI.ovrAvatar2EntitySubMeshInclusionFlags localInclusionFlags = subMesh.inclusionFlags;
                                                if (!(localInclusionFlags == CAPI.ovrAvatar2EntitySubMeshInclusionFlags.All && !_activeSubMeshesIncludeUntyped) && (localInclusionFlags & subMeshInclusion) != 0)
                                                {
                                                    UnityEngine.Rendering.SubMeshDescriptor desc = new UnityEngine.Rendering.SubMeshDescriptor((int)subMesh.indexStart, (int)subMesh.indexCount);
                                                    subMeshDescriptors.Add(desc);
                                                    totalSubMeshBufferSize += desc.indexCount;
                                                }
                                            }
                                        }

                                        mesh.SetIndexBufferParams(originalNumberIndices, UnityEngine.Rendering.IndexFormat.UInt16);
                                        UInt16[] indexBufferCopy;
                                        {
                                            indexBufferCopy = new UInt16[totalSubMeshBufferSize];
                                            // Workaround because Sub Mesh API is not working as planned...
                                            int subMeshDestination = 0;
                                            for (int unitySubMeshIndex = 0; unitySubMeshIndex < subMeshDescriptors.Count; unitySubMeshIndex++)
                                            {
                                                var desc = subMeshDescriptors[unitySubMeshIndex];
                                                for (int indexNumber = desc.indexStart; indexNumber < desc.indexCount + desc.indexStart; indexNumber++)
                                                {
                                                    indexBufferCopy[subMeshDestination] = renderable.originalIndexBuffer[indexNumber];
                                                    subMeshDestination++;
                                                }
                                            }
                                        }
                                        subMeshDescriptors.Clear();

                                        mesh.subMeshCount = 1;
                                        mesh.SetIndexBufferData<UInt16>(indexBufferCopy, 0, 0, totalSubMeshBufferSize);
                                        mesh.SetTriangles(indexBufferCopy, 0);
                                    }
                                }
                            }
#if SUBMESH_DEBUGGING
                            OvrAvatarLog.LogInfo("AFTER MeshInfo: " + mesh.triangles.Length + " triangles, " + mesh.subMeshCount + " submesh count", logScope);
                            for (int i = 0; i < mesh.subMeshCount; i++)
                            {
                                UnityEngine.Rendering.SubMeshDescriptor desc = mesh.GetSubMesh(i);
                                OvrAvatarLog.LogInfo("AFTER SubMeshInfo[" + i + "]: " + desc.indexStart + ", " + desc.indexCount, logScope);
                            }
#endif
                        }
                    }
                }
            }
        }

        /**
         * Gets the current avatar high quality flags (normal map, hair map).
         * It is possible for multiple flags to be true at once.
         * @returns CAPI.ovrAvatar2EntityHighQualityFlags designating avatar choices.
         * @see CAPI.ovrAvatar2EntityHighQualityFlags
         * @see SetActiveHighQualityFlags
         */
        protected CAPI.ovrAvatar2EntityHighQualityFlags GetActiveHighQuality() => _activeHighQuality;

        /**
         * Selects the high quality flags (normal map, hair map).
         * @param CAPI.ovrAvatar2EntityHighQualityFlags designating avatar viewpoint.
         * @see CAPI.ovrAvatar2EntityViewFlags
         * @see GetActiveHighQualityFlags
         */
        protected void SetActiveHighQuality(CAPI.ovrAvatar2EntityHighQualityFlags highQuality)
        {
            var result = CAPI.ovrAvatar2Entity_SetHighQualityFlags(entityId, highQuality);
            if (result.IsSuccess())
            {
                _activeHighQuality = highQuality;

                // TODO: Should we iterate over all the renderers here the way the submeshes do?
                // TODO: change this to a class member with a mx number of meshes
                List<UnityEngine.Rendering.SubMeshDescriptor> subMeshDescriptors = new List<UnityEngine.Rendering.SubMeshDescriptor>(64);
                foreach (PrimitiveRenderData[] renderDatas in _visiblePrimitiveRenderers)
                {
                    foreach (PrimitiveRenderData renderData in renderDatas)
                    {
                        OvrAvatarRenderable renderable = renderData.renderable;
                        MeshRenderer renderer = renderable.rendererComponent as MeshRenderer;
                        if (renderer != null)
                        {
                            bool enableNormalMap = (_activeHighQuality & CAPI.ovrAvatar2EntityHighQualityFlags.NormalMaps) != 0;
                            bool enablePropertyHairMap = (_activeHighQuality & CAPI.ovrAvatar2EntityHighQualityFlags.PropertyHairMap) != 0;
                            if (enableNormalMap)
                            {
                                renderer.sharedMaterial.EnableKeyword("HAS_NORMAL_MAP_ON");
                                renderer.sharedMaterial.SetFloat("HAS_NORMAL_MAP", 1.0f);
                            }
                            else
                            {
                                renderer.sharedMaterial.DisableKeyword("HAS_NORMAL_MAP_ON");
                                renderer.sharedMaterial.SetFloat("HAS_NORMAL_MAP", 0.0f);
                            }

                            if (enablePropertyHairMap)
                            {
                                renderer.sharedMaterial.EnableKeyword("ENABLE_HAIR_ON");
                                renderer.sharedMaterial.SetFloat("ENABLE_HAIR", 1.0f);
                            }
                            else
                            {
                                renderer.sharedMaterial.DisableKeyword("ENABLE_HAIR_ON");
                                renderer.sharedMaterial.SetFloat("ENABLE_HAIR", 0.0f);
                            }
                        }
                    }
                }
            }
            else
            {
                OvrAvatarLog.LogError($"SetHighQualityFlags Failed: {result}");
            }
        }

        #endregion

        #region Misc
        // TODO: Remove pose requirement
        protected string GetNameForNode(CAPI.ovrAvatar2NodeId nodeId, in CAPI.ovrAvatar2Pose tempPose)
        {
            return CAPI.OvrAvatar2Entity_GetNodeName(entityId, nodeId);
        }

        protected CAPI.ovrAvatar2NodeId GetNodeForType(CAPI.ovrAvatar2JointType type)
        {
            if (!_jointTypeToNodeId.TryGetValue(type, out var nodeId))
            {
                return CAPI.ovrAvatar2NodeId.Invalid;
            }
            return nodeId;
        }

        protected uint GetIndexForNode(CAPI.ovrAvatar2NodeId nodeId)
        {
            if (!_nodeToIndex.TryGetValue(nodeId, out var idx))
            {
                OvrAvatarLog.LogError($"Unable to find index for nodeId '{nodeId}'", logScope, this);
                idx = uint.MaxValue;
            }
            return idx;
        }

        protected SkeletonJoint GetSkeletonJoint(int index)
        {
            Debug.Assert(index >= 0,
                $"Index {index} out of range for Skeleton joints. Joint count is {SkeletonJointCount}");

            return GetSkeletonJoint((uint)index);
        }

        protected SkeletonJoint GetSkeletonJoint(uint index)
        {
            Debug.Assert(index < SkeletonJointCount,
                $"Index {index} out of range for Skeleton joints. Joint count is {SkeletonJointCount}");

            return _skeleton[index];
        }

        protected SkeletonJoint? GetSkeletonJoint(CAPI.ovrAvatar2JointType jointType)
        {
            var nodeId = GetNodeForType(jointType);
            if (nodeId == CAPI.ovrAvatar2NodeId.Invalid) { return null; }

            var idx = GetIndexForNode(nodeId);
            return GetSkeletonJoint(idx);
        }

        protected Transform GetSkeletonTransformByType(CAPI.ovrAvatar2JointType jointType)
        {
            // If joint monitor is disabled due to using Unity Skinning,
            // we can still provide the transform from the entity skeleton hierarchy
            if (_jointMonitor != null && _jointMonitor.TryGetTransform(jointType, out var tx))
            {
                return tx;
            }
            else
            {
                return GetSkeletonJoint(jointType)?.transform;
            }
        }

        // NOTE: This does NOT retrieve transforms for Critical Joints when Optimize Critical Joints is enabled
        protected Transform GetSkeletonTxByIndex(int index)
        {
            OvrAvatarLog.Assert(index >= 0);
            return GetSkeletonTxByIndex((uint)index);
        }

        // NOTE: This does NOT retrieve transforms for Critical Joints when Optimize Critical Joints is enabled
        protected Transform GetSkeletonTxByIndex(uint index)
        {
            if (_jointMonitor != null)
            {
                OvrAvatarLog.LogWarning(
                    "Optimize Critical Joints is enabled. GetSkeletonTxByIndex will always return null. Use GetSkeletonTransformByType instead",
                    logScope, this);
                return null;
            }

            OvrAvatarLog.Assert(index < _skeleton.Length);
            var tx = _skeleton[index].transform;
            OvrAvatarLog.Assert(tx != null, logScope, this);
            return tx;
        }

        public bool GetMonitoredPositionAndOrientation(CAPI.ovrAvatar2JointType jointType, out Vector3 position
            , out Quaternion orientation)
        {
            Debug.Assert(_jointMonitor != null);
            if (_jointMonitor != null &&
                _jointMonitor.TryGetPositionAndOrientation(jointType, out position, out orientation))
            {
                return true;
            }
            position = Vector3.zero;
            orientation = Quaternion.identity;
            return false;
        }

        /**
         * Get the current focal point that the avatar is looking at.
         * @returns 3D vector with gaze position.
         */
        public Vector3? GetGazePosition()
        {
            var gazePos = new CAPI.ovrAvatar2Vector3f();
            var result = CAPI.ovrAvatar2Behavior_GetGazePos(entityId, ref gazePos);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                return null;
            }
#if OVR_AVATAR_ENABLE_CLIENT_XFORM
            return gazePos;
#else
            return gazePos.ConvertSpace();
#endif
        }

        /**
         * Determines if this avatar has one or more of the given features.
         *
         * The avatar features are specified when the avatar is created.
         * @param features set of features to check for.
         * @returns true if one of the specified features is set for this avatar.
         * @see CAPI.ovrAvatar2EntityFeatures
         * @see CreateEntity
         * @see CAPI.ovrAvatar2EntityCreateInfo
         */
        public bool HasAnyFeatures(CAPI.ovrAvatar2EntityFeatures features)
        {
            return (_creationInfo.features & features) != 0;
        }


        /**
         * Determines if this avatar has all of the given features.
         *
         * The avatar features are specified when the avatar is created.
         * @param features set of features to check for.
         * @returns true if all of the specified features are set for this avatar.
         * @see CAPI.ovrAvatar2EntityFeatures
         * @see CreateEntity
         * @see CAPI.ovrAvatar2EntityCreateInfo
         */
        public bool HasAllFeatures(CAPI.ovrAvatar2EntityFeatures features)
        {
            return (_creationInfo.features & features) == features;
        }

        private bool ForceEnableFeatures(CAPI.ovrAvatar2EntityFeatures features)
        {
            var newFeatures = _creationInfo.features | features;
            bool didAdd = newFeatures != _creationInfo.features;
            if (didAdd)
            {
                OvrAvatarLog.LogVerbose($"Force enabling features {features & ~_creationInfo.features}", logScope, this);

                _creationInfo.features = newFeatures;
            }
            return didAdd;
        }

        private bool ForceDisableFeatures(CAPI.ovrAvatar2EntityFeatures features)
        {
            var newFeatures = _creationInfo.features & ~features;
            bool didRemove = newFeatures != _creationInfo.features;
            if (didRemove)
            {
                OvrAvatarLog.LogVerbose($"Force disabling features {features & _creationInfo.features}", logScope, this);

                _creationInfo.features = newFeatures;
            }
            return didRemove;
        }

        #endregion

        /////////////////////////////////////////////////
        //:: Private Functions

        #region Private Helpers

        private bool QueryEntityPose(out CAPI.ovrAvatar2Pose entityPose, out CAPI.ovrAvatar2HierarchyVersion poseVersion)
        {
            var result = CAPI.ovrAvatar2Entity_GetPose(entityId, out entityPose, out poseVersion);
            if (!result.IsSuccess())
            {
                OvrAvatarLog.LogError($"Entity_GetPose {result}", logScope, this);
                return false;
            }

            return true;
        }

        private bool QueryEntityRenderState(out CAPI.ovrAvatar2EntityRenderState renderState)
        {
            if (!HasAnyFeatures(CAPI.ovrAvatar2EntityFeatures.Rendering_Prims))
            {
                renderState = default;
                return false;
            }

            var result = CAPI.ovrAvatar2Render_QueryRenderState(entityId, out renderState);
            if (!result.IsSuccess())
            {
                OvrAvatarLog.LogError($"QueryRenderState Error: {result}", logScope, this);
                return false;
            }

            return true;
        }

        private bool QueryPrimitiveRenderState(int index, out CAPI.ovrAvatar2PrimitiveRenderState renderState)
        {
            if (index < 0 || index >= primitiveRenderCount)
            {
                renderState = default;
                OvrAvatarLog.LogError(
                    $"IndexOutOfRange. Tried {index} when _primitiveRenderCount is {primitiveRenderCount}");
                return false;
            }

            return QueryPrimitiveRenderState_Direct(index, out renderState);
        }

        // No range checking, used while building primitives
        private bool QueryPrimitiveRenderState_Direct(int index, out CAPI.ovrAvatar2PrimitiveRenderState renderState)
        {
            if (index < 0)
            {
                OvrAvatarLog.LogError($"GetPrimitiveRenderStateByIndex Invalid Index: {index}", logScope, this);
                renderState = default;
                return false;
            }
            return QueryPrimitiveRenderState_Direct((uint)index, out renderState);
        }

        private bool QueryPrimitiveRenderState_Direct(uint index, out CAPI.ovrAvatar2PrimitiveRenderState renderState)
        {
            if (!HasAnyFeatures(CAPI.ovrAvatar2EntityFeatures.Rendering_Prims))
            {
                renderState = default;
                return false;
            }

            var result = CAPI.ovrAvatar2Render_GetPrimitiveRenderStateByIndex(entityId, (UInt32)index, out renderState);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"GetPrimitiveRenderStateByIndex Error: {result} at index {index}", logScope, this);
                return false;
            }

            return true;
        }

        #endregion

        #region Tracking

        /**
         * Select the body tracker to use for this avatar.
         *
         * The input body tracker has a *TrackingContext* member
         * which implements the interface between the Avatar SDK
         * and the body tracking implementation.
         *
         * @param bodyTrackingBehavior body tracking implementation.
         * @see OvrAvatarBodyTrackingBehavior
         * @see OvrAvatarBodyTrackingContextBase
         */
        public void SetBodyTracking(OvrAvatarBodyTrackingBehavior bodyTrackingBehavior)
        {
            _bodyTracking = bodyTrackingBehavior;
            if (IsCreated)
            {
                SetBodyTrackingContext(_bodyTracking?.TrackingContext);
            }
        }

        /**
         * Select the face pose to use for this avatar.
         */
        public void SetFacePoseProvider(OvrAvatarFacePoseBehavior facePoseBehavior)
        {
            _facePoseBehavior = facePoseBehavior;
            if (IsCreated)
            {
                SetFacePoseProvider(_facePoseBehavior?.FacePoseProvider);
            }
        }

        /**
         * Select the eye pose to use for this avatar.
         */
        public void SetEyePoseProvider(OvrAvatarEyePoseBehavior eyePoseBehavior)
        {
            _eyePoseBehavior = eyePoseBehavior;
            if (IsCreated)
            {
                SetEyePoseProvider(_eyePoseBehavior?.EyePoseProvider);
            }
        }

        /**
         * Select the lip sync behavior to use for this avatar.
         *
         * The input body tracker has a *LipSyncContext* member
         * which implements the interface between the Avatar SDK
         * and the lip tracking implementation.
         *
         * @param lipSyncBehavior lip sync implementation.
         * @see OvrAvatarLipSyncBehavior
         * @see OvrAvatarLipSyncContextBase
         */
        public void SetLipSync(OvrAvatarLipSyncBehavior lipSyncBehavior)
        {
            _lipSync = lipSyncBehavior;
            if (IsCreated)
            {
                SetLipSyncContext(_lipSync != null ? _lipSync.LipSyncContext : null);
            }
        }

        private void SetBodyTrackingContext(OvrAvatarBodyTrackingContextBase newContext)
        {
            // Special case to reduce overhead
            if (newContext is IOvrAvatarNativeBodyTracking bodyTracking)
            {
                var nativeContext = bodyTracking.NativeDataContext;
                CAPI.ovrAvatar2Tracking_SetBodyTrackingContextNative(entityId, in nativeContext)
                    .EnsureSuccess("ovrAvatar2Tracking_SetBodyTrackingContextNative", logScope, this);
            }
            else
            {
                var dataContext = newContext?.DataContext ?? new CAPI.ovrAvatar2TrackingDataContext();
                CAPI.ovrAvatar2Tracking_SetBodyTrackingContext(entityId, in dataContext)
                    .EnsureSuccess("ovrAvatar2Tracking_SetBodyTrackingContext", logScope, this);
            }
        }

        private void SetFacePoseProvider(OvrAvatarFacePoseProviderBase newProvider)
        {
            // Special case to reduce overhead
            if (newProvider is IOvrAvatarNativeFacePose facePose)
            {
                var nativeProvider = facePose.NativeProvider;
                CAPI.ovrAvatar2Input_SetFacePoseProviderNative(entityId, in nativeProvider)
                    .EnsureSuccess("ovrAvatar2Input_SetFacePoseProviderNative", logScope, this);
            }
            else
            {
                var dataContext = newProvider?.Provider ?? new CAPI.ovrAvatar2FacePoseProvider();
                CAPI.ovrAvatar2Input_SetFacePoseProvider(entityId, in dataContext)
                    .EnsureSuccess("ovrAvatar2Input_SetFacePoseProvider", logScope, this);
            }
        }

        private void SetEyePoseProvider(OvrAvatarEyePoseProviderBase newProvider)
        {
            // Special case to reduce overhead
            if (newProvider is IOvrAvatarNativeEyePose eyePose)
            {
                var nativeProvider = eyePose.NativeProvider;
                CAPI.ovrAvatar2Input_SetEyePoseProviderNative(entityId, in nativeProvider)
                    .EnsureSuccess("ovrAvatar2Input_SetEyePoseProviderNative", logScope, this);
            }
            else
            {
                var dataContext = newProvider?.Context ?? new CAPI.ovrAvatar2EyePoseProvider();
                CAPI.ovrAvatar2Input_SetEyePoseProvider(entityId, in dataContext)
                    .EnsureSuccess("ovrAvatar2Input_SetEyePoseProvider", logScope, this);
            }
        }

        private void SetLipSyncContext(OvrAvatarLipSyncContextBase newContext)
        {
            // Special case to reduce overhead
            if (newContext is OvrAvatarVisemeContext visemeContext)
            {
                var ncb = visemeContext.NativeCallbacks;
                CAPI.ovrAvatar2Tracking_SetLipSyncContextNative(entityId, in ncb)
                    .EnsureSuccess("ovrAvatar2Tracking_SetLipSyncContextNative", logScope, this);
            }
            else
            {
                var dataContext = newContext?.DataContext ?? new CAPI.ovrAvatar2LipSyncContext();
                CAPI.ovrAvatar2Tracking_SetLipSyncContext(entityId, in dataContext)
                    .EnsureSuccess("ovrAvatar2Tracking_SetLipSyncContext", logScope, this);
            }
        }

        #endregion
    }
}
