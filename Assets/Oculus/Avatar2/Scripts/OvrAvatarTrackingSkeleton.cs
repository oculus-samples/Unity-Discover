using System;
using UnityEngine;

/**
 * @file OvrAvatarTrackingSkeleton.cs
 */
namespace Oculus.Avatar2
{
    /**
     * Describes a contiguous array of structures.
     * Used to transfer data between C# and native.
     */
    public ref struct OvrSpan<T> where T : struct
    {
        private readonly IntPtr _ptr;
        private readonly int _length;

        public IntPtr Address => _ptr;
        public int Length => _length;

        public unsafe OvrSpan(void* ptr, int length)
        {
            _ptr = new IntPtr(ptr);
            _length = length;
        }
    }

    /**
     * The purpose of this struct is to wrap CAPI.ovrAvatar2TrackingBodySkeleton,
     * so that application code can write skeleton and reference pose data into
     * a native ovrAvatar2TrackingBodySkeleton struct owned by the avatar SDK.
     * @see OvrAvatarBodyTrackingContextBase
     * @see CAPI.ovrAvatar2TrackingBodySkeleton
     */
    public ref struct OvrAvatarTrackingSkeleton
    {
        /// Vector giving the forward bone direction.
        public CAPI.ovrAvatar2Vector3f forwardDir;

        /// Indices for each bone and its corresponding parent bone.
        /// This describes the structure of the skeleton.
        public readonly OvrSpan<CAPI.ovrAvatar2Bone> bones;

        /// Reference pose for the skeleton. This is the pose it is
        /// in if no external transformations are applied.
        public OvrAvatarTrackingPose referencePose;

        /**
         * Set bone by index
         * @param newBone   new bone
         * @param index     where to copy
         * @returns true if index is valid, false otherwise
         */
        public bool SetBone(CAPI.ovrAvatar2Bone newBone, int index)
        {
            if (index < 0 || index >= bones.Length)
            {
                return false;
            }
            unsafe
            {
                CAPI.ovrAvatar2Bone* ptr = (CAPI.ovrAvatar2Bone*)bones.Address.ToPointer();
                *(ptr + index) = newBone;
            }
            return true;
        }

        /**
         * Set multiple bones
         * @param newBones      new bone array
         * @param offset        offset index to start copy
         * @param count         number of bones to copy
         * @returns true if copy succeed, false otherwise
         */
        public bool SetBones(CAPI.ovrAvatar2Bone[] newBones, int offset, int count)
        {
            if (offset < 0 || count <= 0 || (offset + count) > newBones.Length)
            {
                return false;
            }
            unsafe
            {
                CAPI.ovrAvatar2Bone* ptr = (CAPI.ovrAvatar2Bone*)bones.Address.ToPointer();
                for (int i = offset; i < offset + count; ++i)
                {
                    *(ptr + i) = newBones[i];
                }
            }
            return true;
        }

        /**
         * Constructs a C# skeleton from a native skeleton.
         * @param skeleton  native skeleton to copy.
         * @see CAPI.ovrAvatar2TrackingBodySkeleton
         */
        internal OvrAvatarTrackingSkeleton(ref CAPI.ovrAvatar2TrackingBodySkeleton skeleton)
        {
            forwardDir = skeleton.forwardDir;
            referencePose = new OvrAvatarTrackingPose(ref skeleton.referencePose);
            unsafe
            {
                bones = new OvrSpan<CAPI.ovrAvatar2Bone>(skeleton.bones, (int)skeleton.numBones);
            }
        }

        /**
         * Copy the skeleton from this C# instance to a native skeleton.
         * @param skeleton  native skeleton to update.
         * @see CAPI.ovrAvatar2TrackingBodySkeleton
         * @see GetNative
         * @see FromNative
         */
        internal void CopyToNative(ref CAPI.ovrAvatar2TrackingBodySkeleton native)
        {
            unsafe
            {
                Debug.Assert(bones.Length == native.numBones);
                CAPI.ovrAvatar2Bone* ptr = (CAPI.ovrAvatar2Bone*)bones.Address.ToPointer();
                Debug.Assert(ptr == native.bones);
            }
            native.forwardDir = forwardDir;
            referencePose.CopyToNative(ref native.referencePose);
        }

        /**
         * Create a native skeleton from this C# instance.
         * @returns native skeleton structure.
         * @see CAPI.ovrAvatar2TrackingBodySkeleton
         * @see CopyToNative
         * @see FromNative
         */
        internal CAPI.ovrAvatar2TrackingBodySkeleton GetNative()
        {
            unsafe
            {
                CAPI.ovrAvatar2Bone* ptr = (CAPI.ovrAvatar2Bone*)bones.Address.ToPointer();
                return new CAPI.ovrAvatar2TrackingBodySkeleton(ptr, (UInt32)bones.Length, referencePose.GetNative())
                {
                    forwardDir = forwardDir
                };
            }
        }

        /**
         * Copy a native skeleton to this C# instance.
         * @param skeleton  native skeleton to copy.
         * @see CAPI.ovrAvatar2TrackingBodySkeleton
         * @see CopyToNative
         * @see GetNative
         */
        internal void CopyFromNative(ref CAPI.ovrAvatar2TrackingBodySkeleton native)
        {
            unsafe
            {
                Debug.Assert(bones.Length == native.numBones);
                CAPI.ovrAvatar2Bone* ptr = (CAPI.ovrAvatar2Bone*)bones.Address.ToPointer();
                Debug.Assert(ptr == native.bones);
            }
            forwardDir = native.forwardDir;
            referencePose.CopyFromNative(ref native.referencePose);
        }
    }
}
