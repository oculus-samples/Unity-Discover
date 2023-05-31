// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_INTERACTION && HAS_META_AVATARS

using Meta.Utilities;
using Meta.Utilities.Input;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Assertions;

namespace Meta.Utilities.Input
{
    [DefaultExecutionOrder(-80)]
    public class FromXRHandDataSource : DataSource<HandDataAsset>
    {
        [SerializeField]
        private Transform[] m_bones;

        [SerializeField]
        private Vector3 m_rootOffset;
        [SerializeField]
        private Vector3 m_rootAngleOffset;

        [Header("OVR Data Source")]
        [SerializeField, Interface(typeof(IOVRCameraRigRef))]
        private MonoBehaviour m_cameraRigRef;
        private IOVRCameraRigRef CameraRigRef { get; set; }

        [SerializeField]
        private bool m_processLateUpdates;

        [Header("Shared Configuration")]
        [SerializeField]
        private Handedness m_handedness;

        [SerializeField, Interface(typeof(ITrackingToWorldTransformer))]
        private MonoBehaviour m_trackingToWorldTransformer;
        private ITrackingToWorldTransformer TrackingToWorldTransformer { get; set; }

        public bool ProcessLateUpdates
        {
            get => m_processLateUpdates;
            set => m_processLateUpdates = value;
        }

        private readonly HandDataAsset m_handDataAsset = new();
        [AutoSetFromParent]
        [SerializeField] private XRInputManager m_xrInputManager;
        private Transform m_ovrControllerAnchor;
        private HandDataSourceConfig m_config;
        private Pose m_poseOffset;

        public static Quaternion WristFixupRotation { get; } = new(0.0f, 1.0f, 0.0f, 0.0f);

        protected override HandDataAsset DataAsset => m_handDataAsset;

#if HAS_NAUGHTY_ATTRIBUTES
        [NaughtyAttributes.ShowNativeProperty]
#endif
        private Vector3 CurrentRootPosition => DataAsset?.Root.position ?? Vector3.zero;

        private HandSkeleton m_skeleton;

        protected void Awake()
        {
            m_skeleton = HandSkeletonOVR.CreateSkeletonData(m_handedness);
            TrackingToWorldTransformer = m_trackingToWorldTransformer as ITrackingToWorldTransformer;
            CameraRigRef = m_cameraRigRef as IOVRCameraRigRef;

            UpdateConfig();
        }

