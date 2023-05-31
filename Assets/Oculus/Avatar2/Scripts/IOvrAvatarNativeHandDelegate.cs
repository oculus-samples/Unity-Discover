namespace Oculus.Avatar2
{
    /// <summary>
    /// An implementation of hand tracking can implement this interface to reduce the marshaling overhead.
    /// </summary>
    internal interface IOvrAvatarNativeHandDelegate
    {
        CAPI.ovrAvatar2HandTrackingDataContextNative NativeContext { get; }
    }
}
