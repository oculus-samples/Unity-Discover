using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Avatar2
{
    internal interface IJointMonitor : IDisposable
    {
        void OnJointPosesUpdated(List<OvrAvatarJointPose> jointPoses);

        // TODO: Remove from interface when API is finalized and component can be accessed directly
        bool TryGetTransform(CAPI.ovrAvatar2JointType jointType, out Transform tx);

        // TODO: Remove from interface when API is finalized and component can be accessed directly
        bool TryGetPositionAndOrientation(CAPI.ovrAvatar2JointType jointType, out Vector3 pos, out Quaternion rot);

        void UpdateJoints(float deltaTime);
    }

    internal readonly struct OvrAvatarJointPose
    {
        public readonly CAPI.ovrAvatar2JointType jointType;
        public readonly uint jointIndex;
        public readonly Vector3 objectSpacePosition;
        public readonly Quaternion objectSpaceOrientation;

        internal OvrAvatarJointPose(CAPI.ovrAvatar2JointType jointType, uint jointIndex)
        {
            this.jointType = jointType;
            this.jointIndex = jointIndex;
            this.objectSpacePosition = Vector3.zero;
            this.objectSpaceOrientation = Quaternion.identity;
        }

        internal OvrAvatarJointPose(in OvrAvatarJointPose pose, in CAPI.ovrAvatar2Transform tx)
        {
            this.jointType = pose.jointType;
            this.jointIndex = pose.jointIndex;
            this.objectSpacePosition = tx.position;
            this.objectSpaceOrientation = tx.orientation;
        }
    }

    public partial class OvrAvatarEntity : MonoBehaviour
    {
        private readonly HashSet<CAPI.ovrAvatar2JointType> _monitoredJointTypes =
            new HashSet<CAPI.ovrAvatar2JointType>();
        private readonly List<OvrAvatarJointPose> _monitoredJointPoses =
            new List<OvrAvatarJointPose>();

        private IJointMonitor _jointMonitor = null;

        internal bool IsJointTypeLoaded(CAPI.ovrAvatar2JointType jointType)
        {
            return GetNodeForType(jointType) != CAPI.ovrAvatar2NodeId.Invalid;
        }

        internal bool AddMonitoredJoint(CAPI.ovrAvatar2JointType jointType)
        {
            if (_jointMonitor != null && !_monitoredJointTypes.Contains(jointType))
            {
                _monitoredJointTypes.Add(jointType);

                var nodeId = GetNodeForType(jointType);
                if (nodeId != CAPI.ovrAvatar2NodeId.Invalid)
                {
                    var index = _nodeToIndex[nodeId];
                    _monitoredJointPoses.Add(new OvrAvatarJointPose(jointType, index));
                }

                return true;
            }

            return false;
        }

        internal bool RemoveMonitoredJoint(CAPI.ovrAvatar2JointType jointType)
        {
            if (_jointMonitor != null && _monitoredJointTypes.Contains(jointType))
            {
                _monitoredJointTypes.Remove(jointType);
                _monitoredJointPoses.RemoveAll(pose => pose.jointType == jointType);
                return true;
            }

            return false;
        }

        private void MonitorJoints(in CAPI.ovrAvatar2Pose entityPose)
        {
            // Only do the work if someone cares about it
            if (_jointMonitor == null || _monitoredJointPoses.Count == 0) { return; }

            bool validObjectTransforms;
            unsafe { validObjectTransforms = entityPose.objectTransforms != null; }

            if (validObjectTransforms)
            {
                for(int i = 0; i < _monitoredJointPoses.Count; ++i)
                {
                    var jointIndex = _monitoredJointPoses[i].jointIndex;
                    CAPI.ovrAvatar2Transform tx;
                    unsafe { tx = entityPose.objectTransforms[jointIndex].ConvertSpace(); }

                    // Use root transform values instead
                    // BUG: Native sdk shouldn't be giving NaN values in the first place
                    if (tx.IsNan()) {
                        tx = (CAPI.ovrAvatar2Transform)_baseTransform;
                    }

                    // Update transform
                    _monitoredJointPoses[i] = new OvrAvatarJointPose(_monitoredJointPoses[i], in tx);
                }
            }

            _jointMonitor.OnJointPosesUpdated(_monitoredJointPoses);
        }
    }
}
