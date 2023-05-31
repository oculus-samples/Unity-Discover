#if USING_XR_MANAGEMENT && USING_XR_SDK_OCULUS && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

using AOT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Application = UnityEngine.Application;
using Unity.Jobs;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using static Oculus.Avatar2.CAPI;
using Unity.Collections.LowLevel.Unsafe;



#if UNITY_EDITOR
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Oculus.AvatarSDK2.Editor")]
[assembly: InternalsVisibleTo("AvatarSDK.PlayModeTests")]
#endif

/// @file OvrAvatarManager.cs

/**
 * @namespace Oculus
 * Facebook Virtual Reality SDK for Unity.
 */
/**
 * @namespace Oculus.Avatar2
 * Facebook Avatar SDK for Unity.
 * Enables loading and manipulating Facebook avatars
 * in a Unity application.
 */
namespace Oculus.Avatar2
{
    /**
     * @class OvrAvatarManager
     * Configures global options that affect all avatars like
     * tracking, skinning, asset sources and resource limits.
     *
     * There is only one avatar manager per Unity project.
     * It can be updated in the Unity Editor before play
     * and instantiated in prefabs with specific options.
     * - Control system resource usage like how many
     *   avatars loading concurrently, maximum network bandwidth,
     *   how many concurrent loads are permissible
     *   and dynamic streaming constraints.
     * - Specify the locations of assets to preload.
     *   for each platform (Quest, Quest2, Rift).
     * - Select shader and material options.
     * - Choose skinning and tracking behaviours.
     * @see OvrAvatarEntity
     */
    [DefaultExecutionOrder(-1)]
    public partial class OvrAvatarManager : OvrSingletonBehaviour<OvrAvatarManager>
    {
        public const bool IsAndroidStandalone =
#if UNITY_ANDROID && !UNITY_EDITOR
            true;
#else
            false;
#endif

        // Default setting for UseExperimentalSystems
        private const bool ExperimentalSystemsDefaultValue = false;

        public CAPI.ovrAvatar2Platform Platform { get; private set; } = CAPI.ovrAvatar2Platform.Invalid;
        public CAPI.ovrAvatar2ControllerType ControllerType { get; private set; } = CAPI.ovrAvatar2ControllerType.Invalid;

        /**
         * Return codes for @ref UserHasAvatarAsync query.
         */
        public enum HasAvatarRequestResultCode
        {
            UnknownError = 0,
            BadParameter = 1,
            SendFailed = 2,
            RequestFailed = 3,
            RequestCancelled = 4,

            HasNoAvatar = 8,
            HasAvatar = 9,
        }

        /**
         * Return codes for @ref UserHasAvataChangedAsync query.
         */
        public enum HasAvatarChangedRequestResultCode
        {
            UnknownError = 0,
            BadParameter = 1,
            SendFailed = 2,
            RequestFailed = 3,
            RequestCancelled = 4,

            AvatarHasNotChanged = 8,
            AvatarHasChanged = 9,
        }

        /**
         * Encapsulates a mesh from an avatar primitive.
         * @see OnAvatarMeshLoaded
         */
        public struct MeshData
        {
            public MeshData(
                string name,
                int[] indices,
                Vector3[] positions,
                Vector3[] normals,
                Color32[] colors,
                Vector2[] texcoords,
                Vector4[] tangents,
                BoneWeight[] boneweights,
                Matrix4x4[] bindposes)
            {
                Name = name;
                Indices = indices;
                Positions = positions;
                Normals = normals;
                Colors = colors;
                TexCoords = texcoords;
                Tangents = tangents;
                BindPoses = bindposes;
                BoneWeights = boneweights;
            }
            public readonly string Name;
            public readonly int[] Indices;
            public readonly Vector3[] Positions;
            public readonly Vector3[] Normals;
            public readonly Color32[] Colors;
            public readonly Vector2[] TexCoords;
            public readonly Vector4[] Tangents;
            public readonly BoneWeight[] BoneWeights;
            public readonly Matrix4x4[] BindPoses;
        };

        private const string logScope = "manager";
        public static bool initialized { get; private set; }

        [Header("Loading")]

        /**
         * Maximum number of avatars allowed to load concurrently on the main thread.
         * The default is 64.
         */
        [Tooltip("Number of avatars allowed to load concurrently on main thread")]
        public int MaxConcurrentAvatarsLoading = 64;

