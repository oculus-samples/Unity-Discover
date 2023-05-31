using System;
using System.Collections.Generic;
using UnityEngine;

using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using UnityEngine.Profiling;


namespace Oculus.Avatar2
{
    internal class OvrAvatarEntitySmoothingJointJobMonitor : OvrAvatarEntitySmoothingJointMonitor
    {
        private const string logScope = "OvrAvatarEntitySmoothingJointJobMonitor";

        public OvrAvatarEntitySmoothingJointJobMonitor(OvrAvatarEntity entity
            , IInterpolationValueProvider interpolationValueProvider)
            : base(entity, interpolationValueProvider)
        {
        }

        /* Transforms which will be updated - used to schedule the transform job */
        private TransformAccessArray _jobTransformAccess = default;
        /* Sized to the number of transforms which will actually be updated, stores the pose data used by the job */
        private NativeArray<JointPose> _jointJobNativeBuffer;

        /* Used to quickly determine when the set of monitored joints changes */
        private readonly HashSet<InterpolatingJoint> _activeJoints = new HashSet<InterpolatingJoint>();

        /* Used as fast storage for the current set of monitored interpolating joints
         - in order to update `jointJobNativeBuffer_` before scheduling the next transform job */
        private InterpolatingJoint[] _jobJoints = Array.Empty<InterpolatingJoint>();

        /* Only needed for safe shutdown */
        private JobHandle _currentJob = default;

        /* Track whether the monitored joints have changed since the last update */
        private bool _monitoredJointsChanged = false;

        protected override InterpolatingJoint CreateNewJointData(CAPI.ovrAvatar2JointType jointType)
        {
            var joint = base.CreateNewJointData(jointType);
            // TODO: This shouldn't be necessary, any call to `CreateNewJointData` should indicate the joints changed
            // For now this acts as a factor of safety
            _monitoredJointsChanged |= _activeJoints.Add(joint);
            return joint;
        }

        protected override void DisposeJointData(CAPI.ovrAvatar2JointType jointType, InterpolatingJoint jointData)
        {
            // TODO: This shouldn't be necessary, any call to `DisposeJointData` should indicate the joints changed
            // For now this acts as a factor of safety
            _monitoredJointsChanged |= _activeJoints.Remove(jointData);
            base.DisposeJointData(jointType, jointData);
        }

        public override void UpdateJoints(float deltaTime)
        {
            var jointCount = _activeJoints.Count;

            if (_monitoredJointsChanged)
            {
                Profiler.BeginSample("JointMonitorJob::RebuildBuffers");
                // Size all buffers to `jointCount`
                var newJobTransformAccess = new TransformAccessArray(jointCount);
                var newJointJobNativeBuffer = new NativeArray<JointPose>(
                    jointCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);

                if (jointCount != _jobJoints.Length)
                {
                    Array.Resize(ref _jobJoints, jointCount);
                }

                int regIdx = 0;
                var jointData = GetAllJointData();
                OvrAvatarLog.Assert(jointData.Count == jointCount, logScope);

                // Populate new transformAccessArray and update current set of jobJoints
                foreach (var registeredJoint in jointData)
                {
                    _jobJoints[regIdx++] = registeredJoint;
                    // note: filling via index does not work
                    newJobTransformAccess.Add(registeredJoint.JointTransform);
                }

                // Ensure current job is finished before disposing the arrays its using
                _currentJob.Complete();

                // Clean up old buffers
                if(_jobTransformAccess.isCreated) { _jobTransformAccess.Dispose(); }
                if(_jointJobNativeBuffer.IsCreated) { _jointJobNativeBuffer.Dispose(); }

                // Swap in new ones
                _jobTransformAccess = newJobTransformAccess;
                _jointJobNativeBuffer = newJointJobNativeBuffer;

                _monitoredJointsChanged = false;

                Profiler.EndSample(); //"JointMonitorJob::RebuildBuffers"
            }
            else
            {
                // Must wait for previous job to complete before updating `_jointJobNativeBuffer`
                _currentJob.Complete();
            }

            /* Early out if we have 0 joints to update, attempting to schedule a 0 length job will crash */
            if (jointCount == 0) { return; }

            // Update all jointPoses
            Profiler.BeginSample("JointMonitorJob::UpdateJointPoses");
            float interpolationValue = _interpolationProvider.GetRenderInterpolationValue();
            int idx = 0;
            foreach (var joint in _jobJoints)
            {
                // TODO: This could be moved into the IJob itself,
                // but we need to guard against adding new frames while it runs
                joint.CalculateUpdate(interpolationValue, out var pos, out var rot);
                _jointJobNativeBuffer[idx++] = new JointPose(in pos, in rot);
            }

            Profiler.EndSample(); //"JointMonitorJob::UpdateJointPoses"

            _currentJob = ScheduleUpdateTransformsJob(in _jointJobNativeBuffer, in _jobTransformAccess);
        }

        private static JobHandle ScheduleUpdateTransformsJob(in NativeArray<JointPose> joints, in TransformAccessArray transforms) {
            Debug.Assert(joints.Length > 0);
            Debug.Assert(joints.Length == transforms.length);

            // TODO: Consider merging all updateTransforms jobs into one to kickoff in `SyncOutputComplete`
            // * Would reduce scheduling overhead at the cost of less granularity (higher chance to block on mainthread, for longer)
            var updateJob = new UpdateJointTransformsJob(joints);
            return updateJob.Schedule(transforms);
        }

        protected override void Dispose(bool isDispose)
        {
            _currentJob.Complete();

            if (_jobTransformAccess.isCreated)
            {
                _jobTransformAccess.Dispose();
                _jobTransformAccess = default;
            }

            if (_jointJobNativeBuffer.IsCreated)
            {
                _jointJobNativeBuffer.Dispose();
                _jointJobNativeBuffer = default;
            }
            _activeJoints.Clear();
            _jobJoints = null;

            base.Dispose(isDispose);
        }

        /* Job Structs */

        private readonly struct JointPose
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public JointPose(in Vector3 pos, in Quaternion rot)
            {
                this.Position = pos;
                this.Rotation = rot;
            }
        }

        private readonly struct UpdateJointTransformsJob : IJobParallelForTransform {
            public UpdateJointTransformsJob(in NativeArray<JointPose> joints) {
                _jointTransforms = joints;
            }

            [ReadOnly]
            private readonly NativeArray<JointPose> _jointTransforms;

            void IJobParallelForTransform.Execute(int index, TransformAccess txAccess) {
                Profiler.BeginSample("UpdateJointTransformsJob::Execute");
                // Avoid JointPose copy and NativeArray indexer
                unsafe
                {
                    var jointPose = _jointTransforms.GetReadonlyPtr() + index;

                    txAccess.localPosition = jointPose->Position;
                    txAccess.localRotation = jointPose->Rotation;
                }

                Profiler.EndSample();
            }
        }
    }
}
