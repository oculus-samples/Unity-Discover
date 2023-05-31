using System;
using System.Collections;
using System.Collections.Generic;

using Oculus.Skinning.GpuSkinning;

using UnityEngine;
using Object = UnityEngine.Object;

namespace Oculus.Avatar2
{
    internal interface IJointData
    {
        Transform JointTransform { get; }

        bool TryGetPosAndOrientation(out Vector3 pos, out Quaternion quat);

        void Dispose();
    }

    internal abstract class EntityJointMonitorBase<T> : IJointMonitor where T : IJointData
    {
        private readonly List<CAPI.ovrAvatar2JointType> _monitoredJoints = new List<CAPI.ovrAvatar2JointType>();

        private readonly Dictionary<CAPI.ovrAvatar2JointType, T> _jointsToData =
            new Dictionary<CAPI.ovrAvatar2JointType, T>();

        private OvrAvatarEntity _entity;

        protected abstract string LogScope { get; }

        private bool TryGetJointData(CAPI.ovrAvatar2JointType jointType, out T jointData)
        {
            return _jointsToData.TryGetValue(jointType, out jointData);
        }

        public bool TryGetTransform(CAPI.ovrAvatar2JointType jointType, out Transform tx)
        {
            if (TryGetJointData(jointType, out var jointData) && IsJointDataValid(jointData))
            {
                tx = jointData.JointTransform;
                return true;
            }

            tx = null;
            return false;
        }

        public bool TryGetPositionAndOrientation(CAPI.ovrAvatar2JointType jointType, out Vector3 pos
            , out Quaternion rot)
        {
            if (TryGetJointData(jointType, out var jointData) && jointData.TryGetPosAndOrientation(out pos, out rot))
            {
                return true;
            }
            pos = Vector3.zero;
            rot = Quaternion.identity;
            return false;
        }

        protected EntityJointMonitorBase(OvrAvatarEntity entity)
        {
            _entity = entity;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~EntityJointMonitorBase()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool isDispose)
        {
            _monitoredJoints.Clear();

            foreach (var jointData in _jointsToData.Values)
            {
                jointData.Dispose();
            }
            _jointsToData.Clear();

            _entity = null;
        }


        private T AddMonitoredJoint(CAPI.ovrAvatar2JointType jointType)
        {
            OvrAvatarLog.Assert(
                !_jointsToData.TryGetValue(jointType, out var prevJointData) || !IsJointDataValid(prevJointData),
                LogScope,
                _entity);

            var newJointData = CreateNewJointData(jointType);

            if (!_jointsToData.ContainsKey(jointType))
            {
                // Newly tracked joint
                _jointsToData.Add(jointType, newJointData);
            }
            else
            {
                // Had previously tracked joint, but was cleared out (null data)
                _jointsToData[jointType] = newJointData;
            }

            return newJointData;
        }

        void IJointMonitor.OnJointPosesUpdated(List<OvrAvatarJointPose> jointPoses)
        {
            int jointsUpdatedCount = 0;
            foreach (var jointPose in jointPoses)
            {
                var jointType = jointPose.jointType;
                T jointData;
                if (_jointsToData.TryGetValue(jointType, out jointData))
                {
                    // Null data (can happen if
                    // entity clears out joint that was previously monitored)
                    if (!IsJointDataValid(jointData))
                    {
                        jointData = AddMonitoredJoint(jointType);
                    }
                }
                else
                {
                    // OvrAvatarEntity added this joint - begin tracking it
                    _monitoredJoints.Add(jointType);
                    jointData = AddMonitoredJoint(jointType);
                }

                AddNewAnimationFrameForJoint(jointData, jointPose.objectSpacePosition, jointPose.objectSpaceOrientation);
                ++jointsUpdatedCount;
            }

            if (_jointsToData.Count != jointsUpdatedCount)
            {
                // Not all transforms we have were updated, so at least one joint no longer exists on the entity
                foreach (var jointType in _monitoredJoints)
                {
                    // Clear out previously monitored joints that the entity
                    // no longer has
                    if (!_entity.IsJointTypeLoaded(jointType))
                    {
                        if (_jointsToData.TryGetValue(jointType, out var jointData) && IsJointDataValid(jointData))
                        {
                            DisposeJointData(jointType, jointData);
                            _jointsToData[jointType] = default(T);
                        }
                    }
                }
            }
        }

        protected Transform CreateNewTransform(CAPI.ovrAvatar2JointType jointType)
        {
            var go = new GameObject("Joint " + jointType);
            var newTx = go.transform;

            newTx.SetParent(_entity._baseTransform);
            newTx.localScale = Vector3.one;

            return newTx;
        }

        protected Dictionary<CAPI.ovrAvatar2JointType, T>.ValueCollection GetAllJointData()
        {
            return _jointsToData.Values;
        }

        private static bool IsJointDataValid(T jointData)
        {
            return !EqualityComparer<T>.Default.Equals(jointData, default(T));
        }

        protected abstract T CreateNewJointData(CAPI.ovrAvatar2JointType jointType);

