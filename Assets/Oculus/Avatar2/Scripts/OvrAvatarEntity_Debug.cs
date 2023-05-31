using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Oculus.Avatar2
{
    public partial class OvrAvatarEntity
    {
        public string debugEntityRenderState()
        {
            if (QueryEntityRenderState(out var entityState))
            {
                return debugEntityRenderState(in entityState);
            }
            return string.Empty;
        }
        public string debugEntityRenderState(in CAPI.ovrAvatar2EntityRenderState entityState)
        {
            string builder = "";
            string sep = "";
            for (uint idx = 0; idx < entityState.primitiveCount; idx++)
            {
                builder += sep + debugPrimitiveRenderStateString(idx);
                sep = ",\n";
            }
            return builder;
        }

        public string debugPrimitiveRenderStateString(uint primIdx)
        {
            if (QueryPrimitiveRenderState_Direct(primIdx, out var renderState))
            {
                return $"<{primIdx}-{debugPrimitiveRenderStateString(in renderState)}>";
            }
            return string.Empty;
        }
        public string debugPrimitiveRenderStateString(in CAPI.ovrAvatar2PrimitiveRenderState renderState)
        {
            return $"id:{renderState.id},primId:{renderState.primitiveId},nodeId:{renderState.meshNodeId}";
        }

        public string debugPrimitiveRenderFlagsString(uint primIdx)
        {
            if (QueryPrimitiveRenderState_Direct(primIdx, out var renderState))
            {
                return $"<{primIdx}-{debugPrimitiveRenderFlagsString(renderState.primitiveId)}>";
            }
            return string.Empty;
        }
        public string debugPrimitiveRenderFlagsString(CAPI.ovrAvatar2Id primitiveId)
        {
            if (CAPI.ovrAvatar2Asset_GetViewFlags(primitiveId, out var viewFlags)
                .EnsureSuccess("ovrAvatar2Asset_GetViewFlags") &&
                CAPI.ovrAvatar2Asset_GetLodFlags(primitiveId, out var lodFlags)
                .EnsureSuccess("ovrAvatar2Asset_GetLodFlags"))
            {
                return $"view:{viewFlags},lod:{lodFlags}";
            }
            return string.Empty;
        }

        public Dictionary<CAPI.ovrAvatar2JointType, string> debugJointNamesForTypes()
        {
            // TODO: Remove pose dependency when GetNameForNode is added
            if (!QueryEntityPose(out var pose, out var hierVer)) { return null; }

            int jointTypeCount = (int)CAPI.ovrAvatar2JointType.Count;
            var buildDict = new Dictionary<CAPI.ovrAvatar2JointType, string>(jointTypeCount);
            var allJointTypes = new CAPI.ovrAvatar2JointType[jointTypeCount];
            for (int idx = 0; idx < allJointTypes.Length; ++idx)
            {
                allJointTypes[idx] = (CAPI.ovrAvatar2JointType)idx;
            }

            var allJointNames = CAPI.OvrAvatar2Entity_QueryJointTypeNodes(entityId, allJointTypes, this);

            for (int idx = 0; idx < jointTypeCount; ++idx)
            {
                buildDict.Add(allJointTypes[idx], GetNameForNode(allJointNames[idx], in pose));
            }
            return buildDict;
        }

        // TODO: Create generic LoadUri method which StressReloadingAvatar may use instead
        protected internal bool StressReloading_LoadUri(string uri)
        {
            var result = CAPI.OvrAvatarEntity_LoadUri(entityId, uri, out var loadRequestId);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogWarning($"LoadUri failed: {result}");
                return false;
            }
            OvrAvatarManager.Instance.RegisterLoadRequest(this, loadRequestId);

            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        #region Game Debug Drawing
        private static Material _debugLineMat = null;
        private Material DebugLineMat
        {
            get
            {
                if (_debugLineMat == null)
                {
                    _debugLineMat = new Material(Shader.Find("Hidden/Internal-Colored"));
                    _debugLineMat.SetInt("_ZTest", 0);
                }
                return _debugLineMat;
            }
        }

        // OnPostRender only works when the Component is a child of the Camera
        private void OnCameraPostRender(Camera cam)
        {
            if (_debugDrawing.drawSkelHierarchyInGame)
            {
                GameDebugDrawSkelHierarchyInGame();
            }

            if (_debugDrawing.drawSkinTransformsInGame)
            {
                foreach (var meshNodeKVP in _meshNodes)
                {
                    foreach (var prim in meshNodeKVP.Value)
                    {
                        GameDebugDrawSkinTransforms(prim, 0.005f);
                    }
                }
            }
        }

        private void GameDebugDrawSkelHierarchyInGame()
        {
            DebugLineMat.SetPass(0);
            GL.Begin(GL.LINES);
            GL.Color(_debugDrawing.skeletonColor);
            for (int i = 0; i < SkeletonJointCount; ++i)
            {
                SkeletonJoint joint = GetSkeletonJoint(i);
                if (joint.transform == null) { continue; }

                if (joint.parentIndex >= 0)
                {
                    SkeletonJoint parentJoint = GetSkeletonJoint(joint.parentIndex);
                    if (parentJoint.transform == null) { continue; }

                    GL.Vertex(parentJoint.transform.position);
                    GL.Vertex(joint.transform.position);
                }
            }
            GL.End();
        }

        private void GameDebugDrawSkinTransforms(PrimitiveRenderData prd, float locatorScale)
        {
            var scaleNegZ = Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f));
            bool usingJointMonitor = _jointMonitor != null;
            CAPI.ovrAvatar2Pose entityPose = default;
            if (usingJointMonitor)
            {
                if (QueryEntityPose(out entityPose, out var throwaway))
                {
                    unsafe { usingJointMonitor &= entityPose.objectTransforms != null; }
                }
                else
                {
                    usingJointMonitor = false;
                }
            }

            unsafe
            {
                var skinTransformPtr = stackalloc Matrix4x4[prd.primitive.joints.Length];
                var skinTransformSize = sizeof(Matrix4x4) * prd.primitive.joints.Length;
                if (CAPI.ovrAvatar2Render_GetSkinTransforms(entityId, prd.instanceId, new IntPtr(skinTransformPtr), (UInt32)skinTransformSize, false).IsSuccess())
                {
                    DebugLineMat.SetPass(0);
                    GL.Begin(GL.LINES);

                    for (int iJoint = 0; iJoint < prd.primitive.joints.Length; ++iJoint)
                    {
                        var poseIndex = prd.primitive.joints[iJoint];
                        var skinTransform = skinTransformPtr[iJoint];
                        var bindPose = prd.primitive.mesh.bindposes[iJoint];
                        var worldPos = (transform.worldToLocalMatrix * scaleNegZ * skinTransform * bindPose.inverse).MultiplyPoint3x4(Vector3.zero);

                        // skin transform position
                        GL.Color(Color.red);
                        GL.Vertex(worldPos);
                        GL.Vertex(worldPos + Vector3.right * locatorScale);

                        GL.Color(Color.green);
                        GL.Vertex(worldPos);
                        GL.Vertex(worldPos + Vector3.up * locatorScale);

                        GL.Color(Color.blue);
                        GL.Vertex(worldPos);
                        GL.Vertex(worldPos + Vector3.forward * locatorScale);

                        Vector3 jointPos = default;

                        if (usingJointMonitor)
                        {
                            CAPI.ovrAvatar2Transform tx;
                            unsafe { tx = entityPose.objectTransforms[poseIndex].ConvertSpace(); }
                            jointPos = !tx.IsNan() ? (Vector3)tx.position : worldPos;
                        }
                        else
                        {
                            SkeletonJoint joint = GetSkeletonJoint(poseIndex);
                            jointPos = joint.transform != null ? jointPos = joint.transform.position : worldPos;
                        }

                        // line from pose to skin transform
                        GL.Color(Color.magenta);
                        GL.Vertex(worldPos);
                        GL.Vertex(jointPos);
                    }

                    GL.End();
                }
            }
        }
        #endregion
