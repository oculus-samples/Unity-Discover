namespace Oculus.Avatar2
{
    /// <summary>
    /// An implementation of eye pose can implement this interface to reduce the marshaling overhead.
    /// </summary>
    internal interface IOvrAvatarNativeEyePose
    {
        CAPI.ovrAvatar2EyePoseProviderNative NativeProvider { get; }
    }
}
