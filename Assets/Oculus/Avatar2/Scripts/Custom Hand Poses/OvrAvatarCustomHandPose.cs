using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

using HandJointType = OvrAvatarHandJointType.HandJointType;

namespace Oculus.Avatar2
{
    [RequireComponent(typeof(OvrAvatarEntity))]
    public class OvrAvatarCustomHandPose : MonoBehaviour
    {
        private const string logscope = "customHandPose";
        private readonly Quaternion _leftDefaultRotation = Quaternion.Euler(0, 270, 90);
        private readonly Quaternion _rightDefaultRotation = Quaternion.Euler(0, 90, 90);

        private struct JointTransform
        {
            public CAPI.ovrAvatar2JointType type;
            public Transform transform;
            public int parentIndex;
        }

        [SerializeField]
        [Tooltip("Which avatar hand is controlled by this component")]
        private CAPI.ovrAvatar2Side _side = CAPI.ovrAvatar2Side.Left;

        [SerializeField]
        [Tooltip("The hand skeleton to use, kept in it's default pose")]
        private GameObject _handSkeleton = null;

        [SerializeField]
        [Tooltip("The hand skeleton's forward vector in local space")]
        private Vector3 _handSkelLocalForward = new Vector3(0, 0, 1);

        [SerializeField]
        [Tooltip("The instance of the hand skeleton that will be used to set the custom hand pose\nIf null, handSkeleton will be instantiated")]
        public GameObject _handPose = null;

        [SerializeField]
        public Transform _wristOffset = null;

        public bool setHandPose = true;
        public bool setWristOffset = true;

        [SerializeField]
        [HideInInspector]
        private OvrAvatarEntity _entity;

        private (CAPI.ovrAvatar2Bone[] bones, CAPI.ovrAvatar2Transform[] pose) _skeletonData;
        private List<JointTransform> _poseJointTransforms = new List<JointTransform>();
        private int _wristIndex;

        private bool _skeletonIsSet = false;
        private bool _initialized = false;

        private bool _entityPreviouslyReady = false;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            _entity = GetComponent<OvrAvatarEntity>();
        }
#endif

        protected virtual void OnEnable()
        {
            Initialize();
        }

        protected virtual void OnDisable()
        {
            ClearHandPose();
        }

        protected virtual void Update()
        {
            if (setHandPose) { UpdateHandPose(); }
            if (setWristOffset) { UpdateWristOffset(); }
        }

        public void Initialize()
        {
            if (_initialized) return;

            OvrAvatarLog.AssertConstMessage(_entity, "No associated entity", logscope, this);
            _skeletonData = GetSkeletonData();

            if (_handPose == null)
            {
                _handPose = Instantiate(_handSkeleton);
            }

            _poseJointTransforms = GetJointTransforms(_handPose, out _wristIndex);
            _initialized = true;
        }

        public void SetHandSkeleton()
        {
            Initialize();

            // Wait until the entity is loaded with an anim hierarchy
            if (!EntityIsReady()) return;

            var skelPoseHandle = GCHandle.Alloc(_skeletonData.pose, GCHandleType.Pinned);
            var skelBonesHandle = GCHandle.Alloc(_skeletonData.bones, GCHandleType.Pinned);

            try
            {
                unsafe
                {
                    CAPI.ovrAvatar2TrackingBodyPose cSkelPose = new CAPI.ovrAvatar2TrackingBodyPose(
                        (CAPI.ovrAvatar2Transform*)skelPoseHandle.AddrOfPinnedObject(), (uint)_skeletonData.pose.Length,
                        CAPI.ovrAvatar2Space.Local);

                    CAPI.ovrAvatar2TrackingBodySkeleton cSkel = new CAPI.ovrAvatar2TrackingBodySkeleton(
                        (CAPI.ovrAvatar2Bone*)skelBonesHandle.AddrOfPinnedObject(), (uint)_skeletonData.bones.Length,
                        cSkelPose);
                    cSkel.forwardDir = _handSkeleton.transform.TransformDirection(_handSkelLocalForward);
                    cSkel.forwardDir = cSkel.forwardDir.ConvertSpace();

                    if (_entity.SetCustomHandSkeleton(_side, in cSkel))
                    {
                        _skeletonIsSet = true;
                    }
                }
            }
            finally
            {
                skelPoseHandle.Free();
                skelBonesHandle.Free();
            }
        }

