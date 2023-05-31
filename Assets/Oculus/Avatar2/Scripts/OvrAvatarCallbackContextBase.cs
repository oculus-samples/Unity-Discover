using System;
using System.Collections.Generic;

namespace Oculus.Avatar2
{
    /**
     * Base class for C# code which is called from the native libovravatar2 lib.
     * This class maintains a map between the native handle (a pointer) and the
     * C# instances so the C# instance can be found given the native handle.
     */
    public abstract class OvrAvatarCallbackContextBase : IDisposable
    {
        private static Dictionary<int, OvrAvatarCallbackContextBase> instanceMap_ =
            new Dictionary<int, OvrAvatarCallbackContextBase>();

        private static Int32 nextId_;

        protected readonly Int32 id;


        protected OvrAvatarCallbackContextBase()
        {
            id = nextId_++;
            instanceMap_.Add(id, this);
        }

        #region Static Methods

        /**
         * Gets the C# object given the native handle.
         */
        protected static T GetInstance<T>(IntPtr handle) where T : OvrAvatarCallbackContextBase
        {
            if (instanceMap_.TryGetValue(handle.ToInt32(), out var context))
            {
                return (T)context;
            }

            return null;
        }

        internal static void DisposeAll()
        {
            var map = instanceMap_;
            instanceMap_ = new Dictionary<int, OvrAvatarCallbackContextBase>();
            foreach (var kvp in map)
            {
                kvp.Value.Dispose();
            }
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                instanceMap_.Remove(id);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~OvrAvatarCallbackContextBase()
        {
            Dispose(false);
        }
        #endregion
    }
}
