using Oculus.Avatar2;
using UnityEngine;

namespace Oculus.Avatar2
{
    /// <summary>
    /// MonoBehavior which holds a face tracking context so it can be referenced in the inspector
    /// </summary>
    public abstract class OvrAvatarFacePoseBehavior : MonoBehaviour
    {
        public abstract OvrAvatarFacePoseProviderBase FacePoseProvider { get; }
    }
}
