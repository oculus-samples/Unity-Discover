using System;
using System.Runtime.InteropServices;

///
/// @file OvrAvatarAPI_Tracking.cs
/// The structures and enums in this file overlay corresponding
/// data maintained in the native avatar SDK implementation.
///
namespace Oculus.Avatar2
{
    public partial class CAPI
    {
        #region Input
        ///
        /// Flags that indicate which controller buttons are pressed.
        /// @see ovrAvatar2Touch
        ///
        [Flags]
        public enum ovrAvatar2Button : Int32
        {
            /// X/A pressed
            One = 0x0001,

            /// Y/B pressed
            Two = 0x0002,

            /// Select/Oculus button pressed
            Three = 0x0004,

            /// Joystick button pressed
            Joystick = 0x0008,
        }

        ///
        /// Flags that indicate which parts of the controller are touched.
        /// @see ovrAvatar2Touch
        ///
        [Flags]
        public enum ovrAvatar2Touch : Int32
        {
            /// Capacitive touch for X/A button
            One = 0x0001,

            /// Capacitive touch for Y/B button
            Two = 0x0002,

            /// Capacitive touch for thumbstick
            Joystick = 0x0004,

            /// Capacitive touch for thumb rest
            ThumbRest = 0x0008,

            /// Capacitive touch for index trigger
            Index = 0x0010,

            /// Index finger is pointing
            Pointing = 0x0040,

            /// Thumb is up
            ThumbUp = 0x0080,
        }

        ///
        /// Designates the type of controller being used.
        ///
        public enum ovrAvatar2ControllerType : Int32
        {
            /// Invalid or unknown controller
            Invalid = -1,

            ///  Oculus Rift controller
            Rift = 0,

            /// Oculus Touch controller
            Touch = 1,

            /// Oculus Quest 2 controller
            Quest2 = 2,

            /// Meta Quest Pro controller
            QuestPro = 3,
        }

        ///
        /// Describes native controller state.
        ///
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2ControllerState
        {
            ///
            /// Flags specifying which buttons are currently pressed.
            /// @see ovrAvatar2Button
            ///
            public ovrAvatar2Button buttonMask;

            ///
            /// Flags mask specifying which portions of the controller are currently touched.
            /// @see ovrAvatar2Touch
            ///
            public ovrAvatar2Touch touchMask;

            /// X-axis position of the thumbstick.
            public float joystickX;

            /// Y-axis position of the thumbstick.
            public float joystickY;

            /// Current value of the index finger trigger.
            public float indexTrigger;

            /// Current value of the hand trigger (underneath the middle finger).
            public float handTrigger;
        }

        ///
        /// Collects the input state for both left and right controllers.
        /// @see ovrAvatar2ControllerState
        /// @see ovrAvatar2ControllerType
        ///
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2InputControlState
        {
            /// Type of controller being used.
            public ovrAvatar2ControllerType type;

            // the next two entries match 'ovrAvatar2ControllerState controllerState[ovrAvatar2Side_Count]'
            // in the c++ implementation (can't use 'fixed' for ovrAvatar2ControllerState)

            /// Input state of the left controller.
            public ovrAvatar2ControllerState leftControllerState;

            /// Input state of the right controller.
            public ovrAvatar2ControllerState rightControllerState;
        }

