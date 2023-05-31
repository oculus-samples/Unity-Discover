namespace Oculus.Avatar2
{
    /// <summary>
    /// An implementation of face tracking can implement this interface to reduce the marshaling overhead.
    /// </summary>
    internal interface IOvrAvatarNativeFacePose
    {
        CAPI.ovrAvatar2FacePoseProviderNative NativeProvider { get; }
    }
}
