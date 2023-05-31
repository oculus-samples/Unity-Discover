using System;
using UnityEngine;

/**
 * @file OvrAvatarTrackingPose.cs
 */
namespace Oculus.Avatar2
{
    /**
     * The purpose of this struct is to wrap CAPI.ovrAvatar2TrackingBodyPose,
     * so that application code can write pose data into
     * a native ovrAvatar2TrackingBodyPose struct owned by the avatar SDK.
     * @see OvrAvatarBodyTrackingContextBase
     * @see CAPI.ovrAvatar2TrackingBodyPose
     */
    public ref struct OvrAvatarTrackingPose
    {
        /// Coordinate space of this pose (local or object).
        public CAPI.ovrAvatar2Space space;

        /// A reference to an array of bone transforms inside the underlying native struct.
        public readonly OvrSpan<CAPI.ovrAvatar2Transform> transforms;

        /**
         * Set bone transform by index.
         * @param newTransform  new transform.
         * @param index         which index in array to copy to.
         * @returns true if index is valid, false otherwise.
         */
        public bool SetTransform(CAPI.ovrAvatar2Transform newTransform, int index)
        {
            if (index < 0 || index >= transforms.Length)
            {
                return false;
            }
            unsafe
            {
                CAPI.ovrAvatar2Transform* ptr = (CAPI.ovrAvatar2Transform*)transforms.Address;
                *(ptr + index) = newTransform;
            }
            return true;
        }

        /**
         * Set transform of multiple bones.
         * @param newTransforms     new transform array.
         * @param offset            offset index to start copy.
         * @param count             number of transforms to copy.
         * @returns true if copy succeed, false otherwise.
         */
        public bool SetTransforms(CAPI.ovrAvatar2Transform[] newTransforms, int offset, int count)
        {
            if (offset < 0 || count <= 0 || (offset + count) > newTransforms.Length)
            {
                return false;
            }
            unsafe
            {
                CAPI.ovrAvatar2Transform* ptr = (CAPI.ovrAvatar2Transform*)transforms.Address;
                for (int i = offset; i < offset + count; ++i)
                {
                    *(ptr + i) = newTransforms[i];
                }
            }
            return true;
        }

        /**
         * Create a C# struct from a native pose.
         */
        internal OvrAvatarTrackingPose(ref CAPI.ovrAvatar2TrackingBodyPose pose)
        {
            space = pose.space;
            unsafe
            {
                transforms = new OvrSpan<CAPI.ovrAvatar2Transform>(pose.bones, (int)pose.numBones);
            }
        }

        /**
         * Copy the pose from this C# instance to the native pose provided.
         * @param native    where to store the native pose copied.
         * @see CAPI.ovrAvatar2TrackingBodyPose
         */
        internal void CopyToNative(ref CAPI.ovrAvatar2TrackingBodyPose native)
        {
            unsafe
            {
                Debug.Assert(transforms.Length == native.numBones);
                CAPI.ovrAvatar2Transform* ptr = (CAPI.ovrAvatar2Transform*)transforms.Address;
                Debug.Assert(ptr == native.bones);
            }
            native.space = space;
        }

        /**
         * Create a native pose from this C# instance.
         * @returns native pose copied.
         * @see CAPI.ovrAvatar2TrackingBodyPose
         */
        internal CAPI.ovrAvatar2TrackingBodyPose GetNative()
        {
            unsafe
            {
                CAPI.ovrAvatar2Transform* ptr = (CAPI.ovrAvatar2Transform*)transforms.Address;
                return new CAPI.ovrAvatar2TrackingBodyPose(ptr, (UInt32)transforms.Length)
                {
                    space = space
                };
            }
        }

        /**
         * Copy the native pose provided to this C# instance.
         * @param native   native pose to be copied.
         * @see CAPI.ovrAvatar2TrackingBodyPose
         */
        internal void CopyFromNative(ref CAPI.ovrAvatar2TrackingBodyPose native)
        {
            unsafe
            {
                Debug.Assert(transforms.Length == native.numBones);
                CAPI.ovrAvatar2Transform* ptr = (CAPI.ovrAvatar2Transform*)transforms.Address;
                Debug.Assert(ptr == native.bones);
            }
            space = native.space;
        }
    }
}
