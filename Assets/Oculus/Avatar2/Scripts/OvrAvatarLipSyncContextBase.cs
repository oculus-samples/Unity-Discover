using AOT;
using System;

/// @file OvrAvatarLipSyncContextBase.cs

namespace Oculus.Avatar2
{
    /**
     * Base class for C# code to drive lipsync data for avatar entites.
     * @see OvrAvatarEntity.SetLipSync
     */
    public abstract class OvrAvatarLipSyncContextBase : OvrAvatarCallbackContextBase
    {
        // Cache the managed representation to reduce GC allocations
        private readonly OvrAvatarLipSyncState _lipsyncState = new OvrAvatarLipSyncState();
        internal CAPI.ovrAvatar2LipSyncContext DataContext { get; }

        protected OvrAvatarLipSyncContextBase()
        {
            var dataContext = new CAPI.ovrAvatar2LipSyncContext();
            dataContext.context = new IntPtr(id);
            dataContext.lipSyncCallback = LipSyncCallback;

            DataContext = dataContext;
        }

        /**
         * Gets the lip sync state from the native lipsync implementation.
         * The lip sync state contains the weights for the visemes to
         * make the lip expression.
         * Lip sync implementations must override this function to
         * convert the native lip sync state into a form usable by Unity.
         * @param lipsyncState  where to store the generated viseme weights.
         * @see OvrAvatarLipSyncState
         */
        protected abstract bool GetLipSyncState(OvrAvatarLipSyncState lipsyncState);

        [MonoPInvokeCallback(typeof(CAPI.LipSyncCallback))]
        private static bool LipSyncCallback(out CAPI.ovrAvatar2LipSyncState lipsyncState, IntPtr userContext)
        {
            try
            {
                var context = GetInstance<OvrAvatarLipSyncContextBase>(userContext);
                if (context != null)
                {
                    if (context.GetLipSyncState(context._lipsyncState))
                    {
                        lipsyncState = context._lipsyncState.ToNative();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError(e.ToString());
            }

            lipsyncState = new CAPI.ovrAvatar2LipSyncState();
            return false;
        }

        public OvrAvatarLipSyncState DebugQueryLipSyncState()
        {
            if (GetLipSyncState(_lipsyncState))
            {
                return _lipsyncState;
            }
            return null;
        }
    }
}
