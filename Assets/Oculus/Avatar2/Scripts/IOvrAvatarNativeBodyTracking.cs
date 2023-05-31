namespace Oculus.Avatar2
{
    /// <summary>
    /// An implementation of body tracking can implement this interface to reduce the marshaling overhead.
    /// </summary>
    internal interface IOvrAvatarNativeBodyTracking
    {
        CAPI.ovrAvatar2TrackingDataContextNative NativeDataContext { get; }
    }
}
