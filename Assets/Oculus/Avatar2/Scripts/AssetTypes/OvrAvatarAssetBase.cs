using System;
using System.Collections;

/// @file OvrAvatarAssetBase.cs

namespace Oculus.Avatar2
{
    /**
     * Parent class for loading avatar assets.
     * @see OvrAvatarImage
     * @see OvrAvatarPrimitive
     */
    public abstract class OvrAvatarAssetBase : IDisposable
    {
        /// Unique global asset ID.
        public readonly CAPI.ovrAvatar2Id assetId;

        /// Asset type.
        public abstract string typeName { get; }

        /// Asset name.
        public abstract string assetName { get; }

        /// True if asset has finished loading, else false.
        public virtual bool isLoaded { get; protected set; } = false;

        /// True if asset loading was cancelled, else false.
        public bool isCancelled { get; protected set; } = false;

        /**
         * Constructs and initializes an avatar asset.
         * @param assetId   ID to assign to this asset.
         */
        protected OvrAvatarAssetBase(CAPI.ovrAvatar2Id assetId)
        {
            this.assetId = assetId;
            if (OvrAvatarManager.initialized)
            {
                OvrAvatarManager.AddAsset(this);
            }
        }

        // If disposing == true, safe to dispose managed resources. Otherwise only unmanaged resources should be disposed
        protected abstract void Dispose(bool disposing);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "Bad Linter")]
        public void Dispose()
        {
            isLoaded = false;

            if (!isCancelled)
            {
                CancelLoad();
            }

            if (OvrAvatarManager.initialized)
            {
                OvrAvatarManager.RemoveAsset(this);
            }

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~OvrAvatarAssetBase()
        {
            Dispose(false);
        }

        /**
         * Coroutine to wait until an asset has finished loading.
         * Each frame it checks for load completion or cancellation.
         * @see isLoaded
         * @see isCancelled
         */
        public IEnumerator WaitForAssetToLoad()
        {
            while (!isLoaded && !isCancelled)
            {
                yield return null;
            }
        }

        /**
         * Cancel the loading of this asset.
         * @see isCancelled
         */
        public void CancelLoad()
        {
            isCancelled = true;
            isLoaded = false;
            _ExecuteCancel();
        }

        abstract protected void _ExecuteCancel();
    }

    public abstract class OvrAvatarAsset<T> : OvrAvatarAssetBase where T : struct
    {
        public readonly T data;

        protected OvrAvatarAsset(CAPI.ovrAvatar2Id assetId, T data) : base(assetId)
        {
            this.data = data;
        }
    }

}