        ///
        /// Collects the current position, orientation and scale for
        /// the headset and controllers.
        /// @see ovrAvatar2InputControlState
        ///
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2InputTrackingState
        {
            // True if the headset is currently tracked and valid.
            [MarshalAs(UnmanagedType.U1)]
            public bool headsetActive;

            /// True if the left controller is currently tracked and valid.
            [MarshalAs(UnmanagedType.U1)]
            public bool leftControllerActive;

            /// True if the right controller is currently tracked and valid.
            [MarshalAs(UnmanagedType.U1)]
            public bool rightControllerActive;

            ///
            /// True if the controller model should be shown.
            /// Must also have *ovrAvatar2EntityFeatures.ShowControllers* set on the entity.
            /// @see ovrAvatar2EntityFeatures
            /// @see OvrAvatarEntity.CreateEntity
            /// @see ovrAvatar2EntityCreateInfo
            ///
            [MarshalAs(UnmanagedType.U1)]
            public bool leftControllerVisible;

            ///
            /// True if the controller model should be shown.
            /// Must also have *ovrAvatar2EntityFeatures.ShowControllers* set on the entity.
            /// @see ovrAvatar2EntityFeatures
            /// @see OvrAvatarEntity.CreateEntity
            /// @see ovrAvatar2EntityCreateInfo
            ///
            [MarshalAs(UnmanagedType.U1)]
            public bool rightControllerVisible;

            ///
            public ovrAvatar2Transform headset;

            // the next two entries match 'ovrAvatar2Transform controller[ovrAvatar2Side_Count]'
            // in the c++ implementation (can't use 'fixed' for ovrAvatar2Transform)

            /// Transform with position, orientation and scale for the left controller.
            public ovrAvatar2Transform leftController;

            /// Transform with position, orientation and scale for the right controller.
            public ovrAvatar2Transform rightController;
        }
        #endregion

        #region Tracking

        ///
        /// Estimate of the user's overall body pose
        /// (sitting or standing).
        ///
        public enum ovrAvatar2TrackingBodyModality : Int32
        {
            /// Avatar modality unknown.
            Unknown = 0,

            // TODO: verify this is correct
            ///  User is in a seated position.
            Sitting = 1,

            // TODO: verify this is correct
            ///  User is in a standing position.
            Standing = 2,
        };

        // TODO: more explanation of what tracking confidence level means
        ///
        /// Tracking confidence level.
        ///
        public enum ovrAvatar2TrackingConfidence : Int32
        {
            // Low tracking confidence level
            Low = 0,

            // High tracking confidence level
            High = 0x3f800000,
        };

        ///
        /// Indicates the coordinate space of a joint / bone.
        ///
        public enum ovrAvatar2Space : Int32
        {
            /// Local coordinates, with respect to parent joint
            Local = 0,

            /// Object coordinates, with respect to the avatar entity
            Object = 1,

            /// Coordinate space not known
            Unknown = 2,
        }

        ///
        /// Contains bone ID for this bone and the index of it's parent bone.
        /// An array of these defines the structure of a skeleton.
        /// The name and order of the tracking skeleton bones are fixed but
        /// the hierarchy is not.
        ///
        /// @see ovrAvatar2JointType
        ///
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2Bone
        {
            /// Bone ID for this bone.
            public ovrAvatar2JointType boneId;

            /// Zero-based index of this bone's parent bone.
            /// It will be -1 if the bone has no parent.
            public Int16 parentBoneIndex;
        };

        ///
        /// Contains the transforms for the bones in the tracking skeleton.
        ///
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe ref struct ovrAvatar2TrackingBodyPose
        {
            /// Number of bones in the tracking skeleton.
            public readonly UInt32 numBones;

            // TODO: Make this field readonly
            /// Coordinate space of the pose.
            public ovrAvatar2Space space;

            /// Position, orientation and scale for each bone in the skeleton.
            public readonly ovrAvatar2Transform* bones;

            /// Scale value of left hand.
            public readonly float leftHandScale;

            /// Scale value of right hand.
            public readonly float rightHandScale;

            public ovrAvatar2TrackingBodyPose(ovrAvatar2Transform* bones, UInt32 numBones, ovrAvatar2Space space = ovrAvatar2Space.Unknown, float leftHandScale = 1.0f, float rightHandScale = 1.0f)
            {
                this.space = space;
                this.numBones = numBones;
                this.bones = bones;
                this.leftHandScale = leftHandScale;
                this.rightHandScale = rightHandScale;
            }
        };