#endif

#if UNITY_EDITOR
        #region Editor Debug Drawing
        private void OnSceneGUI(SceneView sceneView)
        {
            if (!IsCreated) { return; }

            if (_debugDrawing.drawTrackingPose)
            {
                if (CAPI.ovrAvatar2Tracking_GetPose(entityId, out var pose) == CAPI.ovrAvatar2Result.Success)
                {
                    EditorDebugDrawPose(in pose);
                }
            }

            if (_debugDrawing.drawSkelHierarchy)
            {
                EditorDebugDrawSkelHierarchy();
            }
        }

        private const float CRITICAL_JOINT_RADIUS = 0.125f;
        void OnDrawGizmosSelected()
        {
            if (_skeleton == null) { return; }

            if (_debugDrawing.drawCriticalJoints)
            {
                foreach (var jointPose in _monitoredJointPoses)
                {
                    var skelJointTx = GetSkeletonTransformByType(jointPose.jointType);
                    if (skelJointTx)
                    {
                        Gizmos.matrix = skelJointTx.localToWorldMatrix;
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireSphere(Vector3.zero, CRITICAL_JOINT_RADIUS);
                        Gizmos.color = Color.blue;
                        Gizmos.DrawRay(Vector3.zero, Vector3.forward * CRITICAL_JOINT_RADIUS);
                        Gizmos.color = Color.green;
                        Gizmos.DrawRay(Vector3.zero, Vector3.up * CRITICAL_JOINT_RADIUS);
                    }
                }
            }
        }

        private void EditorDebugDrawPose(in CAPI.ovrAvatar2Pose pose)
        {
            uint count = pose.jointCount;
            Color color = Color.blue;

            // Draw the skeleton in the entity space
            Handles.matrix = transform.localToWorldMatrix;

            for (int i = 0; i < count; ++i)
            {
                unsafe
                {
                    CAPI.ovrAvatar2Transform* jointTransform = pose.objectTransforms + i;
                    EditorDebugDrawOvrTransform(*jointTransform, 0.03f);

                    var jointParentIndex = pose.GetParentIndex(i);
                    if (jointParentIndex >= 0)
                    {
                        CAPI.ovrAvatar2Transform* parentTransform = pose.objectTransforms + jointParentIndex;
                        Handles.color = color;
                        Handles.DrawLine(jointTransform->ConvertSpace().position, parentTransform->ConvertSpace().position);
                    }
                }
            }

            // Reset to world space
            Handles.matrix = Matrix4x4.identity;
        }

        private void EditorDebugDrawSkelHierarchy()
        {
            Color color = _debugDrawing.skeletonColor;
            for (int i = 0; i < SkeletonJointCount; ++i)
            {
                SkeletonJoint joint = GetSkeletonJoint(i);
                if (joint.transform == null) { continue; }

                EditorDebugDrawTransform(joint.transform, 0.03f);
                if (_debugDrawing.drawBoneNames)
                {
                    Handles.color = color;
                    Handles.Label(joint.transform.position, joint.name);
                }

                if (joint.parentIndex >= 0)
                {
                    SkeletonJoint parentJoint = GetSkeletonJoint(joint.parentIndex);
                    if (parentJoint.transform == null) { continue; }

                    Handles.color = color;
                    Handles.DrawLine(joint.transform.position, parentJoint.transform.position);
                }
            }
        }

        private void EditorDebugDrawOvrTransform(CAPI.ovrAvatar2Transform transformToDraw, float scale)
        {
            transformToDraw = transformToDraw.ConvertSpace();
            Vector3 p = transformToDraw.position;
            Quaternion orientation = transformToDraw.orientation;
            Handles.color = Color.red;
            Handles.DrawLine(p, p + (orientation * Vector3.right * scale));
            Handles.color = Color.green;
            Handles.DrawLine(p, p + (orientation * Vector3.up * scale));
            Handles.color = Color.blue;
            Handles.DrawLine(p, p + (orientation * Vector3.forward * scale));
        }

        private void EditorDebugDrawTransform(Transform transformToDraw, float scale)
        {
            Handles.color = Color.red;
            Handles.DrawLine(transformToDraw.position, transformToDraw.position + (transformToDraw.right * scale));
            Handles.color = Color.green;
            Handles.DrawLine(transformToDraw.position, transformToDraw.position + (transformToDraw.up * scale));
            Handles.color = Color.blue;
            Handles.DrawLine(transformToDraw.position, transformToDraw.position + (transformToDraw.forward * scale));
        }
        #endregion
#endif // UNITY_EDITOR
    }
}
