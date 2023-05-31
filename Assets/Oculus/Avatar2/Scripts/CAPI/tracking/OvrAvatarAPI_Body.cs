using System;
using System.Runtime.InteropServices;

namespace Oculus.Avatar2
{
    public partial class CAPI
    {
        enum ovrAvatar2HandTrackingBoneId : Int32
        {
            Invalid = -1,

            Start = 0,
            LeftHandThumbTrapezium = Start + 0,
            LeftHandThumbMeta = Start + 1,
            LeftHandThumbProximal = Start + 2,
            LeftHandThumbDistal = Start + 3,
            LeftHandIndexProximal = Start + 4,
            LeftHandIndexIntermediate = Start + 5,
            LeftHandIndexDistal = Start + 6,
            LeftHandMiddleProximal = Start + 7,
            LeftHandMiddleIntermediate = Start + 8,
            LeftHandMiddleDistal = Start + 9,
            LeftHandRingProximal = Start + 10,
            LeftHandRingIntermediate = Start + 11,
            LeftHandRingDistal = Start + 12,
            LeftHandPinkyMeta = Start + 13,
            LeftHandPinkyProximal = Start + 14,
            LeftHandPinkyIntermediate = Start + 15,
            LeftHandPinkyDistal = Start + 16,
            RightHandThumbTrapezium = Start + 17,
            RightHandThumbMeta = Start + 18,
            RightHandThumbProximal = Start + 19,
            RightHandThumbDistal = Start + 20,
            RightHandIndexProximal = Start + 21,
            RightHandIndexIntermediate = Start + 22,
            RightHandIndexDistal = Start + 23,
            RightHandMiddleProximal = Start + 24,
            RightHandMiddleIntermediate = Start + 25,
            RightHandMiddleDistal = Start + 26,
            RightHandRingProximal = Start + 27,
            RightHandRingIntermediate = Start + 28,
            RightHandRingDistal = Start + 29,
            RightHandPinkyMeta = Start + 30,
            RightHandPinkyProximal = Start + 31,
            RightHandPinkyIntermediate = Start + 32,
            RightHandPinkyDistal = Start + 33,
            Count = Start + 34,
        }

        internal const int MaxHandBones = (int)ovrAvatar2HandTrackingBoneId.Count;

