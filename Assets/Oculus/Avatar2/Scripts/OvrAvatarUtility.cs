using System.Diagnostics.Contracts;

using UnityEngine;

namespace Oculus.Avatar2
{
    public static class OvrAvatarConversions
    {
        // TODO: Make internal, used by BodyAnimTracking.Update()
        public static CAPI.ovrAvatar2Transform ConvertSpace(this in CAPI.ovrAvatar2Transform from)
        {
            return new CAPI.ovrAvatar2Transform(
                from.position.ConvertSpace(), from.orientation.ConvertSpace(), in from.scale);
        }

        // TODO: Make internal, used by BodyAnimTracking.Start()
        public static CAPI.ovrAvatar2Quatf ConvertSpace(this in CAPI.ovrAvatar2Quatf from)
        {
            return new CAPI.ovrAvatar2Quatf(-from.x, -from.y, from.z, from.w);
        }

        // TODO: Make internal, used by BodyAnimTracking.Start()
        public static CAPI.ovrAvatar2Vector3f ConvertSpace(this in CAPI.ovrAvatar2Vector3f from)
        {
            return new CAPI.ovrAvatar2Vector3f(from.x, from.y, -from.z);
        }

        // TODO: Make internal, used by BodyAnimTracking.Update()
        public static CAPI.ovrAvatar2Transform ConvertSpace(this Transform from)
        {
            return new CAPI.ovrAvatar2Transform(
                from.localPosition.ConvertSpace(), from.localRotation.ConvertSpace(), (CAPI.ovrAvatar2Vector3f)from.localScale);
        }

        internal static CAPI.ovrAvatar2Quatf ConvertSpace(this in Quaternion from)
        {
            return new CAPI.ovrAvatar2Quatf(-from.x, -from.y, from.z, from.w);
        }

        internal static CAPI.ovrAvatar2Vector3f ConvertSpace(this in Vector3 from)
        {
            return new CAPI.ovrAvatar2Vector3f(from.x, from.y, -from.z);
        }

        internal static void ApplyWorldOvrTransform(this Transform transform, in CAPI.ovrAvatar2Transform from)
        {
            transform.position = from.position;
            transform.rotation = from.orientation;
            transform.localScale = from.scale;
        }

        internal static void ApplyOvrTransform(this Transform transform, in CAPI.ovrAvatar2Transform from)
        {
            // NOTE: We could route this to the * version, but `fixed` has non-trivial overhead
            transform.localPosition = from.position;
            transform.localRotation = from.orientation;
            transform.localScale = from.scale;
        }

        internal unsafe static void ApplyOvrTransform(this Transform transform, CAPI.ovrAvatar2Transform* from)
        {
            transform.localPosition = from->position;
            transform.localRotation = from->orientation;
            transform.localScale = from->scale;
        }

        internal static CAPI.ovrAvatar2Transform ToWorldOvrTransform(this Transform t)
        {
            return new CAPI.ovrAvatar2Transform(t.position, t.rotation, t.localScale);
        }

        internal static Matrix4x4 ToMatrix(this in CAPI.ovrAvatar2Transform t)
        {
            return Matrix4x4.TRS(t.position, t.orientation, t.scale);
        }
    }

    public static class OvrAvatarUtility
    {
        public static CAPI.ovrAvatar2Transform CombineOvrTransforms(
            in CAPI.ovrAvatar2Transform parent, in CAPI.ovrAvatar2Transform child)
        {
            var scaledChildPose = new CAPI.ovrAvatar2Vector3f
            {
                x = child.position.x * parent.scale.x,
                y = child.position.y * parent.scale.y,
                z = child.position.z * parent.scale.z
            };

            var parentQuat = (Quaternion)parent.orientation;
            var result = new CAPI.ovrAvatar2Transform
            {
                position =
                  parent.position + (CAPI.ovrAvatar2Vector3f) (parentQuat * scaledChildPose),

                orientation = parentQuat * child.orientation,

                scale = new CAPI.ovrAvatar2Vector3f
                {
                    x = parent.scale.x * child.scale.x,
                    y = parent.scale.y * child.scale.y,
                    z = parent.scale.z * child.scale.z
                }
            };
            OvrAvatarLog.Assert(!result.position.IsNaN());
            OvrAvatarLog.Assert(!result.orientation.IsNaN());
            OvrAvatarLog.Assert(!result.scale.IsNaN());

            OvrAvatarLog.Assert(result.orientation.IsNormalized());

            return result;
        }

