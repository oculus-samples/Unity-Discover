using Oculus.Avatar2;
using UnityEngine;

namespace Oculus.Avatar2
{
    /// <summary>
    /// MonoBehavior which holds a eye pose context so it can be referenced in the inspector
    /// </summary>
    public abstract class OvrAvatarEyePoseBehavior : MonoBehaviour
    {
        public abstract OvrAvatarEyePoseProviderBase EyePoseProvider { get; }
    }
}