        // Can be called directly to update to a static hand pose even when component is disabled
        public void UpdateHandPose()
        {
            // Wait until the entity is loaded with an anim hierarchy
            if (!EntityIsReady()) { return; }
            if (!_skeletonIsSet)
            {
                SetHandSkeleton();
                if (!_skeletonIsSet) { return; }
            }

            // Hand pose
            var poseTransforms = new NativeArray<CAPI.ovrAvatar2Transform>(_poseJointTransforms.Count, Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            GetPoseTransformsNative(_poseJointTransforms, poseTransforms);

            try
            {
                unsafe
                {
                    var cPose = new CAPI.ovrAvatar2TrackingBodyPose(
                        (CAPI.ovrAvatar2Transform*)poseTransforms.GetUnsafePtr(), (uint)poseTransforms.Length,
                        CAPI.ovrAvatar2Space.Local);

                    _entity.SetCustomHandPose(_side, in cPose);
                }
            }
            finally
            {
                poseTransforms.Dispose();
            }
        }

        public void UpdateWristOffset()
        {
            if (!EntityIsReady()) return;

            // Wrist offset
            if (_wristOffset != null && _wristIndex >= 0)
            {
                var wristX = _poseJointTransforms[_wristIndex].transform;

                CAPI.ovrAvatar2Vector3f position = wristX.InverseTransformPoint(_wristOffset.position);
                position.x = -position.x;
                CAPI.ovrAvatar2Quatf rotation = Quaternion.Inverse(wristX.rotation) * _wristOffset.rotation;

                var xform = new CAPI.ovrAvatar2Transform(position, rotation);
                _entity.SetCustomWristOffset(_side, in xform);
            }
        }

        public void ClearHandPose()
        {
            if (_entity && _entity.IsCreated)
            {
                _entity.ClearCustomHandPose(_side);
            }
        }

        private bool EntityIsReady()
        {
            bool isReady = _entity && (_entity.CurrentState == OvrAvatarEntity.AvatarState.DefaultAvatar
                                       || _entity.CurrentState == OvrAvatarEntity.AvatarState.FastLoad
                                       || _entity.CurrentState == OvrAvatarEntity.AvatarState.UserAvatar);

            if (!isReady && _entityPreviouslyReady)
            {
                ClearHandPose();
            }

            _entityPreviouslyReady = isReady;
            return isReady;
        }

        // Take list of joint transforms from hand and convert to native space ovrAvatar2Transforms
        private static void GetPoseTransformsNative(List<JointTransform> joints, NativeArray<CAPI.ovrAvatar2Transform> xforms)
        {
            OvrAvatarLog.AssertConstMessage(joints.Count == xforms.Length, "Joint array and native array mismatch",
                logscope);
            for (var i = 0; i < joints.Count; i++)
            {
                // TODO: Handle `OVR_AVATAR_ENABLE_CLIENT_XFORM` in `ConvertSpace()`?
#if OVR_AVATAR_ENABLE_CLIENT_XFORM
                xforms[i] = (CAPI.ovrAvatar2Transform)joints[i].transform;
#else
                xforms[i] = joints[i].transform.ConvertSpace();
#endif
            }

        }

        // Take list of joint transforms from hand and convert to native space ovrAvatar2Transforms
        private static CAPI.ovrAvatar2Transform[] GetPoseTransforms(List<JointTransform> joints)
        {
            var xforms = new CAPI.ovrAvatar2Transform[joints.Count];

            for (var i = 0; i < joints.Count; i++)
            {
                // TODO: Handle `OVR_AVATAR_ENABLE_CLIENT_XFORM` in `ConvertSpace()`?
#if OVR_AVATAR_ENABLE_CLIENT_XFORM
                xforms[i] = (CAPI.ovrAvatar2Transform)joints[i].transform;
#else
                xforms[i] = joints[i].transform.ConvertSpace();
#endif
            }

            return xforms;
        }

        private (CAPI.ovrAvatar2Bone[], CAPI.ovrAvatar2Transform[]) GetSkeletonData()
        {
            var jointTransforms = GetJointTransforms(_handSkeleton);

            var bones = new CAPI.ovrAvatar2Bone[jointTransforms.Count];
            for (var i = 0; i < jointTransforms.Count; i++)
            {
                bones[i] = new CAPI.ovrAvatar2Bone
                {
                    boneId = jointTransforms[i].type,
                    parentBoneIndex = (short)jointTransforms[i].parentIndex
                };
            }
            return (bones, GetPoseTransforms(jointTransforms));
        }

        // Get ordered list of joints from the hand
        private List<JointTransform> GetJointTransforms(GameObject gob)
        {
            return GetJointTransforms(gob, out int wristIndexIgnored);
        }

        private List<JointTransform> GetJointTransforms(GameObject gob, out int wristIndex)
        {
            wristIndex = -1;

            var joints = new List<JointTransform>();
            var transformToIndex = new Dictionary<Transform, int>();
            foreach (var xform in gob.GetComponentsInChildren<Transform>())
            {
                int parentIndex = -1;
                if (xform.parent && !transformToIndex.TryGetValue(xform.parent, out parentIndex))
                {
                    parentIndex = -1;
                }

                var typeComponent = xform.GetComponent<OvrAvatarHandJointType>();
                var type = typeComponent ? typeComponent.jointType : HandJointType.Invalid;
                if (type == HandJointType.Wrist) { wristIndex = joints.Count; }

                joints.Add(new JointTransform
                {
                    type = JointTypeFromHandType(type, _side),
                    transform = xform,
                    parentIndex = parentIndex
                });

                transformToIndex.Add(xform, joints.Count - 1);
            }

            return joints;
        }

        private static CAPI.ovrAvatar2JointType JointTypeFromHandType(
            HandJointType hand, CAPI.ovrAvatar2Side side)
        {
            if (hand == HandJointType.Invalid)
            {
                return CAPI.ovrAvatar2JointType.Invalid;
            }

            if (hand == HandJointType.Wrist)
            {
                return side == CAPI.ovrAvatar2Side.Left
                    ? CAPI.ovrAvatar2JointType.LeftHandWrist
                    : CAPI.ovrAvatar2JointType.RightHandWrist;
            }

            var handBase = side == CAPI.ovrAvatar2Side.Left
                ? CAPI.ovrAvatar2JointType.LeftHandThumbTrapezium
                : CAPI.ovrAvatar2JointType.RightHandThumbTrapezium;

            return handBase + (int)hand - 1;
        }
    }
}