        /**
         * @struct ovrAvatar2TrackingBodySkeleton
         * Contains the description of the body tracking skeleton.
         */
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe ref struct ovrAvatar2TrackingBodySkeleton
        {
            /// Number of bones in the bones array.
            public readonly UInt32 numBones;

            /// Vector with the forward bone direction.
            public ovrAvatar2Vector3f forwardDir;

            /// Array of bone IDs and their parent bones.
            /// Describes the structure of the skeleton.
            public readonly ovrAvatar2Bone* bones;

            /// Reference pose of the skeleton. This is the pose
            /// which places the skeleton in a T with arms horizontal.
            ///
            public ovrAvatar2TrackingBodyPose referencePose;

            ///
            /// Constructs a native body tracking skeleton from a bone hierarchy and initial pose.
            /// @param bones     Array of bone IDs and parent bone indices.
            /// @param numBones  Number of bones in the array.
            /// @param pose      Native pose description.
            /// @see ovrAvatar2Bone
            /// @see ovrAvatar2TrackingBodyPose
            ///
            public ovrAvatar2TrackingBodySkeleton(ovrAvatar2Bone* bones, UInt32 numBones, ovrAvatar2TrackingBodyPose pose)
            {
                this.bones = bones;
                this.numBones = numBones;
                forwardDir = new ovrAvatar2Vector3f();
                referencePose = pose;
            }
        }

        ///
        /// Type of input used for hand tracking.
        ///
        public enum ovrAvatar2HandInputType : Int32
        {
            /// Controller used to get hand position and orientation
            Controller = 0,

            /// Headset sensors are used to get hand position and orientation
            Tracking = 1,

            /// Custom hand tracking implementation
            Custom = 2,

            /// Hand tracking input type unknown
            Unknown = 3,
        }

        ///
        /// Collects the state of the body tracker.
        /// The tracking state includes the position, orientation and scale
        /// for the headset and controllers, buttons pressed, the type of
        /// Hand tracking desired and whether the avatar is sitting or standing.
        /// @see ovrAvatar2InputControlState
        /// @see ovrAvatar2HandInputType
        /// @see ovrAvatar2TrackingBodyModality
        ///
        [StructLayout(LayoutKind.Sequential)]
        public ref struct ovrAvatar2TrackingBodyState
        {
            /// Position, orientation and scale of headset and left and right controllers.
            public ovrAvatar2InputTrackingState inputTrackingState;

            /// Input state for left and right controllers.
            public ovrAvatar2InputControlState inputControlState;

            /// Type of hand tracking input for left hand.
            public ovrAvatar2HandInputType leftHandInputType;

            /// Type of hand tracking input for right hand.
            public ovrAvatar2HandInputType rightHandInputType;

            /// Version number of tracking skeleton.
            public Int32 skeletonVersion;

            /// Number of bones in tracking skeleton.
            public Int32 numBones;

            /// Avatar body modality.
            public ovrAvatar2TrackingBodyModality bodyModality;

            /// Scale value of left hand.
            public float leftHandScale;

            /// Scale value of right hand.
            public float rightHandScale;
        }

        ///
        /// C# callback function invoked from the native code to update the body tracking state.
        /// @param bodyState   C# object to get the updated tracking state.
        /// @param userContext handle to native object originating the call.
        /// @see ovrAvatar2TrackingBodyState
        ///
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal delegate bool BodyStateCallback(out ovrAvatar2TrackingBodyState bodyState, IntPtr userContext);

        ///
        /// C# callback function invoked from the native code to update the body tracking skeleton.
        /// @param skeleton    C# object to get the updated skeleton.
        /// @param userContext C++ pointer to native object originating the call.
        /// @see ovrAvatar2TrackingBodySkeleton
        ///
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal delegate bool BodySkeletonCallback(ref ovrAvatar2TrackingBodySkeleton skeleton, IntPtr userContext);

