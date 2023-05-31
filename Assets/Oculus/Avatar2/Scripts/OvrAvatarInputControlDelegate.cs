namespace Oculus.Avatar2
{
    /**
     * Base class for setting input controls on an avatar entity.
     */
    public abstract class OvrAvatarInputControlDelegate : IOvrAvatarInputControlDelegate
    {
        public abstract bool GetInputControlState(out OvrAvatarInputControlState inputControlState);

        /**
         * Gets the controller type.
         * @returns which type of controller being used (Rift, Touch, etc._)
         * @see CAPI.ovrAvatar2ControllerType
         */
        protected virtual CAPI.ovrAvatar2ControllerType GetControllerType()
        {
            return OvrAvatarManager.Instance.ControllerType;
        }
    }
}
