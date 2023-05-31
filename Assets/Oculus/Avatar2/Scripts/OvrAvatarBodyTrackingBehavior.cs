using UnityEngine;

/**
 * @file OvrAvatarBodyTrackingBehavior.cs
 */

namespace Oculus.Avatar2
{
    /**
     * MonoBehavior which holds a body tracking context so it can be referenced in the inspector.
     * @see OvrAvatarBodyTrackingContextBase
     */
    public abstract class OvrAvatarBodyTrackingBehavior : MonoBehaviour
    {
        /**
         * Get the body tracking implementation.
         * Subclasses must implement a getter for this property.
         * @see OvrAvatarBodyTrackingContextBase
         */
        public abstract OvrAvatarBodyTrackingContextBase TrackingContext { get; }
    }
}
