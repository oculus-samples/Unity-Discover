

using System;
using System.Runtime.InteropServices;

namespace Oculus.Avatar2
{

    public partial class CAPI
    {
        //-----------------------------------------------------------------
        //
        // Gaze Targets
        //
        //

        public enum ovrAvatar2GazeTargetType : Int32
        {
            AvatarHead,
            AvatarHand,
            Object,
            ObjectStatic,

            Count
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2GazeTarget
        {
            public ovrAvatar2Id id;
            public ovrAvatar2Vector3f worldPosition;
            public ovrAvatar2GazeTargetType type;
        }

        /// Create gaze targets.
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Behavior_CreateGazeTargets(IntPtr targets, int targetCount);

        /// Destroy gaze targets.
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Behavior_DestroyGazeTargets(IntPtr targets, int targetCount);

        /// Update position of gaze targets.
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal unsafe static extern ovrAvatar2Result ovrAvatar2Behavior_UpdateGazeTargetPositions(CAPI.ovrAvatar2GazeTarget* targets, int targetCount);

        /// Get the position the avatar is looking at
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Behavior_GetGazePos(
            ovrAvatar2EntityId entityId, ref ovrAvatar2Vector3f outPos);


        //-----------------------------------------------------------------
        //
        // Custom Hands
        //
        //

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result
            ovrAvatar2_SetCustomWristOffset(
                ovrAvatar2EntityId entityId,
                ovrAvatar2Side side,
                in ovrAvatar2Transform offset);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result
            ovrAvatar2_SetCustomHandSkeleton(
                ovrAvatar2EntityId entityId,
                ovrAvatar2Side side,
                in ovrAvatar2TrackingBodySkeleton skeleton);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result
        ovrAvatar2_SetCustomHandPose(
            ovrAvatar2EntityId entityId,
            ovrAvatar2Side side,
            in ovrAvatar2TrackingBodyPose pose);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern ovrAvatar2Result
        ovrAvatar2_ClearCustomHandPose(ovrAvatar2EntityId entityId, ovrAvatar2Side side);

    }
}