        /**
         * Number of resources allowed to load concurrently on background threads.
         * Roughly equal to number of cores used. Defaults to 2.
         */
        [Tooltip(@"Number of resources allowed to load concurrently on background threads
Roughly equal to number of cores used")]
        public int MaxConcurrentResourcesLoading = 2;

        [Tooltip(@"Minimum amount of work to run per frame (in milliseconds) while sliced work remains")]
        [SerializeField]
        [Range(2, 12)]
        private int _minSliceWorkPerFrameMS = 3;

        /**
         * Minimum amount of work to run per frame (in milliseconds) while sliced work remains.
         * This serialized value is set when OvrAvatarManager is initialized but can diverge
         * from the current setting. Negative values will skip runtime initialization of this
         * budget and use compile time default.
         */
        public UInt16 MinSliceWorkPerFrameMS
        {
            get => OvrTime.minWorkPerFrameMS;
            set => OvrTime.minWorkPerFrameMS = value;
        }

        private readonly Dictionary<OvrAvatarEntity, Action> _loadActions = new Dictionary<OvrAvatarEntity, Action>();
        private readonly Queue<OvrAvatarEntity> _loadQueue = new Queue<OvrAvatarEntity>();

        private readonly Queue<OvrAvatarResourceLoader> _resourceLoadQueue = new Queue<OvrAvatarResourceLoader>();

        private int _numAvatarsLoading = 0;
        private int _numResourcesLoading = 0;

        [Header("Network")]

        [Tooltip("Maximum concurrent network requests")]
        [SerializeField]
        [Range(1, 64)]
        private Int64 _maxRequests = 16;

        [Tooltip("-1 for no limit")]
        [SerializeField]
        private Int64 _maxSendBytesPerSecond = -1;

        [Tooltip("-1 for no limit")]
        [SerializeField]
        private Int64 _maxReceiveBytesPerSecond = -1;

        [Tooltip("Number of milliseconds to wait for avatar specification request to complete before failing, 0 for no limit, -1 to use default" +
                 "\n avatar specification request are small, and aren't expected to take more than a second typically.")]
        [SerializeField]
        private Int32 _specificationTimeoutMs = -1;

        [Tooltip("Number of milliseconds to wait for asset request to complete before failing, 0 for no limit, -1 to use default" +
                 "\n asset request can take many seconds, normally set to no limit and rely on low bandwidth timeout")]
        [SerializeField]
        private Int32 _assetTimeoutMs = -1;

        [Tooltip("Number of seconds below lowBandwidthBytesPerSecond to trigger timeout, 0 for no bandwidth timeout, -1 to use default")]
        [SerializeField]
        private Int32 _assetLowBandwidthTimeoutSeconds = -1;

        [Tooltip("Bytes per second threshold for low bandwidth timeout, -1 to use default")]
        [SerializeField]
        private Int32 _assetLowBandwidthBytesPerSecond = -1;

        /**
        * A list of zip files to be loaded upon initializing.
        * Filepaths must be relative to the *StreamingAssets* folder, postfix and file extension will be auto applied.
        */
        [Header("Preset Zip Assets")]
        [Tooltip("A list of zip files to be loaded upon initializing." +
                 "\n Filepaths must be relative to the StreamingAssets folder. zipPostfix will be automatically applied.")]

        [SerializeField]
        private List<string> _preloadZipFiles = new List<string>();

        [Tooltip("A list of platform independent zip files to be loaded upon initializing." +
                 "\n Filepaths must be relative to the StreamingAssets folder, postfix will not be applied.")]
        [SerializeField]
        private List<string> _preloadUniversalZipFiles = new List<string>();

        [Tooltip("Platform postfix for avatar .zip files inside Streaming Assets, and for the .glb files themselves inside the zip (in lowercase).  E.g. 'PresetAvatars_Rift.zip', '0_rift.glb'.")]
        [SerializeField]
        private string zipPostfixDefault = "Rift";

        [Tooltip("Platform postfix for avatar .zip files inside Streaming Assets, and for the .glb files themselves inside the zip (in lowercase).  E.g. 'PresetAvatars_Quest.zip', '0_quest.glb'.")]
        [SerializeField]
        private string zipPostfixAndroid = "Quest";

        [Tooltip("Platform postfix for avatar .zip files inside Streaming Assets, and for the .glb files themselves inside the zip (in lowercase).  E.g. 'PresetAvatars_Quest.zip', '0_quest.glb'." +
                 "\n\nNote, Quest and Quest 2 currently share the same set of presets to reduce APK size.")]
        [SerializeField]
        private string zipPostfixQuest2 = "Quest";

        [Tooltip("FastLoad postfix for avatar .zip files inside Streaming Assets, and for the .glb files themselves inside the zip (in lowercase).  E.g. 'PresetAvatars_Fastload.zip', '0_fastload.glb'.")]
        [SerializeField]
        private string zipPostfixFastLoad = "Fastload";

        [Tooltip("Extension of avatar glb files inside preset .zip files")]
        [SerializeField]
        private string zipFileExtension = ".glb";


        [Header("Streaming Assets")]
        [Tooltip("Platform postfix used when loading glb files directly out of Streaming Assets (not in a .zip)" +
                 "\n\nThe full filename will be: (path)_(platform)(quality)(version)(extension), e.g. customavatar_riftv04.glb.zst")]
        [SerializeField]
        private string streamingAssetPostfixDefault = "rift";

        [Tooltip("Platform postfix used when loading glb files directly out of Streaming Assets (not in a .zip)" +
                 "\n\nThe full filename will be: (path)_(platform)(quality)(version)(extension), e.g. customavatar_quest1v04.glb.zst")]
        [SerializeField]
        private string streamingAssetPostfixAndroid = "quest1";

        [Tooltip("Platform postfix used when loading glb files directly out of Streaming Assets (not in a .zip)" +
                 "\n\nThe full filename will be: (path)_(platform)(quality)(version)(extension), e.g. customavatar_quest2v04.glb.zst")]
        [SerializeField]
        private string streamingAssetPostfixQuest2 = "quest2";

        [Tooltip("Fast Load postfix used when loading glb files directly out of Streaming Assets (not in a .zip)" +
                 "\n\nThe full filename will be: (path)_(platform)(quality)(version)(extension), e.g. customavatar_fastloadv04.glb.zst")]
        [SerializeField]
        private string streamingAssetPostfixFastLoad = "fastload";

        [Tooltip("Version suffix (padded to 2 digits) when loading glb files directly out of Streaming Assets (not in a .zip)")]
        [SerializeField]
        private int assetVersionDefault = 4;

        [Tooltip("Version suffix (padded to 2 digits) when loading glb files directly out of Streaming Assets (not in a .zip)")]
        [SerializeField]
        private int assetVersionAndroid = 4;

        [Tooltip("Version suffix (padded to 2 digits) when loading glb files directly out of Streaming Assets (not in a .zip)")]
        [SerializeField]
        private int assetVersionQuest2 = 4;

        [Tooltip("File extension used when loading glb files directly out of Streaming Assets (not in a .zip)")]
        [SerializeField]
        private string streamingAssetFileExtension = ".glb.zst";

        [Header("Required Assets")]
        [Tooltip("Where to find OvrAvatar2Assets.zip inside StreamingAssets")]
        [SerializeField]
        private string ovrAvatar2AssetFolder = "Oculus";

        [Header("Customization")]

        [Tooltip("Color of the default avatar model rendered by ovrAvatar2EntityFeatures.UseDefaultModel")]
        [SerializeField]
        private Color _defaultModelColor = new Color(CAPI.DefaultAvatarColorRed, CAPI.DefaultAvatarColorGreen, CAPI.DefaultAvatarColorBlue);

        [Header("Shaders")]
        /// Shader manager to use for all avatars.
        [SerializeField]
        public OvrAvatarShaderManagerBase ShaderManager;

        [Header("Experimental")]
        [Tooltip("Update critical joints using Unity Transform jobs, may help reduce main thread CPU usage")]
        [SerializeField]
        private bool _useCriticalJointJobs = false;
        [Tooltip("Connect the avatar runtime to the Avatar Monitor pane in Unity to show avatar SDK data")]
        [SerializeField]
        private bool _enableDevTools = false;

        [Tooltip(@"Initially load a lower quality, but significantly faster to load version of the avatar. This will be used until the full quality avatar is loaded.")]
        [SerializeField]
        public bool UseFastLoadAvatar = false;

        [Tooltip(@"Enable experimental systems.")]
        [SerializeField]
        private DefaultableBool _useExperimentalSystems = DefaultableBool.Default;
        
        [Header("Debug")]
        [SerializeField]
        private CAPI.ovrAvatar2LogLevel _ovrLogLevel = CAPI.ovrAvatar2LogLevel.Verbose;

        private readonly List<OvrAvatarEntity> _entityUpdateOrder = new List<OvrAvatarEntity>(16);
        private OvrAvatarEntity[] _entityUpdateArray = Array.Empty<OvrAvatarEntity>();
        private bool _entityUpdateArrayChanged = false;

        private readonly Dictionary<CAPI.ovrAvatar2Id, OvrAvatarAssetBase> _assetMap = new Dictionary<CAPI.ovrAvatar2Id, OvrAvatarAssetBase>();
        private readonly Dictionary<CAPI.ovrAvatar2Id, OvrAvatarResourceLoader> _resourcesByID = new Dictionary<CAPI.ovrAvatar2Id, OvrAvatarResourceLoader>();

        /// Desired level for console logging.
        public CAPI.ovrAvatar2LogLevel ovrLogLevel => _ovrLogLevel;

        /// Effective setting for activating experimental systems, accounting for runtime config and compile time defaults
        public bool UseExperimentalSystems => _useExperimentalSystems.GetValue(ExperimentalSystemsDefaultValue);
        
        public void SetLogLevel(CAPI.ovrAvatar2LogLevel logLevel)
        {
            if (_ovrLogLevel != logLevel)
            {
                _ovrLogLevel = logLevel;
                OvrAvatarLog.logLevel = OvrAvatarLog.GetLogLevel(logLevel);
            }
        }

        /// Maximum number of concurrent network requests.
        /// The value -1 indicates no limit.
        public Int64 MaxRequests => _maxRequests;

        /// Maximum number of bytes to send per second.
        /// The value -1 indicates no limit.
        public Int64 MaxSendBytesPerSecond => _maxSendBytesPerSecond;

        public Int64 MaxReceiveBytesPerSecond => _maxReceiveBytesPerSecond;

        ///  Whether critical joints update using Unity Transform jobs, which may help reduce main thread CPU usage
        public bool UseCriticalJointJobs => _useCriticalJointJobs;

        // Whether to activate the arbiter client sending data
        public bool EnableDevTools => _enableDevTools;

        ///
        public IOvrAvatarHandTrackingDelegate DefaultHandTrackingDelegate { get; private set; }

        ///
        public IOvrAvatarInputTrackingDelegate DefaultInputTrackingDelegate { get; private set; }

        ///
        public IOvrAvatarInputControlDelegate DefaultInputControlDelegate { get; private set; }

        /// Selects the GPU skinning controller to use for all avatars.
        public OvrAvatarGpuSkinningController GpuSkinningController { get; private set; }

        /// Selects the gaze target manager to use for all avatars.
        public OvrAvatarGazeTargetManager GazeTargetManager { get; private set; }


        /// Selects the face tracker to use for all avatars.
        internal OvrAvatarFacePoseProviderBase OvrPluginFacePoseProvider { get; private set; }

        /// Selects the eye tracker to use for all avatars.
        internal OvrAvatarEyePoseProviderBase OvrPluginEyePoseProvider { get; private set; }

        private delegate void RequestDelegate(CAPI.ovrAvatar2Result result, IntPtr userContext);

        private readonly Dictionary<CAPI.ovrAvatar2RequestId, RequestDelegate> _activeRequests =
            new Dictionary<CAPI.ovrAvatar2RequestId, RequestDelegate>();

        #region Lifecycle

        protected override void Initialize()
        {
            Debug.Assert(!initialized);

            // Can't query in static initializer under 2019.4
            // - "UnityException: GetGraphicsShaderLevel is not allowed to be called from a MonoBehaviour constructor
            // (or instance field initializer), call it in Awake or Start instead. Called from MonoBehaviour 'OvrAvatarManager'."
            _shaderLevelSupport = SystemInfo.graphicsShaderLevel;

            ValidateSupportedSkinners();

            if (!OvrTime.HasLimitedBudget)
            {
                if (_minSliceWorkPerFrameMS <= UInt16.MaxValue)
                {
                    OvrTime.minWorkPerFrameMS = (UInt16)_minSliceWorkPerFrameMS;
                }
                else
                {
                    OvrTime.ResetInitialBudget();
                }
            }

            OvrAvatarLog.logLevel = OvrAvatarLog.GetLogLevel(_ovrLogLevel);

            // Used by nativeSDK telemetry to identify source app
            var clientName = $"{Application.companyName}.{Application.productName}";
            var clientAppVersionString = $"{Application.version}+{Application.unityVersion}";

            var platform = GetPlatform();
            OvrAvatarLog.LogInfo($"OvrAvatarManager initializing for app {clientName}::{clientAppVersionString} on platform '{CAPI.ovrAvatar2PlatformToString(platform)}'"
                , logScope, this);

            var initInfo = CAPI.OvrAvatar_DefaultInitInfo(clientAppVersionString, platform);
            {
                initInfo.flags |= CAPI.ovrAvatar2InitializeFlags.EnableSkinningOrigin;
                initInfo.loggingLevel = _ovrLogLevel;
                initInfo.loggingCallback = OvrAvatarLog.LogCallBack;
                initInfo.loggingContext = IntPtr.Zero;
                initInfo.requestCallback = RequestCallback;
                initInfo.resourceLoadCallback = ResourceCallback;
                initInfo.resourceLoadContext = IntPtr.Zero;
                initInfo.fallbackPathToOvrAvatar2AssetsZip = GetAssetPathForFile(ovrAvatar2AssetFolder, true);
                initInfo.numWorkerThreads = 1;
                initInfo.fileOpenCallback = null;
                initInfo.fileReadCallback = null;
                initInfo.fileCloseCallback = null;
                initInfo.fileReaderContext = IntPtr.Zero;
                initInfo.maxNetworkRequests = _maxRequests;
                initInfo.maxNetworkSendBytesPerSecond = _maxSendBytesPerSecond;
                initInfo.maxNetworkReceiveBytesPerSecond = _maxReceiveBytesPerSecond;
                initInfo.defaultModelColor.x = _defaultModelColor.r;
                initInfo.defaultModelColor.y = _defaultModelColor.g;
                initInfo.defaultModelColor.z = _defaultModelColor.b;
                initInfo.clientName = clientName;
                // Transform from SDK space (-Z forward) to Unity space (+Z forward)
#if OVR_AVATAR_ENABLE_CLIENT_XFORM
                initInfo.clientSpaceRightAxis = UnityEngine.Vector3.right;
                initInfo.clientSpaceUpAxis = UnityEngine.Vector3.up;
                initInfo.clientSpaceForwardAxis = UnityEngine.Vector3.forward;
#else
                initInfo.clientSpaceRightAxis = UnityEngine.Vector3.right;
                initInfo.clientSpaceUpAxis = UnityEngine.Vector3.up;
                initInfo.clientSpaceForwardAxis = -UnityEngine.Vector3.forward;
#endif
            }

            if (UseExperimentalSystems
            )
            {
                // All bits used by application during initialization (0-3)
                const ovrAvatar2InitializeFlags appFlags
                    = (ovrAvatar2InitializeFlags)(((int)ovrAvatar2InitializeFlags.Last << 1) - 1);
                // Set of non-debug bits (0-29)
                const ovrAvatar2InitializeFlags allSystemFlags = (ovrAvatar2InitializeFlags)((1 << 29) - 1);

                // Specifics bits to set for experimental features (4-28)
                const ovrAvatar2InitializeFlags experimentalMask = allSystemFlags & ~appFlags;
                initInfo.flags |= experimentalMask;
            }

#if USING_XR_SDK
            OvrAvatarLog.LogInfo($"Attempting to initialize ovrplugintracking lib", logScope, this);
            if (OvrPluginTracking.Initialize(initInfo.loggingCallback, initInfo.loggingContext))
            {

                OvrPluginFacePoseProvider = OvrPluginTracking.CreateFaceTrackingContext();
                if (OvrPluginFacePoseProvider != null)
                {
                    OvrAvatarLog.LogInfo("Created ovrplugintracking face tracking context", logScope, this);
                }
                else
                {
                    OvrAvatarLog.LogInfo("Failed to created ovrplugintracking face tracking context", logScope, this);
                }

                OvrPluginEyePoseProvider = OvrPluginTracking.CreateEyeTrackingContext();
                if (OvrPluginEyePoseProvider != null)
                {
                    OvrAvatarLog.LogInfo("Created ovrplugintracking eye tracking context", logScope, this);
                }
                else
                {
                    OvrAvatarLog.LogInfo("Failed to created ovrplugintracking eye tracking context", logScope, this);
                }

                DefaultHandTrackingDelegate = OvrPluginTracking.CreateHandTrackingDelegate();
                OvrAvatarLog.LogInfo(DefaultHandTrackingDelegate != null
                    ? "Created ovrplugintracking hand tracking delegate"
                    : "Failed to created ovrplugintracking hand tracking delegate", logScope, this);
            }
            else
            {
                OvrAvatarLog.LogInfo("Failed to initialize  ovrplugintracking lib", logScope, this);
            }
#endif

            // This is actually used even w/out GPUSkinning as part of skinning mode selection
            // NOTE: That is rather ugly, make that not the case or atleast rename
            GpuSkinningConfiguration.Instantiate();
            GpuSkinningConfiguration.Instance.ValidateFallbackSkinner(UnitySkinnerSupported, OvrGPUSkinnerSupported);

            // initialize the GPU Skinning dll
            bool gpuSkinningInitSuccess = false;
            if (OvrGPUSkinnerSupported || OvrComputeSkinnerSupported)
            {
                try
                {
                    gpuSkinningInitSuccess = CAPI.OvrGpuSkinning_Initialize();
                }
                catch (DllNotFoundException linkExcpt)
                {
                    OvrAvatarLog.LogError($"DllNotFound, likely UnsatisfiedLinkError: {linkExcpt.Message}", logScope, this);
                }
                catch (Exception unknownExcpt)
                {
                    OvrAvatarLog.LogError($"Exception initializing gpuskinning: {unknownExcpt.Message}", logScope, this);
                }

                if (gpuSkinningInitSuccess)
                {
                    OvrAvatarLog.LogDebug("Initializing GPUSkinning Singletons");
                    GpuSkinningController = new OvrAvatarGpuSkinningController();
                }
                else
                {
                    OvrAvatarLog.LogError("ovrGpuSkinning_Initialize failed", logScope, this);
                }
            }

            bool ovrAvatar2Init = false;
            try
            {
                ovrAvatar2Init = CAPI.OvrAvatar_Initialize(in initInfo);
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError($"Exception calling ovrAvatar2_Initialize: {e}", logScope, this);
            }
            initialized = ovrAvatar2Init;
            if (!initialized)
            {
                OvrAvatarLog.LogError("ovrAvatar2_Initialize Failed", logScope, this);
                return;
            }
            Platform = platform;
            ControllerType = GetControllerType();

            // Enable the tools link so we send data to the Avatar Resource Monitor window
            // TODO: fix root cause of this check causing a crash
            //if (OvrAvatarManager.Instance.EnableDevTools)
            {
                if (CAPI.ovrAvatar2_EnableDevToolsLink() != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogWarning("Failed to enable dev tools link", logScope, this);
                }
            }

            OvrAvatarLog.LogVerbose($"libovravatar2 initialized with version: {CAPI.OvrAvatar_GetVersionString()}", logScope, this);

            AvatarLODManager.Instantiate();

            // TODO: This was done in SampleManager after initialization. What is the effect?
            CAPI.OvrAvatar2_Update();

            //Preload zip files
            foreach (var filePath in _preloadZipFiles)
            {
                AddZipSource(filePath);
            }
            foreach (var uniFilePath in _preloadUniversalZipFiles)
            {
                AddUniversalZipSource(uniFilePath);
            }

            GazeTargetManager = new OvrAvatarGazeTargetManager();

            if (ShaderManager == null)
            {
                OvrAvatarLog.LogWarning("OvrAvatarShaderManager needs to be specified. Otherwise materials cannot be created. Falling back to default manager.", logScope, this);
                ShaderManager = gameObject.AddComponent<OvrAvatarShaderManagerSingle>();
            }

            UpdateNetworkSetting(ref CAPI.SpecificationNetworkSettings.timeoutMS, _specificationTimeoutMs);
            UpdateNetworkSetting(ref CAPI.AssetNetworkSettings.timeoutMS, _assetTimeoutMs);
            UpdateNetworkSetting(ref CAPI.AssetNetworkSettings.lowSpeedTimeSeconds, _assetLowBandwidthTimeoutSeconds);
            UpdateNetworkSetting(ref CAPI.AssetNetworkSettings.lowSpeedLimitBytesPerSecond, _assetLowBandwidthBytesPerSecond);

            OvrAvatarLog.LogInfo($"OvrAvatarManager initialized app with target version: {CAPI.TargetAvatarLibVersionString}", logScope, this);
        }
        private static void UpdateNetworkSetting(ref UInt32 sdkSetting, Int32 serializedValue)
        {
            if (serializedValue >= 0)
            {
                sdkSetting = (uint)serializedValue;
            }
        }

        protected virtual void Update()
        {
            UpdateInternal(Time.deltaTime);
        }

        // Used when updating avatarSDK from a mechanism other than UnityEvent.Update
        public void Step(float deltaSeconds)
        {
            UpdateInternal(deltaSeconds);
        }

        private bool GetActives(in NativeArray<ovrAvatar2EntityId> entityIds, ref NativeArray<bool> actives)
        {
            Debug.Assert(entityIds.Length == actives.Length);
            unsafe
            {
                return CAPI.ovrAvatar2Entity_GetActives((ovrAvatar2EntityId*)entityIds.GetUnsafePtr(), (bool*)actives.GetUnsafePtr(), (uint)entityIds.Length)
                        .EnsureSuccess("ovrAvatar2Entity_GetActives", logScope, this);
            }
        }

        private void UpdateInternal(float deltaSeconds)
        {
            if (!initialized) { return; }
            if (ShaderManager == null)
            {
                OvrAvatarLog.LogDebug("OvrAvatarShaderManager needs to be specified", logScope, this);
                return;
            }
            else if (!ShaderManager.Initialized)
            {
                OvrAvatarLog.LogDebug("Waiting for OvrAvatarShaderManager to finish initialization", logScope, this);
                return;
            }

            Profiler.BeginSample("OvrTime.InternalUpdate");
            OvrTime.InternalUpdate();
            Profiler.EndSample();

            Profiler.BeginSample("GazeTargetManager.Update");
            GazeTargetManager.Update();
            Profiler.EndSample();

            AvatarLODManager.Instance.UpdateInternal();

            if (_entityUpdateArrayChanged)
            {
                Profiler.BeginSample("OvrAvatarManager.entityUpdateArrayChanged");
                _entityUpdateArrayChanged = false;

                var updateCount = _entityUpdateOrder.Count;
                if (_entityUpdateArray.Length != updateCount)
                {
                    Array.Resize(ref _entityUpdateArray, updateCount);
                }
                _entityUpdateOrder.CopyTo(_entityUpdateArray);
                Profiler.EndSample();
            }

            var entityIds = new NativeArray<ovrAvatar2EntityId>(_entityUpdateArray.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var actives = new NativeArray<bool>(_entityUpdateArray.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            unsafe
            {
                var ids = (ovrAvatar2EntityId*)entityIds.GetUnsafePtr();
                for (int i = 0; i < _entityUpdateArray.Length; i++)
                {
                    ids[i] = _entityUpdateArray[i].internalEntityId;
                }
            }

            Profiler.BeginSample("OvrAvatarManager.PreSDKUpdates");
            var transforms = new NativeArray<CAPI.ovrAvatar2Transform>(_entityUpdateArray.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            uint nextTransformIndex = 0;
            if (GetActives(entityIds, ref actives))
            {
                for (int i = 0; i < _entityUpdateArray.Length; i++)
                {
                    _entityUpdateArray[i].PreSDKUpdateInternal(actives[i], ref transforms, nextTransformIndex++);
                }
                unsafe
                {
                    CAPI.ovrAvatar2Entity_SetRoots(entityIds.GetPtr<ovrAvatar2EntityId>(), transforms.GetPtr<CAPI.ovrAvatar2Transform>(), nextTransformIndex);
                }
            }
            Profiler.EndSample();

            Profiler.BeginSample("CAPI.OvrAvatar2_Update");
            CAPI.OvrAvatar2_Update(deltaSeconds);
            Profiler.EndSample(); // "CAPI.OvrAvatar2_Update"

            Profiler.BeginSample("OvrAvatarManager.PostSDKUpdates");
            // DOD to OOP mismatch makes for pain, going to have to read and then write 64 bytes
            // for every single avatar, just to set one bit's worth of info :(
            if (GetActives(entityIds, ref actives))
            {
                for (int i = 0; i < _entityUpdateArray.Length; i++)
                {
                    _entityUpdateArray[i].PostSDKUpdateInternal(actives[i]);
                }
            }
            Profiler.EndSample();

            Profiler.BeginSample("OvrAvatarManager.UpdateLoadRequests");
            if (_loadRequestsChanged)
            {
                // copy to avoid issues with modifying during iteration
                Array.Resize(ref _loadRequestsArray, _loadRequests.Count);
                _loadRequests.CopyTo(_loadRequestsArray);
                _loadRequestsChanged = false;
            }
            foreach (var loadRequest in _loadRequestsArray)
            {
                if (loadRequest.Update())
                {
                    _loadRequests.Remove(loadRequest);
                    _loadRequestsChanged = true;
                }
            }
            Profiler.EndSample();

            Profiler.BeginSample("OvrAvatarManager.UpdateInternals");
            GpuSkinningController?.StartFrame();
            foreach (var trackedEntity in _entityUpdateArray)
            {
                trackedEntity.UpdateInternal(deltaSeconds);
            }
            GpuSkinningController?.EndFrame();
            Profiler.EndSample();

            if (UseCriticalJointJobs)
            {
                // Ensure transform jobs are eligible to be assigned to worker threads
                JobHandle.ScheduleBatchedJobs();
            }

            GpuSkinningController?.UpdateInternal();
            Permission_Update();
        }

        protected override void Shutdown()
        {
            // MUST be set before calling ovrAvatar2_Shutdown, in case any sdk work is being done on another thread
            initialized = false;

            foreach (var entity in _entityUpdateOrder)
            {
                entity.Teardown();
            }
            _entityUpdateOrder.Clear();

            var assets = new OvrAvatarAssetBase[_assetMap.Count];
            _assetMap.Values.CopyTo(assets, 0);
            foreach (var asset in assets)
            {
                asset.Dispose();
            }

            if (GpuSkinningController != null)
            {
                GpuSkinningController.Dispose();
            }

            foreach (var resource in _resourcesByID)
            {
                resource.Value.Dispose();
            }
            _resourcesByID.Clear();

            OvrAvatarCallbackContextBase.DisposeAll();

            OvrAvatarLog.LogInfo("SystemDispose", logScope, this);
            bool shutdownSuccess = CAPI.OvrAvatar_Shutdown();
            if (!shutdownSuccess)
            {
                OvrAvatarLog.LogError($"Failed OvrAvatar_Shutdown", logScope, this);
            }

            OvrPluginTracking.Shutdown();

            if (OvrGPUSkinnerSupported || OvrComputeSkinnerSupported)
            {
                var gpuSkinningShutdownResult = CAPI.OvrGpuSkinning_Shutdown();
                if (!gpuSkinningShutdownResult)
                {
                    OvrAvatarLog.LogError($"Failed to shutdown OvrGpuSkinning", logScope, this);
                }
            }

            _entityUpdateArray = Array.Empty<OvrAvatarEntity>();
            _assetMap.Clear();

            _ShutdownSingleton<GpuSkinningConfiguration>(GpuSkinningConfiguration.Instance);

            _ShutdownSingleton<AvatarLODManager>(AvatarLODManager.Instance);

            GpuSkinningController = null;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
#if !DISABLE_IPC_CONNECTOR
            if (IpcOafConnector.hasInstance)
            {
                IpcOafConnector.Instance.Destroy();
            }
#endif
#endif

            OvrTime.CancelAll();
        }

        #endregion

        #region Public Functions

        internal void AddTrackedEntity(OvrAvatarEntity entity)
        {
            if (!_entityUpdateOrder.Contains(entity))
            {
                _entityUpdateOrder.Add(entity);
                _entityUpdateArrayChanged = true;
            }
        }

        internal void RemoveTrackedEntity(OvrAvatarEntity entity)
        {
            if (_entityUpdateOrder.Contains(entity))
            {
                _entityUpdateOrder.Remove(entity);
                _entityUpdateArrayChanged = true;
            }
        }

        /**
         * Load an additional asset ZIP file into memory.
         * @param file  path to a zip file or a directory
         *              (relative to *StreamingAssets* directory).
         * A platform-specific postfix is added to the name of
         * the ZIP file. This lets you select different assets
         * for different platforms from within the Unity Editor.
         */
        public void AddZipSource(string file)
        {
            string platformPostfix = GetPlatformPostfix(true);
            string fastPostfix = GetFastLoadPostfix(true);

            string filePath;
            if (platformPostfix.Length > 0)
            {
                // Remove extension so we can insert postfix
                filePath = Path.ChangeExtension(file, null);
                filePath += $"_{platformPostfix}.zip";
            }
            else
            {
                // Otherwise, simply ensure the filename ends with ".zip"
                filePath = Path.ChangeExtension(file, "zip");
            }
            filePath = GetAssetPathForFile(filePath);
            AddRawZipSource(filePath);

            string fastPath = Path.ChangeExtension(file, null);
            fastPath += $"_{fastPostfix}.zip";
            fastPath = GetAssetPathForFile(fastPath);
            AddRawZipSource(fastPath);
        }

        /**
         * Load an additional asset ZIP file into memory.
         * @param file  path to a zip file or a directory
         *              (relative to *StreamingAssets* directory).
         * The platform-specific prefix is *not* added.
         * This ZIP file will be available on all platforms.
         */
        public void AddUniversalZipSource(string file)
        {
            file = Path.ChangeExtension(file, "zip");
            file = GetAssetPathForFile(file);
            AddRawZipSource(file);
        }

        private void AddRawZipSource(string filePath)
        {
            CAPI.ovrAvatar2Result result = CAPI.ovrAvatar2_AddZipSourceFile(filePath);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"Error AddZipSourceFile {result}. Path was {filePath}", logScope, this);
            }
            else
            {
                OvrAvatarLog.LogDebug($"Added zip source {filePath}", logScope, this);
            }
        }

        /**
         * Asynchronous request to determine whether a specific user has an avatar.
         * @returns return code indicating the result of the request.
         * @ returns @ ref HasAvatarRequestResultCode - one of:
         *           UnknownError, BadParameter, SendFailed, RequestFailed,
         *           HasNoAvatar or HasAvatar
         * @see HasAvatarRequestResultCode
         */
        public async Task<HasAvatarRequestResultCode> UserHasAvatarAsync(UInt64 userId)
        {
            if (userId == 0)
            {
                OvrAvatarLog.LogError("UserHasAvatarAsync failed: userId must not be 0", logScope, this);
                return HasAvatarRequestResultCode.BadParameter;
            }

            if (!OvrAvatarEntitlement.AccessTokenIsValid())
            {
                OvrAvatarLog.LogError("UserHasAvatarAsync failed: no valid access token", logScope, this);
                return HasAvatarRequestResultCode.BadParameter;
            }

            // Queue request
            bool requestSent = CAPI.OvrAvatar_HasAvatar(userId, out var requestId, IntPtr.Zero);
            if (!requestSent)
            {
                OvrAvatarLog.LogError($"ovrAvatar2_HasAvatar failed to send request", logScope, this);
                return HasAvatarRequestResultCode.SendFailed;
            }

            // Setup request handler
            var completionSource = _RegisterBoolRequestHandler(requestId);

            // Await request completion
            await completionSource.Task;

            var requestResult = completionSource.Task.Result;
            bool requestSuccess = requestResult.requestResult.IsSuccess();
            if (!requestSuccess)
            {
                OvrAvatarLog.LogError($"UserHasAvatarAsync completed the request but the result was {requestResult.requestResult}", logScope, this);
                return HasAvatarRequestResultCode.RequestFailed;
            }

            if (!requestResult.resultBool.HasValue)
            {
                OvrAvatarLog.LogError($"ovrAvatar2_HasAvatar encountered an unexpected error", logScope, this);
                return HasAvatarRequestResultCode.UnknownError;
            }

            if (!requestResult.resultBool.Value)
            {
                OvrAvatarLog.LogWarning($"ovrAvatar2_HasAvatar user has no saved avatar", logScope, this);
                return HasAvatarRequestResultCode.HasNoAvatar;
            }

            return HasAvatarRequestResultCode.HasAvatar;
        }

        internal async Task<HasAvatarChangedRequestResultCode> SendHasAvatarChangedRequestAsync(CAPI.ovrAvatar2EntityId entityId)
        {
            // Queue request
            bool requestSent = CAPI.OvrAvatar2_HasAvatarChanged(entityId, out var requestId, IntPtr.Zero);
            if (!requestSent)
            {
                OvrAvatarLog.LogError($"ovrAvatar2_HasAvatar failed to send request", logScope, this);
                return HasAvatarChangedRequestResultCode.SendFailed;
            }

            // Setup request handler
            var completionSource = _RegisterBoolRequestHandler(requestId);

            // Await request completion
            await completionSource.Task;

            var requestResult = completionSource.Task.Result;
            bool requestSuccess = requestResult.requestResult.IsSuccess();
            if (!requestSuccess)
            {
                OvrAvatarLog.LogError($"ovrAvatar2_HasAvatarChanged completed the request but the result was {requestResult.requestResult}", logScope, this);
                return HasAvatarChangedRequestResultCode.RequestFailed;
            }

            // ReSharper disable once InvertIf
            if (!requestResult.resultBool.HasValue)
            {
                OvrAvatarLog.LogError($"ovrAvatar2_HasAvatarChanged encountered an unexpected error", logScope, this);
                return HasAvatarChangedRequestResultCode.UnknownError;
            }

            return requestResult.resultBool.Value
                ? HasAvatarChangedRequestResultCode.AvatarHasChanged
                : HasAvatarChangedRequestResultCode.AvatarHasNotChanged;
        }

        // TODO: Make static
        private TaskCompletionSource<AvatarRequestBoolResults> _RegisterBoolRequestHandler(CAPI.ovrAvatar2RequestId requestId)
        {
            var completionSource = new TaskCompletionSource<AvatarRequestBoolResults>();
            _activeRequests.Add(requestId,
                (avatarResult, context) =>
                {
                    bool? resultBool = null;
                    if (avatarResult.IsSuccess())
                    {
                        if (CAPI.OvrAvatar_GetRequestBool(requestId, out var requestBool))
                        {
                            resultBool = requestBool;
                        }
                    }

                    completionSource.SetResult(new AvatarRequestBoolResults(avatarResult, resultBool));
                });
            return completionSource;
        }

        // Updates maxNetworkSendBytesPerSecond and maxNetworkReceiveBytesPerSecond

        /**
         * Updates network bandwidth settings.
         * @param newMaxSendBytesPerSecond    Maximum number of bytes to send per second.
         * @param newMaxReceiveBytesPerSecond Maximum number of bytes to receive per second.
         * @param newMaxRequests              Maximum number of concurrent requests.
         * These can also be configured from within the Unity Editor.
         * @see MaxReceiveBytesPerSecond
         * @see MaxSendBytesPerSecond
         * @see MaxRequests
         */
        public void UpdateNetworkSettings(Int64 newMaxSendBytesPerSecond, Int64 newMaxReceiveBytesPerSecond, Int64 newMaxRequests)
        {
            var result = CAPI.ovrAvatar2_UpdateNetworkSettings(newMaxSendBytesPerSecond, newMaxReceiveBytesPerSecond, newMaxRequests);
            if (result.IsSuccess())
            {
                _maxSendBytesPerSecond = newMaxSendBytesPerSecond;
                _maxReceiveBytesPerSecond = newMaxReceiveBytesPerSecond;
                _maxRequests = newMaxRequests;
            }
            else
            {
                OvrAvatarLog.LogError($"ovrAvatar2_UpdateNetworkSettings failed with result {result}");
            }
        }

        /**
         * Query network statistics for all avatars.
         * @param downloadTotalBytes    Gets total bytes downloaded.
         * @param downloadSpeed         Gets download speed (bytes /sec).
         * @param totalRequests         Gets total number of requests.
         * @param activeRequests        Get number of active requests.
         */
        public (UInt64 downloadTotalBytes, UInt64 downloadSpeed, UInt64 totalRequests, UInt64 activeRequests) QueryNetworkStats()
        {
            bool statsSuccess =
                CAPI.OvrAvatar2_QueryNetworkStats(out var stats);

            if (statsSuccess)
            {
                return (stats.downloadTotalBytes, stats.downloadSpeed, stats.totalRequests, stats.activeRequests);
            }
            else
            {
                OvrAvatarLog.LogError("ovrAvatar2_QueryNetworkStats failed");
                return default;
            }
        }

        public string GetPlatformGLBPostfix(bool isFromZip)
        {
            return GetPlatformPostfix(isFromZip).ToLower();
        }

        public string GetFastLoadGLBPostfix(bool isFromZip)
        {
            return isFromZip ? zipPostfixFastLoad : streamingAssetPostfixFastLoad;
        }

        public string GetPlatformGLBVersion(bool highQuality, bool isFromZip)
        {
            if (isFromZip)
            {
                return String.Empty;
            }
            int assetVersion = 0;
            switch (Platform)
            {
                case CAPI.ovrAvatar2Platform.PC:
                    assetVersion = assetVersionDefault;
                    break;
                case CAPI.ovrAvatar2Platform.Quest:
                    assetVersion = assetVersionAndroid;
                    break;
                case CAPI.ovrAvatar2Platform.Quest2:
                    assetVersion = assetVersionQuest2;
                    break;
                case CAPI.ovrAvatar2Platform.QuestPro:
                    assetVersion = assetVersionQuest2;
                    break;
                default:
                    OvrAvatarLog.LogError($"Error unknown platform for version number, using default. Platform was {Platform}.", logScope, this);
                    assetVersion = assetVersionDefault;
                    break;
            }

            return "_" + (highQuality ? "h" : "v") + assetVersion.ToString("00");
        }

        public string GetPlatformGLBExtension(bool isFromZip)
        {
            return isFromZip ? zipFileExtension : streamingAssetFileExtension;
        }

        private static CAPI.ovrAvatar2Platform GetPlatform()
        {
            return IsAndroidStandalone ? GetAndroidStandalonePlatform() : CAPI.ovrAvatar2Platform.PC;
        }

        private CAPI.ovrAvatar2ControllerType GetControllerType()
        {
            OvrAvatarLog.Assert(
                CAPI.ovrAvatar2Platform.First <= Platform && Platform <= CAPI.ovrAvatar2Platform.Last
                , logScope, this);

            switch (Platform)
            {
                case CAPI.ovrAvatar2Platform.PC:
                    return CAPI.ovrAvatar2ControllerType.Rift;
                case CAPI.ovrAvatar2Platform.Quest:
                    return CAPI.ovrAvatar2ControllerType.Touch;
                case CAPI.ovrAvatar2Platform.Quest2:
                    return CAPI.ovrAvatar2ControllerType.Quest2;
                case CAPI.ovrAvatar2Platform.QuestPro:
                    return CAPI.ovrAvatar2ControllerType.QuestPro;


                case CAPI.ovrAvatar2Platform.Num:
                case CAPI.ovrAvatar2Platform.Invalid:
                default:
                    OvrAvatarLog.LogError($"Unable to find controller type for current platform {Platform}", logScope, this);
                    break;
            }
            return CAPI.ovrAvatar2ControllerType.Invalid;
        }

        #endregion

        #region Private Functions

        private string GetPlatformPostfix(bool isFromZip)
        {
            switch (Platform)
            {
                case CAPI.ovrAvatar2Platform.PC:
                    return isFromZip ? zipPostfixDefault : streamingAssetPostfixDefault;
                case CAPI.ovrAvatar2Platform.Quest:
                    return isFromZip ? zipPostfixAndroid : streamingAssetPostfixAndroid;
                case CAPI.ovrAvatar2Platform.Quest2:
                    return isFromZip ? zipPostfixQuest2 : streamingAssetPostfixQuest2;
                case CAPI.ovrAvatar2Platform.QuestPro:
                    return isFromZip ? zipPostfixQuest2 : streamingAssetPostfixQuest2;
                default:
                    OvrAvatarLog.LogError($"Error unknown platform for prefix, using default. Platform was {Platform}.", logScope, this);
                    return IsAndroidStandalone ? (isFromZip ? zipPostfixAndroid : streamingAssetPostfixAndroid) : (isFromZip ? zipPostfixDefault : streamingAssetPostfixDefault);
            }
        }

        private string GetFastLoadPostfix(bool isFromZip)
        {
            return isFromZip ? zipPostfixFastLoad : streamingAssetPostfixFastLoad;
        }


        private string GetAssetPathForFile(string file, bool suppressNonExistentWarning = false)
        {
            if (Application.isEditor)
            {
                var path = Path.Combine(Application.dataPath, "Oculus", "Avatar2", "StreamingAssets", file);
                if (!File.Exists(path))
                {
                    if (!suppressNonExistentWarning)
                    {
                        OvrAvatarLog.LogWarning("Asset doesn't exist: " + path, logScope);
                    }
                }
                return path;
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            return IsAndroidStandalone ? file : Path.Combine(Application.streamingAssetsPath, file);
        }

        private static CAPI.ovrAvatar2Platform GetAndroidStandalonePlatform()
            => GetAndroidStandalonePlatform(SystemInfo.deviceName);


        internal const string NonQuestDeviceLogText = "Identified non-Quest platform";
        internal const string RecognizedQuestDeviceLogText =
            "Identified Quest platform!";
        internal const string UnrecognizedQuestDeviceWarningText =
            "Unrecognized Quest platform, treating as QuestPro. AvatarSDK is likely out of date!";
        internal static CAPI.ovrAvatar2Platform GetAndroidStandalonePlatform(string deviceName)
        {
            deviceName = deviceName.Trim();

            var isQuestDevice = deviceName.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isQuestDevice)
            {
                OvrAvatarLog.LogInfo(NonQuestDeviceLogText);
                // Default to Quest1 settings for unrecognized Android headsets
                OvrAvatarLog.LogVerbose("Reporting as Quest device", logScope);
                return CAPI.ovrAvatar2Platform.Quest;
            }

            var foundType = CAPI.ovrAvatar2Platform.Invalid;

            // Check explicitly for Quest1
            bool isQuest1 = deviceName.EndsWith("Quest", StringComparison.OrdinalIgnoreCase);
            if (isQuest1) { foundType = CAPI.ovrAvatar2Platform.Quest; }


            if (foundType == CAPI.ovrAvatar2Platform.Invalid)
            {
                bool isQuest2 = deviceName.EndsWith("2")
                                && (deviceName.EndsWith("Quest 2", StringComparison.OrdinalIgnoreCase)
                                    || deviceName.EndsWith("Quest2", StringComparison.OrdinalIgnoreCase));
                if (isQuest2) { foundType =  CAPI.ovrAvatar2Platform.Quest2; }
            }

            if (foundType == CAPI.ovrAvatar2Platform.Invalid)
            {
                bool isQuestPro = deviceName.EndsWith("Pro")
                                  && (deviceName.EndsWith("Quest Pro", StringComparison.OrdinalIgnoreCase)
                                      || deviceName.EndsWith("QuestPro", StringComparison.OrdinalIgnoreCase));
                if (isQuestPro) { foundType =  CAPI.ovrAvatar2Platform.QuestPro; }
            }

            if (foundType == CAPI.ovrAvatar2Platform.Invalid)
            {
                // Default to QuestPro settings for any Quest device which isn't Quest1 or Quest2
                OvrAvatarLog.LogWarning(UnrecognizedQuestDeviceWarningText, logScope);
                return CAPI.ovrAvatar2Platform.QuestPro;
            }

            OvrAvatarLog.LogInfo(RecognizedQuestDeviceLogText, logScope);
            OvrAvatarLog.LogVerbose($"Identified Quest platform {foundType}", logScope);
            return foundType;
        }

        #endregion

        #region SDK Callbacks

        /**
         * Event that provides the original untransformed avatar mesh data before skinning deformations are applied.
         * Since each mesh is only loaded once, this function will be called for every level of detail
         * of an avatar even if that avatar is loaded more than once.
         * If OnAvatarMeshLoaded has no listeners, the Unity mesh vertices are loaded into the GPU and discarded.
         * Otherwise, the mesh is provided as an argument to the listener function.
         * The application is responsible for memory management of this mesh data and removing added listener delegates.
         * @param mgr   OvrAvatarManager instance which invoked the event
         * @param prim  OvrAvatarPrimitive describing the avatar SDK mesh
         * @param mesh  MeshData structure containing the data for the mesh.
         *              Not all of these fields will be present. If GPU skinning is used,
         *              the BindPoses field will be null - this data is only present
         *              for Unity skinning. Depending on avatar configuration,
         *              Tangents and Colors might also be omitted.
         */
        public delegate void AvatarMeshLoadHandler(OvrAvatarManager mgr, OvrAvatarPrimitive prim, MeshData mesh);
        public event AvatarMeshLoadHandler OnAvatarMeshLoaded;

        internal bool HasMeshLoadListener => OnAvatarMeshLoaded != null;

        internal void InvokeMeshLoadEvent(OvrAvatarPrimitive prim, MeshData destMesh)
        {
            Debug.Assert(HasMeshLoadListener);
            OnAvatarMeshLoaded?.Invoke(this, prim, destMesh);
        }

        [MonoPInvokeCallback(typeof(CAPI.ResourceDelegate))]
        public static void ResourceCallback(in CAPI.ovrAvatar2Asset_Resource resource, IntPtr context)
        {
            try
            {
                if (!hasInstance) { return; }

                OvrAvatarResourceLoader.ResourceCallbackHandler(
                    in resource,
                    Instance._resourcesByID,
                    out var queueLoad);

                if (queueLoad != null)
                {
                    Instance.QueueResourceLoad(queueLoad);
                }
            }
            catch (Exception e)
            {
                // Catch all exceptions to prevent C# exceptions from propagating to C++ catch handlers
                try
                {
                    OvrAvatarLog.LogError($"{e.Message}\n{e.StackTrace}", logScope, OvrAvatarManager.Instance);
                }
                catch
                {
                    // ignored
                }
            }
        }

        [MonoPInvokeCallback(typeof(CAPI.RequestDelegate))]
        public static void RequestCallback(CAPI.ovrAvatar2RequestId requestId, CAPI.ovrAvatar2Result result, IntPtr userContext)
        {
            try
            {
                if (!hasInstance) { return; }

                if (Instance._activeRequests.TryGetValue(requestId, out var callback))
                {
                    callback.Invoke(result, userContext);
                    Instance._activeRequests.Remove(requestId);
                }
                else
                {
                    OvrAvatarLog.LogError($"Unhandled request with ID {(Int32)requestId} and result {result}", logScope);
                }
            }
            catch (Exception e)
            {
                // Catch all exceptions to prevent C# exceptions from propagating to C++ catch handlers
                try
                {
                    OvrAvatarLog.LogError($"{e.Message}\n{e.StackTrace}", logScope);
                }
                catch
                {
                    // ignored
                }
            }
        }

        #endregion

        #region Asset Loading

        internal static bool IsOvrAvatarAssetLoaded(CAPI.ovrAvatar2Id assetId)
        {
            return OvrAvatarManager.Instance._assetMap.ContainsKey(assetId);
        }

        internal static bool GetOvrAvatarAsset<T>(CAPI.ovrAvatar2Id assetId, out T asset) where T : OvrAvatarAssetBase
        {
            if (Instance._assetMap.TryGetValue(assetId, out var foundAsset))
            {
                asset = foundAsset as T;
                if (asset != null)
                {
                    return true;
                }

                OvrAvatarLog.LogError($"Found asset {assetId}, but it is not of type {typeof(T)}", logScope);
            }

            asset = null;
            return false;
        }

        internal static void AddAsset(OvrAvatarAssetBase newAsset)
        {
            if (!Instance._assetMap.ContainsKey(newAsset.assetId))
            {
                Instance._assetMap.Add(newAsset.assetId, newAsset);
            }
            else
            {
                OvrAvatarLog.LogError($"Tried to add asset {newAsset.assetId}, but there is already a tracked asset with that ID!", logScope);
            }
        }

        internal static void RemoveAsset(OvrAvatarAssetBase oldAsset)
        {
            Debug.Assert(!oldAsset.isLoaded);
            if (!Instance._assetMap.Remove(oldAsset.assetId))
            {
                OvrAvatarLog.LogError($"Tried to remove {oldAsset.assetId}, but it wasn't tracked!", logScope);
            }
        }
        #endregion

        #region Avatar Loading

        // This class tracks LoadRequests that are in progress and detects changes in state in order to trigger callbacks
        // to the OvrAvatarEntity
        private class LoadRequest
        {
            internal OvrAvatarEntity entity;
            internal CAPI.ovrAvatar2LoadRequestId requestId;
            internal CAPI.ovrAvatar2LoadRequestState lastState;

            // Updates from internal CAPI state and triggers callbacks to the OvrAvatarEntity
            // Returns true if the load request is in a terminal state (Success/Failed/Cancelled) or invalid
            internal bool Update()
            {
                CAPI.ovrAvatar2LoadRequestInfo requestInfo;
                if (CAPI.ovrAvatar2Result.Success != CAPI.ovrAvatar2Asset_GetLoadRequestInfo(requestId, out requestInfo))
                {
                    return true;
                }

                if (requestInfo.state != lastState)
                {
                    entity.InvokeOnLoadRequestStateChanged(requestInfo);
                    lastState = requestInfo.state;
                }

                return (requestInfo.state == CAPI.ovrAvatar2LoadRequestState.Success
                        || requestInfo.state == CAPI.ovrAvatar2LoadRequestState.Cancelled
                        || requestInfo.state == CAPI.ovrAvatar2LoadRequestState.Failed);
            }
        }

        private readonly List<LoadRequest> _loadRequests = new List<LoadRequest>();
        private LoadRequest[] _loadRequestsArray = Array.Empty<LoadRequest>();
        private bool _loadRequestsChanged = false;

        // Registers an avatar load request. Active requests must be polled for updates
        internal void RegisterLoadRequest(OvrAvatarEntity entity, CAPI.ovrAvatar2LoadRequestId requestId)
        {
            var request = new LoadRequest();
            request.entity = entity;
            request.requestId = requestId;
            request.lastState = CAPI.ovrAvatar2LoadRequestState.None;

            // Update immediately and call user callback for the initial state. This is done here because
            // the state may change during the next SDK update, before callbacks are called
            if (!request.Update())
            {
                _loadRequests.Add(request);
                _loadRequestsChanged = true;
            }
        }

        internal void RemoveLoadRequests(OvrAvatarEntity entity)
        {
            for (int idx = _loadRequests.Count - 1; idx >= 0; --idx)
            {
                if (_loadRequests[idx].entity == entity)
                {
                    _loadRequests.RemoveAt(idx);
                }
            }
            _loadRequestsChanged = true;
        }

        internal void FinishedAvatarLoad()
        {
            Action nextLoadAction = null;
            while (_loadQueue.Count > 0)
            {
                var e = _loadQueue.Dequeue();
                if (_loadActions.TryGetValue(e, out nextLoadAction))
                {
                    _loadActions.Remove(e);
                    break;
                }
            }

            if (nextLoadAction != null)
            {
                nextLoadAction();
            }
            else
            {
                --_numAvatarsLoading;
            }
        }

        internal void QueueLoadAvatar(OvrAvatarEntity entity, Action loadAction)
        {
            if (_numAvatarsLoading < MaxConcurrentAvatarsLoading)
            {
                ++_numAvatarsLoading;
                loadAction();
                return;
            }

            _loadActions.Add(entity, loadAction);
            _loadQueue.Enqueue(entity);
        }

        internal void RemoveQueuedLoad(OvrAvatarEntity entity)
        {
            if (_loadActions.Remove(entity))
            {
                // Can only remove from front of queue, otherwise we will just ignore the entity when it comes up
                // TODO: May not be worth even bothering with this
                if (_loadQueue.Count > 0 && _loadQueue.Peek() == entity)
                {
                    _loadQueue.Dequeue();
                }
            }
        }

        #endregion // Avatar Loading

        private void QueueResourceLoad(OvrAvatarResourceLoader loader)
        {
            OvrAvatarLog.Assert(!_resourceLoadQueue.Contains(loader), logScope, this);

            if (_numResourcesLoading < MaxConcurrentResourcesLoading)
            {
                ++_numResourcesLoading;
                loader.StartLoad();
            }
            else
            {
                _resourceLoadQueue.Enqueue(loader);
            }
        }
        internal void ResourceLoadComplete(OvrAvatarResourceLoader finishedLoader)
        {
            ResourceLoadEnded(finishedLoader);
        }
        internal void ResourceLoadCancelled(OvrAvatarResourceLoader failedLoader)
        {
            ResourceLoadEnded(failedLoader);
        }
        private void ResourceLoadEnded(OvrAvatarResourceLoader finishedLoader)
        {
            while (_resourceLoadQueue.Count > 0)
            {
                var nextLoader = _resourceLoadQueue.Dequeue();
                if (nextLoader.CanLoad)
                {
                    nextLoader.StartLoad();
                    return;
                }
            }

            --_numResourcesLoading;
        }

        internal Dictionary<CAPI.ovrAvatar2Id, OvrAvatarResourceLoader> GetResourceID()
        {
            return _resourcesByID;
        }

        private void _ShutdownSingleton<T>(T singletonInstance)
            where T : OvrSingletonBehaviour<T>
        {
            _AvatarManagerCheckShutdown(this, singletonInstance);
        }

        private readonly struct AvatarRequestBoolResults
        {
            public AvatarRequestBoolResults(CAPI.ovrAvatar2Result res, bool? resBool)
            { requestResult = res; resultBool = resBool; }

            public readonly CAPI.ovrAvatar2Result requestResult;
            public readonly bool? resultBool;
        }
    }
}
