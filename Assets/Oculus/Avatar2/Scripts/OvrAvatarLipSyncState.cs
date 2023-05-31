/// @file OvrAvatarLipSyncState.cs

namespace Oculus.Avatar2
{
    /**
     * Collects the viseme weights for avatar lip motion and
     * converts to and from C# and C++ native versions.
     * @see ovrAvatar2Viseme
     */
    public sealed class OvrAvatarLipSyncState
    {
        /**
         * How hard the avatar is laughing???
         */
        public float laughterScore;

        /**
         * Array of viseme weights.
         * @see CAPI.ovrAvatar2Viseme
         */
        public readonly float[] visemes = new float[(int)CAPI.ovrAvatar2Viseme.Count];

        #region Native Conversions
        /**
         * Creates a new native lip tracking state from this C# instance.
         * @see CAPI.ovrAvatar2LipSyncState
         * @see FromNative
         */
        internal CAPI.ovrAvatar2LipSyncState ToNative()
        {
            var native = new CAPI.ovrAvatar2LipSyncState
            {
                laughterScore = laughterScore,
            };
            unsafe
            {
                for (var i = 0; i < visemes.Length; i++)
                {
                    native.visemes[i] = visemes[i];
                }
            }

            return native;
        }

        /**
         * Copies the given native lip tracking state to this C# instance.
         * @see CAPI.ovrAvatar2LipSyncState
         * @see ToNative
         */
        internal void FromNative(ref CAPI.ovrAvatar2LipSyncState native)
        {
            unsafe
            {
                for (var i = 0; i < visemes.Length; i++)
                {
                    visemes[i] = native.visemes[i];
                }
            }

            laughterScore = native.laughterScore;
        }
        #endregion
    }
}
