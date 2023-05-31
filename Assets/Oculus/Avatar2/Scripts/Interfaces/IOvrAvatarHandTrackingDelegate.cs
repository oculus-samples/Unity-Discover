namespace Oculus.Avatar2
{
    /// <summary>
    /// Interface which allows the OvrAvatarBodyTrackingContext to read hand tracking data from clients.
    /// </summary>
    public interface IOvrAvatarHandTrackingDelegate
    {
        bool GetHandData(OvrAvatarTrackingHandsState handData);
    }
}
