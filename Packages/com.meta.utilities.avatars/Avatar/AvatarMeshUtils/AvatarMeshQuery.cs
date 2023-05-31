// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Linq;
using Meta.Utilities;
using Oculus.Avatar2;
using Unity.Collections;
using UnityEngine;
using static Oculus.Avatar2.CAPI;

#if HAS_UNITY_BURST
using Unity.Mathematics;
#endif

namespace Meta.Utilities.Avatars
{
    /// <summary>
    /// Allows scripts to access an avatar entity's mesh data such as vertices and bones.
    /// </summary>
#if HAS_UNITY_BURST
    [Unity.Burst.BurstCompile]
#endif
    public class AvatarMeshQuery : MonoBehaviour
    {
        [SerializeField, AutoSet] private OvrAvatarEntity m_avatarEntity;
        [SerializeField] private ovrAvatar2JointType[] m_criticalJoints;
        [SerializeField]
        private int m_armCheckPlanePointsCount = 16;
        [SerializeField]
        private float m_armCheckMaxDist = 1f;
        private OvrAvatarPrimitive m_primitive;
        private Dictionary<ovrAvatar2JointType, (uint index, Matrix4x4 inverseBindPose)?> m_boneInfo = new();
        private AvatarMeshCache.MeshData m_avatarMeshData;

        [SerializeField] private ovrAvatar2EntityViewFlags m_requiredViewFlags = ovrAvatar2EntityViewFlags.FirstPerson;
        public int? VertexCount => m_avatarMeshData.Positions?.Length;
        public int? WeightCount => m_avatarMeshData.Weights?.Length;

        public event Action OnMeshDataAcquired;

#if HAS_NAUGHTY_ATTRIBUTES
        [NaughtyAttributes.ShowNativeProperty]
#endif
        public bool HasMeshData => VertexCount > 0 && WeightCount > 0;

        private void OnEnable()
        {
            AvatarMeshCache.WhenInstantiated(cache =>
            {
                if (isActiveAndEnabled)
                {
                    cache.OnMeshLoaded += OnMeshLoaded;
                }
            });
        }
        private void OnDisable()
        {
            AvatarMeshCache.Instance.OnMeshLoaded -= OnMeshLoaded;
        }

        private void OnMeshLoaded(OvrAvatarPrimitive loadedPrim)
        {
            var avatarEntity = (AvatarEntity)m_avatarEntity;
            _ = StartCoroutine(new WaitWhile(() => m_avatarEntity.IsPendingAvatar || m_avatarEntity.IsApplyingModels || !avatarEntity.HasDoneAvatarCheck).Then(() =>
            {
                UpdateMeshData(loadedPrim);
            }));
        }

        private void UpdateMeshData(OvrAvatarPrimitive prim)
        {
            var mesh = prim.mesh;
            var filters = m_avatarEntity.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Select(f => f.sharedMesh).Contains(mesh) &&
                prim.lodFlags == ovrAvatar2EntityLODFlags.LOD_0 &&
                (prim.viewFlags & m_requiredViewFlags) == m_requiredViewFlags)
            {
                var meshData = AvatarMeshCache.Instance.GetMeshData(prim);
                if (meshData.HasValue)
                {
                    m_primitive = prim;
                    m_boneInfo.Clear();
                    foreach (var joint in m_criticalJoints)
                    {
                        m_boneInfo[joint] = GetBoneInfo(joint);
                    }
                    m_avatarMeshData = meshData.Value;
                    OnMeshDataAcquired?.Invoke();
                }
            }
        }

        #region bone operations
        public (uint index, Matrix4x4 inverseBindPose)? GetBoneInfo(ovrAvatar2JointType jointType)
        {
            if (m_boneInfo.ContainsKey(jointType) && m_boneInfo[jointType].HasValue)
            {
                return m_boneInfo[jointType];
            }

            var entityId = GetEntityId();
            var nodes = QueryJointTypeNodes(entityId, stackalloc[] { jointType });

            if (nodes?.Length > 0 && nodes[0] is { } node)
            {
                if (ovrAvatar2Entity_GetPose(entityId, out var pose, out _) is ovrAvatar2Result.Success)
                {
                    var index = GetNodeIndex(node, pose);
                    return index.HasValue ? GetBoneIndexFromJointIndex(pose, index.Value) : null;
                }
            }

            return null;
        }
        private Dictionary<uint, uint> GetBoneIndexFromJointIndexMap(in ovrAvatar2Pose pose)
        {
            var dict = new Dictionary<uint, uint>();
            unsafe
            {
                var info = stackalloc ovrAvatar2JointInfo[(int)pose.jointCount];
                var assetId = m_primitive.assetId;
                if (ovrAvatar2Primitive_GetJointInfo(assetId, info, (uint)(pose.jointCount * sizeof(ovrAvatar2JointInfo))) is ovrAvatar2Result.Success)
                {
                    for (var j = 0u; j != pose.jointCount; j += 1)
                    {
                        dict[(uint)info[j].jointIndex] = j;
                    }
                }
            }
            return dict;
        }
        private Dictionary<uint, string> GetBoneIndexNames(in ovrAvatar2Pose pose)
        {
            var jointIndexToBoneIndex = GetBoneIndexFromJointIndexMap(pose);
            var ret = new Dictionary<uint, string>();
            for (var i = 0u; i != pose.jointCount; i += 1)
            {
                if (jointIndexToBoneIndex.TryGetValue(i, out var jointIndex))
                {
                    ret.Add(jointIndex, OvrAvatar2Entity_GetNodeName(GetEntityId(), pose.GetNodeIdAtIndex(i)));
                }
            }
            return ret;
        }
        private (uint index, Matrix4x4 inverseBindPose)? GetBoneIndexFromJointIndex(in ovrAvatar2Pose pose, uint index)
        {
            unsafe
            {
                var info = stackalloc ovrAvatar2JointInfo[(int)pose.jointCount];
                var assetId = m_primitive.assetId;
                if (ovrAvatar2Primitive_GetJointInfo(assetId, info, (uint)(pose.jointCount * sizeof(ovrAvatar2JointInfo))) is ovrAvatar2Result.Success)
                {
                    for (var j = 0u; j != pose.jointCount; j += 1)
                    {
                        if (info[j].jointIndex == index)
                        {
                            return (j, info[j].inverseBind.ToUnityMatrix());
                        }
                    }
                }
            }
            return null;
        }
        #endregion