        [Pure]
        public static string GetAsString(this in CAPI.ovrAvatar2Transform transform, int decimalPlaces = 2)
        {
            string format = "F" + decimalPlaces;
            return $"{((Vector3)transform.position).ToString(format)}, {((Quaternion)transform.orientation).eulerAngles.ToString(format)}, {((Vector3)transform.scale).ToString(format)}";
        }

        [Pure]
        public static bool IsNaN(this in CAPI.ovrAvatar2Vector3f v)
        {
            return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z);
        }

        [Pure]
        public static bool IsNaN(this in CAPI.ovrAvatar2Quatf q)
        {
            return float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w);
        }

        [Pure]
        public static bool IsNan(this in CAPI.ovrAvatar2Transform t)
        {
            return t.position.IsNaN() || t.orientation.IsNaN() || t.scale.IsNaN();
        }

        [Pure]
        public static bool IsZero(this in CAPI.ovrAvatar2Vector3f vec)
            => vec.x == 0.0f && vec.y == 0.0f && vec.z == 0.0f;

        [Pure]
        public static bool IsOne(this in CAPI.ovrAvatar2Vector3f vec)
            => vec.x == 1.0f && vec.y == 1.0f && vec.z == 1.0f;

        [Pure]
        public static bool IsIdentity(this in CAPI.ovrAvatar2Quatf quat)
            => quat.x == 0.0f && quat.y == 0.0f && quat.z == 0.0f && (quat.w == 1.0f || quat.w == -1.0f);

        [Pure]

        public static bool IsNormalized(this in CAPI.ovrAvatar2Quatf quat)
            => Mathf.Approximately(quat.LengthSquared, 1.0f);


        [Pure]
        public static bool IsIdentity(this in CAPI.ovrAvatar2Transform transform)
            => transform.position.IsZero() && transform.orientation.IsIdentity() && transform.scale.IsOne();

        public static Matrix4x4 ToUnityMatrix(this in CAPI.ovrAvatar2Matrix4f m)
        {
            m.CopyToUnityMatrix(out var unityM);
            return unityM;
        }

        public static CAPI.ovrAvatar2Matrix4f ToAvatarMatrix(this in Matrix4x4 m)
        {
            m.CopyToAvatarMatrix(out var avatarMat);
            return avatarMat;
        }

        public static void CopyToUnityMatrix(this in CAPI.ovrAvatar2Matrix4f m, out Matrix4x4 unityMatrix)
        {
            unityMatrix.m00 = m.m00;
            unityMatrix.m10 = m.m10;
            unityMatrix.m20 = m.m20;
            unityMatrix.m30 = m.m30;
            unityMatrix.m01 = m.m01;
            unityMatrix.m11 = m.m11;
            unityMatrix.m21 = m.m21;
            unityMatrix.m31 = m.m31;
            unityMatrix.m02 = m.m02;
            unityMatrix.m12 = m.m12;
            unityMatrix.m22 = m.m22;
            unityMatrix.m32 = m.m32;
            unityMatrix.m03 = m.m03;
            unityMatrix.m13 = m.m13;
            unityMatrix.m23 = m.m23;
            unityMatrix.m33 = m.m33;
        }

        public static void CopyToAvatarMatrix(this in Matrix4x4 unityMatrix, out CAPI.ovrAvatar2Matrix4f m)
        {
            m.m00 = unityMatrix.m00;
            m.m10 = unityMatrix.m10;
            m.m20 = unityMatrix.m20;
            m.m30 = unityMatrix.m30;
            m.m01 = unityMatrix.m01;
            m.m11 = unityMatrix.m11;
            m.m21 = unityMatrix.m21;
            m.m31 = unityMatrix.m31;
            m.m02 = unityMatrix.m02;
            m.m12 = unityMatrix.m12;
            m.m22 = unityMatrix.m22;
            m.m32 = unityMatrix.m32;
            m.m03 = unityMatrix.m03;
            m.m13 = unityMatrix.m13;
            m.m23 = unityMatrix.m23;
            m.m33 = unityMatrix.m33;
        }
    }
}
