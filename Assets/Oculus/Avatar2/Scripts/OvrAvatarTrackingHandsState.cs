/**
 * @file OvrAvatarTrackingHandsState.cs
 */

namespace Oculus.Avatar2
{
    /**
     * Maintains wrist positions and hand bone rotations and
     * converts to and from C# and C++ native versions.
     * @see OvrAvatarBodyTrackingContextBase
     */
    public sealed class OvrAvatarTrackingHandsState
    {
        /// Transform for the left wrist.
        public CAPI.ovrAvatar2Transform wristPosLeft;

        /// Transform for the right wrist.
        public CAPI.ovrAvatar2Transform wristPosRight;

        /// Array of rotations for the hand bones.
        public readonly CAPI.ovrAvatar2Quatf[] boneRotations = new CAPI.ovrAvatar2Quatf[(int)CAPI.MaxHandBones];

        /// Tracked uniform scale for the left hand.
        public float handScaleLeft;

        /// Tracked uniform scale for the right hand.
        public float handScaleRight;

        /// True if the left hand is being tracked.
        public bool isTrackedLeft;

        /// True if the right hand is being tracked.
        public bool isTrackedRight;

        /// True if the tracking confidence is high for the left hand.
        public bool isConfidentLeft;

        /// True if the tracking confidence is high for the right hand.
        public bool isConfidentRight;

        /**
         * Creates a native C++ hand pose from this C# pose.
         * @see CAPI.ovrAvatar2HandTrackingState
         * @see FromNative
         */
        internal CAPI.ovrAvatar2HandTrackingState ToNative()
        {
            return new CAPI.ovrAvatar2HandTrackingState
            {
                wristPosLeft = wristPosLeft,
                wristPosRight = wristPosRight,
                handScaleLeft = handScaleLeft,
                handScaleRight = handScaleRight,
                isTrackedLeft = isTrackedLeft,
                isTrackedRight = isTrackedRight,
                isConfidentLeft = isConfidentLeft,
                isConfidentRight = isConfidentRight,
                boneRotation0 = boneRotations[0],
                boneRotation1 = boneRotations[1],
                boneRotation2 = boneRotations[2],
                boneRotation3 = boneRotations[3],
                boneRotation4 = boneRotations[4],
                boneRotation5 = boneRotations[5],
                boneRotation6 = boneRotations[6],
                boneRotation7 = boneRotations[7],
                boneRotation8 = boneRotations[8],
                boneRotation9 = boneRotations[9],
                boneRotation10 = boneRotations[10],
                boneRotation11 = boneRotations[11],
                boneRotation12 = boneRotations[12],
                boneRotation13 = boneRotations[13],
                boneRotation14 = boneRotations[14],
                boneRotation15 = boneRotations[15],
                boneRotation16 = boneRotations[16],
                boneRotation17 = boneRotations[17],
                boneRotation18 = boneRotations[18],
                boneRotation19 = boneRotations[19],
                boneRotation20 = boneRotations[20],
                boneRotation21 = boneRotations[21],
                boneRotation22 = boneRotations[22],
                boneRotation23 = boneRotations[23],
                boneRotation24 = boneRotations[24],
                boneRotation25 = boneRotations[25],
                boneRotation26 = boneRotations[26],
                boneRotation27 = boneRotations[27],
                boneRotation28 = boneRotations[28],
                boneRotation29 = boneRotations[29],
                boneRotation30 = boneRotations[30],
                boneRotation31 = boneRotations[31],
                boneRotation32 = boneRotations[32],
                boneRotation33 = boneRotations[33],
            };
        }

        /**
         * Copies the native hand pose provided to this C# pose.
         * @see CAPI.ovrAvatar2HandTrackingState
         * @see ToNative
         */
        internal void FromNative(ref CAPI.ovrAvatar2HandTrackingState native)
        {
            wristPosLeft = native.wristPosLeft;
            wristPosRight = native.wristPosRight;
            handScaleLeft = native.handScaleLeft;
            handScaleRight = native.handScaleRight;
            isTrackedLeft = native.isTrackedLeft;
            isTrackedRight = native.isTrackedRight;
            isConfidentLeft = native.isConfidentLeft;
            isConfidentRight = native.isConfidentRight;

            boneRotations[0] = native.boneRotation0;
            boneRotations[1] = native.boneRotation1;
            boneRotations[2] = native.boneRotation2;
            boneRotations[3] = native.boneRotation3;
            boneRotations[4] = native.boneRotation4;
            boneRotations[5] = native.boneRotation5;
            boneRotations[6] = native.boneRotation6;
            boneRotations[7] = native.boneRotation7;
            boneRotations[8] = native.boneRotation8;
            boneRotations[9] = native.boneRotation9;
            boneRotations[10] = native.boneRotation10;
            boneRotations[11] = native.boneRotation11;
            boneRotations[12] = native.boneRotation12;
            boneRotations[13] = native.boneRotation13;
            boneRotations[14] = native.boneRotation14;
            boneRotations[15] = native.boneRotation15;
            boneRotations[16] = native.boneRotation16;
            boneRotations[17] = native.boneRotation17;
            boneRotations[18] = native.boneRotation18;
            boneRotations[19] = native.boneRotation19;
            boneRotations[20] = native.boneRotation20;
            boneRotations[21] = native.boneRotation21;
            boneRotations[22] = native.boneRotation22;
            boneRotations[23] = native.boneRotation23;
            boneRotations[24] = native.boneRotation24;
            boneRotations[25] = native.boneRotation25;
            boneRotations[26] = native.boneRotation26;
            boneRotations[27] = native.boneRotation27;
            boneRotations[28] = native.boneRotation28;
            boneRotations[29] = native.boneRotation29;
            boneRotations[30] = native.boneRotation30;
            boneRotations[31] = native.boneRotation31;
            boneRotations[32] = native.boneRotation32;
            boneRotations[33] = native.boneRotation33;
        }
    }
}