        protected override void Start()
        {
            this.BeginStart(ref _started, base.Start);
            Assert.IsNotNull(CameraRigRef);
            Assert.IsNotNull(TrackingToWorldTransformer);
            if (m_handedness == Handedness.Left)
            {
                Assert.IsNotNull(CameraRigRef.LeftHand);
                m_ovrControllerAnchor = CameraRigRef.LeftController;
            }
            else
            {
                Assert.IsNotNull(CameraRigRef.RightHand);
                m_ovrControllerAnchor = CameraRigRef.RightController;
            }

            var offset = new Pose(m_rootOffset, Quaternion.Euler(m_rootAngleOffset));
            if (m_handedness == Handedness.Left)
            {
                offset.position.x = -offset.position.x;
                offset.rotation = Quaternion.Euler(180f, 0f, 0f) * offset.rotation;
            }
            m_poseOffset = offset;

            UpdateSkeleton();
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

        private void UpdateSkeleton()
        {
            if (_started)
            {
                for (var i = 0; i < m_skeleton.joints.Length; i++)
                {
                    m_skeleton.joints[i].pose.position = m_bones[i].localPosition;
                    m_skeleton.joints[i].pose.rotation = m_bones[i].localRotation;
                }
            }
        }

        private HandDataSourceConfig Config
        {
            get
            {
                if (m_config != null)
                {
                    return m_config;
                }

                m_config = new HandDataSourceConfig()
                {
                    Handedness = m_handedness
                };

                return m_config;
            }
        }

        private void UpdateConfig()
        {
            Config.Handedness = m_handedness;
            Config.TrackingToWorldTransformer = TrackingToWorldTransformer;
            Config.HandSkeleton = m_skeleton;
        }

        protected override void UpdateData()
        {
            m_handDataAsset.Config = Config;
            m_handDataAsset.IsDataValid = true;
            m_handDataAsset.IsConnected = true;
            if (!m_handDataAsset.IsConnected)
            {
                // revert state fields to their defaults
                m_handDataAsset.IsTracked = default;
                m_handDataAsset.RootPoseOrigin = default;
                m_handDataAsset.PointerPoseOrigin = default;
                m_handDataAsset.IsHighConfidence = default;
                for (var fingerIdx = 0; fingerIdx < Constants.NUM_FINGERS; fingerIdx++)
                {
                    m_handDataAsset.IsFingerPinching[fingerIdx] = default;
                    m_handDataAsset.IsFingerHighConfidence[fingerIdx] = default;
                }
                return;
            }

            m_handDataAsset.IsTracked = true;
            m_handDataAsset.IsHighConfidence = true;
            m_handDataAsset.HandScale = 1f;

            m_handDataAsset.IsDominantHand = m_handedness == Handedness.Right;

            var actions = m_xrInputManager.GetActions(m_handedness == Handedness.Left);
            var anchor = m_xrInputManager.GetAnchor(m_handedness == Handedness.Left);

            var indexStrength = actions.AxisIndexTrigger.action.ReadValue<float>();
            var gripStrength = actions.AxisHandTrigger.action.ReadValue<float>();

            m_handDataAsset.IsFingerHighConfidence[(int)HandFinger.Thumb] = true;
            m_handDataAsset.IsFingerPinching[(int)HandFinger.Thumb] = indexStrength >= 0.95f || gripStrength >= 0.95f;
            m_handDataAsset.FingerPinchStrength[(int)HandFinger.Thumb] = Mathf.Max(indexStrength, gripStrength);

            m_handDataAsset.IsFingerHighConfidence[(int)HandFinger.Index] = true;
            m_handDataAsset.IsFingerPinching[(int)HandFinger.Index] = indexStrength >= 0.95f;
            m_handDataAsset.FingerPinchStrength[(int)HandFinger.Index] = indexStrength;

            m_handDataAsset.IsFingerHighConfidence[(int)HandFinger.Middle] = true;
            m_handDataAsset.IsFingerPinching[(int)HandFinger.Middle] = gripStrength >= 0.95f;
            m_handDataAsset.FingerPinchStrength[(int)HandFinger.Middle] = gripStrength;

            m_handDataAsset.IsFingerHighConfidence[(int)HandFinger.Ring] = true;
            m_handDataAsset.IsFingerPinching[(int)HandFinger.Ring] = gripStrength >= 0.95f;
            m_handDataAsset.FingerPinchStrength[(int)HandFinger.Ring] = gripStrength;

            m_handDataAsset.IsFingerHighConfidence[(int)HandFinger.Pinky] = true;
            m_handDataAsset.IsFingerPinching[(int)HandFinger.Pinky] = gripStrength >= 0.95f;
            m_handDataAsset.FingerPinchStrength[(int)HandFinger.Pinky] = gripStrength;

            m_handDataAsset.PointerPoseOrigin = PoseOrigin.RawTrackedPose;
            m_handDataAsset.PointerPose = new Pose(anchor.localPosition, anchor.localRotation);

            for (var i = 0; i < m_bones.Length; i++)
            {
                m_handDataAsset.Joints[i] = m_bones[i].localRotation;
            }

            m_handDataAsset.Joints[0] = WristFixupRotation;

            // Convert controller pose from world to tracking space.
            var pose = new Pose(m_ovrControllerAnchor.position, m_ovrControllerAnchor.rotation);
            pose = Config.TrackingToWorldTransformer.ToTrackingPose(pose);

            PoseUtils.Multiply(pose, m_poseOffset, ref m_handDataAsset.Root);
            m_handDataAsset.RootPoseOrigin = PoseOrigin.RawTrackedPose;
        }

        #region Inject

        public void InjectAllFromOVRControllerHandDataSource(UpdateModeFlags updateMode, IDataSource updateAfter,
            Handedness handedness, ITrackingToWorldTransformer trackingToWorldTransformer,
            IDataSource<HmdDataAsset> hmdData, Transform[] bones,
            Vector3 rootOffset, Vector3 rootAngleOffset)
        {
            InjectAllDataSource(updateMode, updateAfter);
            InjectHandedness(handedness);
            InjectTrackingToWorldTransformer(trackingToWorldTransformer);
            InjectBones(bones);
            InjectRootOffset(rootOffset);
            InjectRootAngleOffset(rootAngleOffset);
        }

        public void InjectHandedness(Handedness handedness)
        {
            m_handedness = handedness;
        }

        public void InjectTrackingToWorldTransformer(ITrackingToWorldTransformer trackingToWorldTransformer)
        {
            m_trackingToWorldTransformer = trackingToWorldTransformer as MonoBehaviour;
            TrackingToWorldTransformer = trackingToWorldTransformer;
        }

        public void InjectBones(Transform[] bones)
        {
            m_bones = bones;
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