        // TODO: Convert to unsafe fixed arrays
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2HandTrackingState
        {
            public ovrAvatar2Transform wristPosLeft;
            public ovrAvatar2Transform wristPosRight;

            public ovrAvatar2Quatf boneRotation0;
            public ovrAvatar2Quatf boneRotation1;
            public ovrAvatar2Quatf boneRotation2;
            public ovrAvatar2Quatf boneRotation3;
            public ovrAvatar2Quatf boneRotation4;
            public ovrAvatar2Quatf boneRotation5;
            public ovrAvatar2Quatf boneRotation6;
            public ovrAvatar2Quatf boneRotation7;
            public ovrAvatar2Quatf boneRotation8;
            public ovrAvatar2Quatf boneRotation9;
            public ovrAvatar2Quatf boneRotation10;
            public ovrAvatar2Quatf boneRotation11;
            public ovrAvatar2Quatf boneRotation12;
            public ovrAvatar2Quatf boneRotation13;
            public ovrAvatar2Quatf boneRotation14;
            public ovrAvatar2Quatf boneRotation15;
            public ovrAvatar2Quatf boneRotation16;
            public ovrAvatar2Quatf boneRotation17;
            public ovrAvatar2Quatf boneRotation18;
            public ovrAvatar2Quatf boneRotation19;
            public ovrAvatar2Quatf boneRotation20;
            public ovrAvatar2Quatf boneRotation21;
            public ovrAvatar2Quatf boneRotation22;
            public ovrAvatar2Quatf boneRotation23;
            public ovrAvatar2Quatf boneRotation24;
            public ovrAvatar2Quatf boneRotation25;
            public ovrAvatar2Quatf boneRotation26;
            public ovrAvatar2Quatf boneRotation27;
            public ovrAvatar2Quatf boneRotation28;
            public ovrAvatar2Quatf boneRotation29;
            public ovrAvatar2Quatf boneRotation30;
            public ovrAvatar2Quatf boneRotation31;
            public ovrAvatar2Quatf boneRotation32;
            public ovrAvatar2Quatf boneRotation33;

            public float handScaleLeft;
            public float handScaleRight;
            [MarshalAs(UnmanagedType.U1)] public bool isTrackedLeft;
            [MarshalAs(UnmanagedType.U1)] public bool isTrackedRight;
            [MarshalAs(UnmanagedType.U1)] public bool isConfidentLeft;
            [MarshalAs(UnmanagedType.U1)] public bool isConfidentRight;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool HandStateCallback(out ovrAvatar2HandTrackingState skeleton, IntPtr userContext);

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2HandTrackingDataContext
        {
            public IntPtr context;
            public HandStateCallback handTrackingCallback;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2HandTrackingDataContextNative
        {
            public IntPtr context;
            public IntPtr handTrackingCallback;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal delegate bool InputControlCallback(out ovrAvatar2InputControlState inputControlState, IntPtr userContext);

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2InputControlContext
        {
            public IntPtr context;
            public InputControlCallback inputControlCallback;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2InputControlContextNative
        {
            public IntPtr context;
            public IntPtr inputControlCallback;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal delegate bool InputTrackingCallback(out ovrAvatar2InputTrackingState inputTrackingState, IntPtr userContext);

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2InputTrackingContext
        {
            public IntPtr context;
            public InputTrackingCallback inputTrackingCallback;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2InputTrackingContextNative
        {
            public IntPtr context;
            public IntPtr inputTrackingCallback;
        }

        public enum ovrAvatar2BodyMarkerTypes : Int32
        {
            Hmd,
            LeftController,
            RightController,
            LeftHand,
            RightHand,
        }

        [Flags]
        public enum ovrAvatar2BodyProviderCreateFlags : Int32
        {
            ///< When set, the body context will run in a background thread.
            RunAsync = 1 << 0,
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Body_CreateProvider(
            ovrAvatar2BodyProviderCreateFlags flags, out IntPtr bodyTrackingContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Body_DestroyProvider(IntPtr bodyTrackingContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Body_SetOffset(
            IntPtr bodyTrackingContext,
            ovrAvatar2BodyMarkerTypes type,
            in ovrAvatar2Transform inputTransforms);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Body_SetHandTrackingContext(IntPtr bodyTrackingContext,
            in ovrAvatar2HandTrackingDataContext handContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ovrAvatar2Body_SetHandTrackingContext")]
        internal static extern ovrAvatar2Result ovrAvatar2Body_SetHandTrackingContextNative(
            IntPtr bodyTrackingContext, in ovrAvatar2HandTrackingDataContextNative handContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Body_SetInputControlContext(IntPtr bodyTrackingContext, in ovrAvatar2InputControlContext context);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrAvatar2Body_SetInputControlContext")]
        internal static extern ovrAvatar2Result ovrAvatar2Body_SetInputControlContextNative(IntPtr bodyTrackingContext, in ovrAvatar2InputControlContextNative context);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Body_SetInputTrackingContext(IntPtr bodyTrackingContext, in ovrAvatar2InputTrackingContext context);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrAvatar2Body_SetInputTrackingContext")]
        internal static extern ovrAvatar2Result ovrAvatar2Body_SetInputTrackingContextNative(IntPtr bodyTrackingContext, in ovrAvatar2InputTrackingContextNative context);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Body_InitializeDataContext(
            IntPtr bodyTrackingContext, out ovrAvatar2TrackingDataContext dataContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "ovrAvatar2Body_InitializeDataContext")]
        internal static extern ovrAvatar2Result ovrAvatar2Body_InitializeDataContextNative(
            IntPtr bodyTrackingContext, out ovrAvatar2TrackingDataContextNative dataContext);
    }
}
