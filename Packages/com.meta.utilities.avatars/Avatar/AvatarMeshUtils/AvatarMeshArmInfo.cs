// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.Linq;
using Meta.Utilities;
using Unity.Collections;
using UnityEngine;
using static Oculus.Avatar2.CAPI;

namespace Meta.Utilities.Avatars
{
    public struct ArmDetails
    {
        public Vector3 UpperTop;
        public Vector3 UpperBottom;
        public Vector3 LowerTop;
        public Vector3 LowerBottom;
        public float UpperRadius;
        public float LowerRadius;

        public ArmDetails(Vector3 upperTop, Vector3 upperBottom, Vector3 lowerTop, Vector3 lowerBottom, float upperRadius, float lowerRadius)
        {
            UpperTop = upperTop;
            UpperBottom = upperBottom;
            LowerTop = lowerTop;
            LowerBottom = lowerBottom;
            UpperRadius = upperRadius;
            LowerRadius = lowerRadius;
        }
    }

    public static class AvatarMeshArmInfo
    {
        private const string LEFT_ARM_BONE_PREFIX = "LeftArm";
        private const string RIGHT_ARM_BONE_PREFIX = "RightArm";

        public static (Vector3 top, Vector3 bottom, float radius)? GetArmPartDetails(this AvatarMeshQuery avatarMeshQuery, bool leftArm, NativeArray<Vector3> verts, float percentDown)
        {
            var upperBone = leftArm ? ovrAvatar2JointType.LeftArmUpper : ovrAvatar2JointType.RightArmUpper;
            var lowerBone = leftArm ? ovrAvatar2JointType.LeftArmLower : ovrAvatar2JointType.RightArmLower;
            var wristBone = leftArm ? ovrAvatar2JointType.LeftHandWrist : ovrAvatar2JointType.RightHandWrist;

            var upperBoneInfo = avatarMeshQuery.GetBoneInfo(upperBone);
            var lowerBoneInfo = avatarMeshQuery.GetBoneInfo(lowerBone);
            var wristBoneInfo = avatarMeshQuery.GetBoneInfo(wristBone);

            if (upperBoneInfo == null || !upperBoneInfo.HasValue)
                return null;
            if (lowerBoneInfo == null || !lowerBoneInfo.HasValue)
                return null;
            if (wristBoneInfo == null || !wristBoneInfo.HasValue)
                return null;

            var upperPose = upperBoneInfo.Value.inverseBindPose.inverse;
            var lowerPose = lowerBoneInfo.Value.inverseBindPose.inverse;
            var wristPose = wristBoneInfo.Value.inverseBindPose.inverse;

            var point = Vector3.LerpUnclamped(upperPose.GetPosition(), wristPose.GetPosition(), percentDown);
            var normal = lowerPose.MultiplyVector(Vector3.right);
            var planePoints = avatarMeshQuery.GetPlanePoints(verts, point, normal).ToList();
            if (planePoints.Count == 0)
                return null;

            var edge = planePoints.MaxByOrDefault(p => (p - point).sqrMagnitude);
            var up = lowerPose.MultiplyVector(Vector3.forward);
            var topDistance = planePoints.Max(p => Vector3.Dot(up, p - point));
            var bottomDistance = planePoints.Max(p => Vector3.Dot(-up, p - point));
            var topPoint = point + up * topDistance;
            var radius = Vector3.Dot(up, topPoint - lowerPose.GetPosition());
            return (topPoint, point - up * bottomDistance, radius);
        }

        public static ArmDetails GetArmDetails(this AvatarMeshQuery avatarMeshQuery, bool leftArm, float upperPercent, float lowerPercent)
        {
            if (!avatarMeshQuery.VertexCount.HasValue)
            {
                return new ArmDetails();
            }

            var verts = avatarMeshQuery.GetArmVertices(true).ToTempArray(avatarMeshQuery.VertexCount.Value);
            var (lowerTop, lowerBottom, lowerRadius) = avatarMeshQuery.GetArmPartDetails(leftArm, verts, lowerPercent);
            var (upperTop, upperBottom, upperRadius) = avatarMeshQuery.GetArmPartDetails(leftArm, verts, upperPercent);
            return new ArmDetails(upperTop, upperBottom, lowerTop, lowerBottom, upperRadius, lowerRadius);
        }

        public static IEnumerable<Vector3> GetArmVertices(this AvatarMeshQuery avatarMeshQuery, bool leftArm) =>
            GetArmVertices(avatarMeshQuery, leftArm ? LEFT_ARM_BONE_PREFIX : RIGHT_ARM_BONE_PREFIX);

        private static IEnumerable<Vector3> GetArmVertices(this AvatarMeshQuery avatarMeshQuery, string boneNamePrefix) =>
            avatarMeshQuery.GetAllVertices().
            Where(vert => vert.BoneName.StartsWith(boneNamePrefix)).Select(vert => vert.VertPosition);
    }
}
