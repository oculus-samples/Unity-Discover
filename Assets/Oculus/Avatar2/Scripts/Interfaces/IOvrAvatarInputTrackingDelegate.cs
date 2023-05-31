namespace Oculus.Avatar2
{
    /// <summary>
    /// Interface which allows the OvrAvatarBodyTrackingContext to read input tracking data from clients.
    /// </summary>
    public interface IOvrAvatarInputTrackingDelegate
    {
        bool GetInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState);
    }
}