        #region node operations
        private Dictionary<ovrAvatar2NodeId, uint> GetNodeIdToJointIndexMap(in ovrAvatar2Pose pose)
        {
            var dict = new Dictionary<ovrAvatar2NodeId, uint>();
            for (var i = 0u; i != pose.jointCount; i += 1)
            {
                dict.Add(pose.GetNodeIdAtIndex(i), i);
            }
            return dict;
        }
        private static uint? GetNodeIndex(ovrAvatar2NodeId node, in ovrAvatar2Pose pose)
        {
            for (var i = 0u; i != pose.jointCount; i += 1)
            {
                var id = pose.GetNodeIdAtIndex(i);
                if (id == node)
                    return i;
            }
            return null;
        }
        private static ovrAvatar2NodeId[] QueryJointTypeNodes(ovrAvatar2EntityId entityId, Span<ovrAvatar2JointType> jointTypes) =>
            OvrAvatar2Entity_QueryJointTypeNodes(entityId, jointTypes.AsNativeSlice());
        #endregion

        #region public getter methods
        public IEnumerable<Vector3> GetPlanePoints(NativeArray<Vector3> verts, Vector3 planePoint, Vector3 planeNormal)
        {
            var ptsA = verts.
                Where(p => Vector3.Dot(planePoint - p, planeNormal) is { } distance && distance < 0 && distance > -m_armCheckMaxDist).
                OrderByDescending(p => Vector3.Dot(planePoint - p, planeNormal)).
                Take(m_armCheckPlanePointsCount).
                ToTempArray(m_armCheckPlanePointsCount);
            var ptsB = verts.
                Where(p => Vector3.Dot(planePoint - p, planeNormal) is { } distance && distance > 0 && distance < m_armCheckMaxDist).
                OrderBy(p => Vector3.Dot(planePoint - p, planeNormal)).
                Take(m_armCheckPlanePointsCount).
                ToTempArray(m_armCheckPlanePointsCount);
            foreach (var a in ptsA)
                foreach (var b in ptsB)
                    if (IntersectLinePlane(a, b, planePoint, planeNormal) is { } value)
                        yield return value;
        }

        public IEnumerable<(string BoneName, Vector3 VertPosition, float VertBoneWeight)> GetAllVertices()
        {
            _ = ovrAvatar2Entity_GetPose(GetEntityId(), out var pose, out _);
            var boneNames = GetBoneIndexNames(pose);

            foreach (var (pos, weight) in m_avatarMeshData.Positions.Zip(m_avatarMeshData.Weights))
            {
                if (boneNames.TryGetValue((uint)weight.boneIndex0, out var boneName))
                {
                    yield return (boneName, pos, weight.weight0);
                }
            }
        }

        public Transform GetJointTransform(ovrAvatar2JointType joint)
        {
            if (m_avatarEntity is AvatarEntity avatarEntity)
                return avatarEntity.GetJointTransform(joint);
#if UNITY_EDITOR
            return m_avatarEntity.GetMethod<Func<ovrAvatar2JointType, Transform>>("GetSkeletonTransformByType")?.Invoke(joint);
#else
            return null;
#endif
        }
        #endregion

#if HAS_UNITY_BURST
        [Unity.Burst.BurstCompile]
#endif
        private static bool IntersectLinePlane(in float3 point1, in float3 point2, in float3 planePoint, in float3 planeNormal, out float3 intersection)
        {
            var u = point2 - point1;
            var w = point1 - planePoint;

            var d = math.dot(planeNormal, u);
            var n = -math.dot(planeNormal, w);

            if (math.abs(d) < math.EPSILON)   // if line is parallel & co-planar to plane
            {
                intersection = planePoint;
                return n == 0;
            }

            var sI = n / d;

            intersection = point1 + sI * u;
            return sI is >= 0 and <= 1;
        }

        private Vector3? IntersectLinePlane(Vector3 point1, Vector3 point2, Vector3 planePoint, Vector3 planeNormal) =>
            IntersectLinePlane(point1, point2, planePoint, planeNormal, out var intersection) ? intersection : null;

        private ovrAvatar2EntityId GetEntityId()
        {
            if (m_avatarEntity is AvatarEntity ent)
                return ent.EntityId;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            return (ovrAvatar2EntityId)m_avatarEntity.GetProperty("entityId");
#else
            return ovrAvatar2EntityId.Invalid;
#endif
        }
    }
}