        ///
        /// C# callback function invoked from the native code to update the body tracking pose.
        /// @param pose        C# object to get the updated pose.
        /// @param userContext handle to native object originating the call.
        /// @see ovrAvatar2TrackingBodyPose
        ///
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal delegate bool BodyPoseCallback(ref ovrAvatar2TrackingBodyPose pose, IntPtr userContext);

        ///
        /// Collects the C# callbacks and the pointer to the native body tracking implementation.
        /// This is the *context* passed to @ref BodyPoseCallback(),
        /// @ref BodySkeletonCallback() and @ref BodyStateCallback().
        ///
        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2TrackingDataContext
        {
            /// Native handle to the body tracking implementation.
            public IntPtr context;

            /// C# function called by native code to update body tracking state.
            public BodyStateCallback bodyStateCallback;

            /// C# function called by native code to update body tracking skeleton.
            public BodySkeletonCallback bodySkeletonCallback;

            /// C# function called by native code to update body tracking pose.
            public BodyPoseCallback bodyPoseCallback;
        }

        ///
        /// Collects the native handles to the C# callbacks and the
        /// body tracking implementation.
        ///
        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2TrackingDataContextNative
        {
            /// Native handle to the native body tracking implementation.
            public IntPtr context;

            ///
            /// Native handle to the body state callback.
            /// @see BodyStateCallback
            ///
            public IntPtr bodyStateCallback;

            ///
            /// Native handle to the body skeleton callback.
            /// @see BodySkeletonCallback
            ///
            public IntPtr bodySkeletonCallback;

            ///
            /// Native handle to the body pose callback.
            /// @see BodyPoseCallback
            ///
            public IntPtr bodyPoseCallback;
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Tracking_SetBodyTrackingContext(ovrAvatar2EntityId entityId, in ovrAvatar2TrackingDataContext context);


        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrAvatar2Tracking_SetBodyTrackingContext")]
        internal static extern ovrAvatar2Result ovrAvatar2Tracking_SetBodyTrackingContextNative(ovrAvatar2EntityId entityId, in ovrAvatar2TrackingDataContextNative context);

        #endregion Tracking


        #region LipSync

        ///
        /// Avatar visemes used for synchronizing avatar lip motion with audio speech.
        ///
        public enum ovrAvatar2Viseme : Int32
        {
            /// Silent viseme
            sil = 0,
            /// PP viseme (corresponds to p,b,m phonemes in worlds like \a put , \a bat, \a mat)
            PP = 1,
            /// FF viseme (corrseponds to f,v phonemes in the worlds like \a fat, \a vat)
            FF = 2,
            /// TH viseme (corresponds to th phoneme in words like \a think, \a that)
            TH = 3,
            /// DD viseme (corresponds to t,d phonemes in words like \a tip or \a doll)
            DD = 4,
            /// kk viseme (corresponds to k,g phonemes in words like \a call or \a gas)
            kk = 5,
            /// CH viseme (corresponds to tS,dZ,S phonemes in words like \a chair, \a join, \a she)
            CH = 6,
            /// SS viseme (corresponds to s,z phonemes in words like \a sir or \a zeal)
            SS = 7,
            /// nn viseme (corresponds to n,l phonemes in worlds like \a lot or \a not)
            nn = 8,
            /// RR viseme (corresponds to r phoneme in worlds like \a red)
            RR = 9,
            /// aa viseme (corresponds to A: phoneme in worlds like \a car)
            aa = 10,
            /// E viseme (corresponds to e phoneme in worlds like \a bed)
            E = 11,
            /// I viseme (corresponds to ih phoneme in worlds like \a tip)
            ih = 12,
            /// O viseme (corresponds to oh phoneme in worlds like \a toe)
            oh = 13,
            /// U viseme (corresponds to ou phoneme in worlds like \a book)
            ou = 14,

            /// Total number of visemes
            Count = 15
        }

        // TODO: reference lipsync from SDK
        ///
        /// Collects the viseme weights from the lip tracker.
        ///
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct ovrAvatar2LipSyncState
        {
            /// Array of weights for each viseme.
            public fixed float visemes[(int)ovrAvatar2Viseme.Count];

            // TODO: figure out what this is
            public float laughterScore;
        }

        ///
        /// C# callback function invoked from the native code to update
        /// the lip sync viseme weights.
        /// @param lipSyncState    C# object to get the updated viseme weights.
        /// @param userContext     handle to native object originating the call.
        /// @see ovrAvatar2LipSyncState
        ////
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate bool LipSyncCallback(out ovrAvatar2LipSyncState lipSyncState, IntPtr userContext);

        ///
        /// Collects the C# callback and the pointer to the
        /// native lip sync implementation.
        /// This is the *context* passed to @ref LipSyncCallback().
        ///
        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2LipSyncContext
        {
            /// handle to the native body tracking implementation.
            public IntPtr context;

            /// C# function to called by native code to update lip sync viseme weights.
            public LipSyncCallback lipSyncCallback;
        }

        ///
        /// Collects native handle for the C# callback and the pointer to the
        /// native lip sync implementation.
        /// This is the *context* passed to @ref LipSyncCallback().
        ///
        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2LipSyncContextNative
        {
            /// handle to the native lip sync implementation.
            public IntPtr context;

            /// Native handle to the lip sync callback function.
            public IntPtr lipSyncCallback;
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Tracking_SetLipSyncContext(ovrAvatar2EntityId entityId, in ovrAvatar2LipSyncContext context);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrAvatar2Tracking_SetLipSyncContext")]
        internal static extern ovrAvatar2Result ovrAvatar2Tracking_SetLipSyncContextNative(ovrAvatar2EntityId entityId, in ovrAvatar2LipSyncContextNative context);


        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Tracking_GetVisemes(ovrAvatar2EntityId entityId, Int32 numVisemeValues, IntPtr visemeValues);


        //-----------------------------------------------------------------
        //
        // Pose
        //
        //

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Tracking_GetPose(ovrAvatar2EntityId entityId, out ovrAvatar2Pose outPose);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Tracking_GetPoseValid(ovrAvatar2EntityId entityId, [MarshalAs(UnmanagedType.U1)] out bool isValid);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern unsafe ovrAvatar2Result ovrAvatar2Tracking_GetNameAtIndex(
        ovrAvatar2EntityId entityId, int index, byte* nameBuffer, UInt32 bufferSize);
        #endregion LipSync


        #region Face
        //-----------------------------------------------------------------
        //
        // Face
        //
        //

        public enum ovrAvatar2FaceExpression : Int32
        {
            BrowLowererL = 0,
            BrowLowererR = 1,

            CheekPuffL = 2,
            CheekPuffR = 3,
            CheekRaiserL = 4,
            CheekRaiserR = 5,
            CheekSuckL = 6,
            CheekSuckR = 7,

            ChinRaiserL = 8,
            ChinRaiserR = 9,

            DimplerL = 10,
            DimplerR = 11,

            EyesClosedL = 12,
            EyesClosedR = 13,
            EyesLookDownL = 14,
            EyesLookDownR = 15,
            EyesLookLeftL = 16,
            EyesLookLeftR = 17,
            EyesLookRightL = 18,
            EyesLookRightR = 19,
            EyesLookUpL = 20,
            EyesLookUpR = 21,

            InnerBrowRaiserL = 22,
            InnerBrowRaiserR = 23,

            JawDrop = 24,
            JawSidewaysLeft = 25,
            JawSidewaysRight = 26,
            JawThrust = 27,

            LidTightenerL = 28,
            LidTightenerR = 29,

            LipCornerDepressorL = 30,
            LipCornerDepressorR = 31,
            LipCornerPullerL = 32,
            LipCornerPullerR = 33,
            LipFunnelerLB = 34,
            LipFunnelerLT = 35,
            LipFunnelerRB = 36,
            LipFunnelerRT = 37,
            LipPressorL = 38,
            LipPressorR = 39,
            LipPuckerL = 40,
            LipPuckerR = 41,
            LipStretcherL = 42,
            LipStretcherR = 43,
            LipSuckLB = 44,
            LipSuckLT = 45,
            LipSuckRB = 46,
            LipSuckRT = 47,
            LipTightenerL = 48,
            LipTightenerR = 49,
            LipsTowardLB = 50,
            LipsTowardLT = 51,
            LipsTowardRB = 52,
            LipsTowardRT = 53,
            LowerLipDepressorL = 54,
            LowerLipDepressorR = 55,

            MouthLeft = 56,
            MouthRight = 57,

            NasiolabialFurrowL = 58,
            NasiolabialFurrowR = 59,

            NoseWrinklerL = 60,
            NoseWrinklerR = 61,
            NostrilCompressorL = 62,
            NostrilCompressorR = 63,
            NostrilDilatorL = 64,
            NostrilDilatorR = 65,

            OuterBrowRaiserL = 66,
            OuterBrowRaiserR = 67,

            UpperLidRaiserL = 68,
            UpperLidRaiserR = 69,
            UpperLipRaiserL = 70,
            UpperLipRaiserR = 71,

            Count = 72
        }

        // TODO: Investigate why fixed float array marshalling is only failing on Mac M1. Implementation is swithed over to dynamic float[] to work around the issue, but using fixed array has
        // the benefit of fixing array size to ensure tight coupling btn managed and unmanaged code. Fixed array in a struct is also pre-initialized without the need for explicit array initialization.
        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2FacePose
        {
            // expressionWeights and expressionConfidence are expected to have size equal to ovrAvatar2FaceExpression.Count
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)ovrAvatar2FaceExpression.Count)]
            public float[] expressionWeights;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)ovrAvatar2FaceExpression.Count)]
            public float[] expressionConfidence;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal delegate bool FacePoseCallback(out ovrAvatar2FacePose facePose, IntPtr userContext);

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2FacePoseProvider
        {
            public IntPtr provider;
            public FacePoseCallback facePoseCallback;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2FacePoseProviderNative
        {
            public IntPtr provider;
            public IntPtr facePoseCallback;
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Input_SetFacePoseProvider(ovrAvatar2EntityId entityId
            , in ovrAvatar2FacePoseProvider provider);

        [DllImport(
            LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrAvatar2Input_SetFacePoseProvider")]
        internal static extern ovrAvatar2Result ovrAvatar2Input_SetFacePoseProviderNative(ovrAvatar2EntityId entityId
            , in ovrAvatar2FacePoseProviderNative provider);
        #endregion Face

        #region Eye
        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2EyePose
        {
            public ovrAvatar2Quatf orientation;
            public ovrAvatar2Vector3f position;
            [MarshalAs(UnmanagedType.U1)]
            public bool isValid;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2EyesPose
        {
            public ovrAvatar2EyePose leftEye;
            public ovrAvatar2EyePose rightEye;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal delegate bool EyePoseCallback(out ovrAvatar2EyesPose eyePose, IntPtr userContext);

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2EyePoseProvider
        {
            public IntPtr provider;
            public EyePoseCallback eyePoseCallback;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ovrAvatar2EyePoseProviderNative
        {
            public IntPtr provider;
            public IntPtr eyePoseCallback;
        }

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2Input_SetEyePoseProvider(ovrAvatar2EntityId entityId
            , in ovrAvatar2EyePoseProvider context);

        [DllImport(
            LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrAvatar2Input_SetEyePoseProvider")]
        internal static extern ovrAvatar2Result ovrAvatar2Input_SetEyePoseProviderNative(ovrAvatar2EntityId entityId
            , in ovrAvatar2EyePoseProviderNative context);
        #endregion Eye
    }
}
