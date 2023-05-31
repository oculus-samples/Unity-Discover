using UnityEngine;
/// @file OvrAvatarLipSyncBehavior.cs

namespace Oculus.Avatar2
{
    /**
     * MonoBehavior which holds a lip sync context so it can be referenced in the inspector.
     * @see OvrAvatarLipSyncContextBase
     */
    public abstract class OvrAvatarLipSyncBehavior : MonoBehaviour
    {
        /**
         * Get the lip sync implementation.
         * Subclasses must implement a getter for this property.
         * @see OvrAvatarLipSyncContextBase
         */
        public abstract OvrAvatarLipSyncContextBase LipSyncContext { get; }
    }
}
