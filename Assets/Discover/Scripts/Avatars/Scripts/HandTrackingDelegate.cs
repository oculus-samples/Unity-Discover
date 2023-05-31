// Copyright (c) Meta Platforms, Inc. and affiliates.



#pragma warning disable IDE1006

using Oculus.Avatar2;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Assertions;

namespace Oculus.Interaction.AvatarIntegration
{
    public class HandTrackingDelegate : IOvrAvatarHandTrackingDelegate
    {
        private Transform _root;
        private IHand _leftHand;
        private IHand _rightHand;
        private const int JOINTS_PER_HAND = 17;
        public HandTrackingDelegate(Transform root, IHand leftHand, IHand rightHand)
        {
            _root = root;
            Assert.IsNotNull(_root);

            _leftHand = leftHand;
            Assert.IsNotNull(_leftHand);

            _rightHand = rightHand;
            Assert.IsNotNull(_rightHand);
        }

        public bool GetHandData(OvrAvatarTrackingHandsState handData)
        {
            // tracking status flags
            handData.isConfidentLeft = _leftHand.IsHighConfidence;
            handData.isConfidentRight = _rightHand.IsHighConfidence;
            handData.isTrackedLeft = _leftHand.IsTrackedDataValid;
            handData.isTrackedRight = _rightHand.IsTrackedDataValid;
            handData.handScaleLeft = _leftHand.Scale;
            handData.handScaleRight = _rightHand.Scale;

            var worldToRootPose = _root.GetPose();
            PoseUtils.Invert(ref worldToRootPose);

            // wrist positions
            if (_leftHand.GetRootPose(out var wristPose))
            {
                handData.wristPosLeft = InteractionAvatarConversions.PoseToAvatarTransformFlipZ(wristPose.GetTransformedBy(worldToRootPose));
            }

            if (_rightHand.GetRootPose(out wristPose))
            {
                handData.wristPosRight = InteractionAvatarConversions.PoseToAvatarTransformFlipZ(wristPose.GetTransformedBy(worldToRootPose));
            }
            // joint rotations
            var sourceOffset = (int)HandJointId.HandThumb0;
            var destOffset = 0;
            CopyJointRotations(_leftHand, sourceOffset, handData.boneRotations, destOffset);

            destOffset = JOINTS_PER_HAND;
            CopyJointRotations(_rightHand, sourceOffset, handData.boneRotations, destOffset);

            return true;
        }

        private void CopyJointRotations(IHand hand, int sourceOffset,
            CAPI.ovrAvatar2Quatf[] destination, int destinationOffset)
        {
            if (!hand.GetJointPosesLocal(out var localJoints))
            {
                return;
            }
            for (var i = 0; i < JOINTS_PER_HAND; ++i)
            {
                destination[destinationOffset + i] = InteractionAvatarConversions.UnityToAvatarQuaternionFlipX(localJoints[sourceOffset + i].rotation);
            }
        }
    }
}
