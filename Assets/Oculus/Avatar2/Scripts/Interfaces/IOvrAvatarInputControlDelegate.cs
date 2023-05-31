namespace Oculus.Avatar2
{
    /// <summary>
    /// Interface which allows the OvrAvatarBodyTrackingContext to read input control data from clients.
    /// </summary>
    public interface IOvrAvatarInputControlDelegate
    {
        bool GetInputControlState(out OvrAvatarInputControlState inputControlState);
    }
}
