// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_INTERACTION

using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Assertions;

namespace Meta.Utilities.Input
{
    [DefaultExecutionOrder(-80)]
    public class FromXRHmdDataSource : DataSource<HmdDataAsset>
    {
        [SerializeField]
        private Vector3 m_rootOffset;
        [SerializeField]
        private Vector3 m_rootAngleOffset;

        [Header("XR Data Source")]
        [SerializeField, Interface(typeof(IOVRCameraRigRef))]
        private MonoBehaviour m_cameraRigRef;
        private IOVRCameraRigRef CameraRigRef { get; set; }

        [SerializeField]
        private bool m_processLateUpdates;

        [Header("Shared Configuration")]
        [SerializeField, Interface(typeof(ITrackingToWorldTransformer))]
        private MonoBehaviour m_trackingToWorldTransformer;
        private ITrackingToWorldTransformer TrackingToWorldTransformer { get; set; }

#if HAS_NAUGHTY_ATTRIBUTES
        [NaughtyAttributes.ShowNativeProperty]
#endif
        private Vector3 CurrentRootPosition => DataAsset?.Root.position ?? Vector3.zero;

        public bool ProcessLateUpdates
        {
            get => m_processLateUpdates;
            set => m_processLateUpdates = value;
        }

        private readonly HmdDataAsset m_hmdDataAsset = new();
        private HmdDataSourceConfig m_config;
        private Pose m_poseOffset;

        protected override HmdDataAsset DataAsset => m_hmdDataAsset;

        protected void Awake()
        {
            TrackingToWorldTransformer = m_trackingToWorldTransformer as ITrackingToWorldTransformer;
            CameraRigRef = m_cameraRigRef as IOVRCameraRigRef;

            UpdateConfig();
        }

        protected override void Start()
        {
            this.BeginStart(ref _started, base.Start);
            Assert.IsNotNull(CameraRigRef);
            Assert.IsNotNull(TrackingToWorldTransformer);

            var offset = new Pose(m_rootOffset, Quaternion.Euler(m_rootAngleOffset));
            m_poseOffset = offset;

            UpdateConfig();
            this.EndStart(ref _started);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_started)
            {
                CameraRigRef.WhenInputDataDirtied += HandleInputDataDirtied;
            }
        }

        protected override void OnDisable()
        {
            if (_started)
            {
                CameraRigRef.WhenInputDataDirtied -= HandleInputDataDirtied;
            }

            base.OnDisable();
        }

        private void HandleInputDataDirtied(bool isLateUpdate)
        {
            if (isLateUpdate && !m_processLateUpdates)
            {
                return;
            }
            MarkInputDataRequiresUpdate();
        }

        private HmdDataSourceConfig Config
        {
            get
            {
                if (m_config != null)
                {
                    return m_config;
                }

                m_config = new();

                return m_config;
            }
        }

        private void UpdateConfig()
        {
            Config.TrackingToWorldTransformer = TrackingToWorldTransformer;
        }

        protected override void UpdateData()
        {
            m_hmdDataAsset.Config = Config;

            m_hmdDataAsset.IsTracked = true;
            m_hmdDataAsset.FrameId = Time.frameCount;

            // Convert controller pose from world to tracking space.
            var centerEyeAnchor = CameraRigRef.CameraRig.centerEyeAnchor;
            var pose = new Pose(centerEyeAnchor.position, centerEyeAnchor.rotation);

            pose = Config.TrackingToWorldTransformer.ToTrackingPose(pose);

            PoseUtils.Multiply(pose, m_poseOffset, ref m_hmdDataAsset.Root);
        }

        #region Inject

        public void InjectAllFromOVRControllerHandDataSource(UpdateModeFlags updateMode, IDataSource updateAfter,
            ITrackingToWorldTransformer trackingToWorldTransformer,
            IDataSource<HmdDataAsset> hmdData,
            Vector3 rootOffset, Vector3 rootAngleOffset)
        {
            InjectAllDataSource(updateMode, updateAfter);
            InjectTrackingToWorldTransformer(trackingToWorldTransformer);
            InjectRootOffset(rootOffset);
            InjectRootAngleOffset(rootAngleOffset);
        }

        public void InjectTrackingToWorldTransformer(ITrackingToWorldTransformer trackingToWorldTransformer)
        {
            m_trackingToWorldTransformer = trackingToWorldTransformer as MonoBehaviour;
            TrackingToWorldTransformer = trackingToWorldTransformer;
        }

        public void InjectRootOffset(Vector3 rootOffset)
        {
            m_rootOffset = rootOffset;
        }

        public void InjectRootAngleOffset(Vector3 rootAngleOffset)
        {
            m_rootAngleOffset = rootAngleOffset;
        }

        #endregion
    }
}

#endif