        protected virtual void DisposeJointData(CAPI.ovrAvatar2JointType jointType, T jointData)
        {
            jointData.Dispose();
        }

        protected abstract void AddNewAnimationFrameForJoint(
            T jointData,
            in Vector3 objectSpacePosition,
            in Quaternion objectSpaceOrientation);

        public abstract void UpdateJoints(float deltaTime);
    } // end class

    internal class TransformHolder : IJointData, IDisposable
    {
        public Transform JointTransform { get; }

        public TransformHolder(Transform tx)
        {
            JointTransform = tx;
        }

        public bool TryGetPosAndOrientation(out Vector3 pos, out Quaternion quat)
        {
            if (JointTransform == null)
            {
                pos = Vector3.zero;
                quat = Quaternion.identity;
                return false;
            }
            pos = JointTransform.localPosition;
            quat = JointTransform.localRotation;
            return true;
        }

        public void Dispose()
        {
            Object.Destroy(JointTransform.gameObject);
        }
    }

    internal class OvrAvatarEntityJointMonitor : EntityJointMonitorBase<TransformHolder>
    {
        public OvrAvatarEntityJointMonitor(OvrAvatarEntity entity) : base(entity)
        {
        }

        protected override string LogScope => "jointMonitor";

        protected override TransformHolder CreateNewJointData(CAPI.ovrAvatar2JointType jointType)
        {
            var newTransform = CreateNewTransform(jointType);
            return new TransformHolder(newTransform);
        }

        protected override void AddNewAnimationFrameForJoint(
            TransformHolder jointData,
            in Vector3 objectSpacePosition,
            in Quaternion objectSpaceOrientation)
        {
            // Animation frames just overwrite the transform directly
            jointData.JointTransform.localPosition = objectSpacePosition;
            jointData.JointTransform.localRotation = objectSpaceOrientation;
        }

        public override void UpdateJoints(float deltaTime)
        {
            // Intentionally empty
        }
    }

    internal class InterpolatingJoint : IJointData
    {
        private Vector3 _objectSpacePosition0;
        private Vector3 _objectSpacePosition1;

        private Quaternion _objectSpaceOrientation0;
        private Quaternion _objectSpaceOrientation1;

        private float _lastInterpolationValue = 0.0f;

        public InterpolatingJoint(Transform tx)
        {
            JointTransform = tx;
        }

        public Transform JointTransform { get; private set; }

        public bool TryGetPosAndOrientation(out Vector3 pos, out Quaternion quat)
        {
            CalculateUpdate(_lastInterpolationValue, out pos, out quat);
            return true;
        }

        public void Dispose()
        {
            if (JointTransform == null) { return; }

            Object.Destroy(JointTransform.gameObject);
            JointTransform = null;
        }

        public void AddNewAnimationFrame(in Vector3 objectSpacePosition, in Quaternion objectSpaceOrientation)
        {
            // Shift the "latest frame"'s data to the "earliest frame"'s data and then
            // add in the new frame
            _objectSpacePosition0 = _objectSpacePosition1;
            _objectSpaceOrientation0 = _objectSpaceOrientation1;

            _objectSpacePosition1 = objectSpacePosition;
            _objectSpaceOrientation1 = objectSpaceOrientation;
        }

        public void CalculateUpdate(float interpolationValue, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.Lerp(_objectSpacePosition0, _objectSpacePosition1, interpolationValue);
            rotation = Quaternion.Slerp(_objectSpaceOrientation0, _objectSpaceOrientation1, interpolationValue);
        }

        public void UpdateTransform(float interpolationValue)
        {
            _lastInterpolationValue = interpolationValue;

            CalculateUpdate(interpolationValue, out var pos, out var rot);
            JointTransform.localPosition = pos;
            JointTransform.localRotation = rot;
        }
    }

    internal class OvrAvatarEntitySmoothingJointMonitor : EntityJointMonitorBase<InterpolatingJoint>
    {
        protected readonly IInterpolationValueProvider _interpolationProvider;

        public OvrAvatarEntitySmoothingJointMonitor(OvrAvatarEntity entity, IInterpolationValueProvider interpolationValueProvider) : base(entity)
        {
            _interpolationProvider = interpolationValueProvider;
        }

        protected override string LogScope => "SmoothingJointMonitor";

        protected override InterpolatingJoint CreateNewJointData(CAPI.ovrAvatar2JointType jointType)
        {
            var newTransform = CreateNewTransform(jointType);
            return new InterpolatingJoint(newTransform);
        }

        protected override void AddNewAnimationFrameForJoint(InterpolatingJoint jointData, in Vector3 objectSpacePosition, in Quaternion objectSpaceOrientation)
        {
            jointData.AddNewAnimationFrame(in objectSpacePosition, in objectSpaceOrientation);
        }

        public override void UpdateJoints(float deltaTime)
        {
            float interpolationValue = _interpolationProvider.GetRenderInterpolationValue();

            // Update all joints
            foreach (var joint in GetAllJointData())
            {
                joint.UpdateTransform(interpolationValue);
            }
        }
    }
}
