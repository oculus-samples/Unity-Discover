using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Avatar2
{
    ///
    /// Represents a request to load a textured mesh.
    /// This will spawn requests to load one or more
    /// primitives which will in turn load one or more textures.
    /// This class accumulates the loaded meshes and textures
    /// as lists of @ref OvrAvatarPrimitive and @ref OvrAvatarImage instances.
    ///
    /// Resource loading is asynchronous and begins when
    /// @ref StartLoad is called.
    ///
    public sealed class OvrAvatarResourceLoader : IDisposable
    {
        private const string logScope = "resourceLoader";

        private OvrAvatarResourceTimer resourceTimer = null; // Should be only present in debug builds

        internal static void ResourceCallbackHandler(
            in CAPI.ovrAvatar2Asset_Resource resource,
            Dictionary<CAPI.ovrAvatar2Id,
            OvrAvatarResourceLoader> resourceMap,
            out OvrAvatarResourceLoader queueLoad)
        {
            OvrAvatarLog.LogVerbose(
                $"Received resource callback {resource.assetID} with status {resource.status}"
                , logScope);

            queueLoad = null;

            switch (resource.status)
            {
                case CAPI.ovrAvatar2AssetStatus.ovrAvatar2AssetStatus_LoadFailed:
                    if (!OvrAvatarManager.shuttingDown)
                    {
                        OvrAvatarLog.LogError($"Failed to load resource");

                        // Try unload, just in case something has gone whacky
                        if (resourceMap.TryGetValue(resource.assetID
                            , out OvrAvatarResourceLoader failedLoader))
                        {
                            failedLoader.Unload();
                            resourceMap.Remove(resource.assetID);
                            failedLoader.resourceTimer?.TrackStatusEvent(OvrAvatarResourceTimer.AssetLifeTimeStatus.LoadFailed);
                        }
                    }
                    break;

                case CAPI.ovrAvatar2AssetStatus.ovrAvatar2AssetStatus_Loaded:

                    if (!resourceMap.TryGetValue(resource.assetID, out var current))
                    {
                        var newLoader = new OvrAvatarResourceLoader(resource.assetID);
                        resourceMap.Add(resource.assetID, newLoader);
                        queueLoad = newLoader;
                        OvrAvatarLog.LogDebug($"Mapped resource id:{resource.assetID}", logScope);
                    }
                    else
                    {
                        OvrAvatarLog.LogDebug(
                            $"Resource id: {resource.assetID} already loaded {current}", logScope);
                    }

                    break;

                case CAPI.ovrAvatar2AssetStatus.ovrAvatar2AssetStatus_Unloaded:
                    if (resourceMap.TryGetValue(resource.assetID, out var loader))
                    {
                        loader.Unload();
                        loader.resourceTimer?.TrackStatusEvent(OvrAvatarResourceTimer.AssetLifeTimeStatus.Unloaded);
                        resourceMap.Remove(resource.assetID);
                    }
                    else
                    {
                        OvrAvatarLog.LogWarning(
                            $"Unable to unloading resource id:{resource.assetID}, not found", logScope);
                    }
                    break;

                case CAPI.ovrAvatar2AssetStatus.ovrAvatar2AssetStatus_Updated:
                    throw new InvalidOperationException("Resource updates are not currently supported");

                default:
                    throw new InvalidOperationException($"Unexpected resource status {resource.status}");
            }
        }

        private readonly List<OvrAvatarPrimitive> _primitives = new List<OvrAvatarPrimitive>();
        private readonly List<OvrAvatarImage> _images = new List<OvrAvatarImage>();

        // Effectively indicates whether load is in progress
        private OvrTime.SliceHandle _loadResourceAsyncCoroutine = default;

#if UNITY_EDITOR
        internal List<OvrAvatarPrimitive> Primitives => _primitives;

        internal List<OvrAvatarImage> Images => _images;
#endif

        private bool _hasReleasedNativeResources = false;
        public bool isCancelled { get; private set; } = false;
        public bool isDisposed { get; private set; } = false;

        public bool CanLoad => !(isCancelled || isDisposed);

        public readonly CAPI.ovrAvatar2Id resourceId;

        internal OvrAvatarImage CreateImage(in CAPI.ovrAvatar2MaterialTexture textureData
            , in CAPI.ovrAvatar2Image imageData, uint imageIndex, CAPI.ovrAvatar2Id resourceId)
        {
            bool srgb = (textureData.type == CAPI.ovrAvatar2MaterialTextureType.BaseColor);
            var newImage = new OvrAvatarImage(resourceId, imageIndex, imageData, srgb);
            _images.Add(newImage);
            return newImage;
        }

        public OvrAvatarResourceLoader(CAPI.ovrAvatar2Id resourceIdentifier)
        {
            OvrAvatarLog.Assert(resourceIdentifier != CAPI.ovrAvatar2Id.Invalid);
            this.resourceId = resourceIdentifier;
            // only allow for load time tracking overhead if logging level is high enough
            if (Debug.isDebugBuild)
            {
                resourceTimer = new OvrAvatarResourceTimer(this);
            }

            resourceTimer?.TrackStatusEvent(OvrAvatarResourceTimer.AssetLifeTimeStatus.Created);
            CreateResourcePrimitives();
        }

        private void CreateResourcePrimitives()
        {
            // Get Primitive Count
            var result = CAPI.ovrAvatar2Asset_GetPrimitiveCount(resourceId, out UInt32 primitiveCount);
            if (result != CAPI.ovrAvatar2Result.Success)
            {
                OvrAvatarLog.LogError($"LoadResource Error: GetMeshPrimitiveCount {result}", logScope);

                ReleaseNativeResource();
                return;
            }

            // Load Primitives
            _primitives.Capacity = (int)primitiveCount;
            for (UInt32 i = 0; i < primitiveCount; ++i)
            {
                result = CAPI.ovrAvatar2Asset_GetPrimitiveByIndex(resourceId, i,
                    out CAPI.ovrAvatar2Primitive primitiveData);
                if (result != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogError(
                        $"LoadResource Error: GetPrimitiveByIndex {result}", logScope);

                    ReleaseNativeResource();
                    return;
                }

                if (OvrAvatarManager.IsOvrAvatarAssetLoaded(primitiveData.id))
                {
                    OvrAvatarLog.LogWarning(
                        $"Mesh primitive with id {primitiveData.id} already exists.", logScope);
                    continue;
                }

                OvrAvatarLog.LogVerbose($"Mapped primitive id:{primitiveData.id}", logScope);
                var newPrimitive = new OvrAvatarPrimitive(this, in primitiveData);

                _primitives.Add(newPrimitive);
            }
        }

        internal void StartLoad()
        {
            _loadResourceAsyncCoroutine = OvrTime.Slice(LoadResourceAsync());
        }

        private bool CheckCancel(OvrAvatarAssetBase asset, out OvrTime.SliceStep step)
        {
            bool cancelled = asset.isCancelled;
            if (cancelled)
            {
                OvrAvatarLog.LogVerbose(
                    $"{asset.typeName} {asset.assetName} cancelled during resource load."
                    , logScope);

                // Resume checking next frame
                // TODO: Switch to Wait, but currently no unit test - use Delay for now
                // yield return OvrTime.SliceStep.Wait;
                step = OvrTime.SliceStep.Delay;
            }
            else // !isCancelled
            {
                // Loading in progress, delay next slice
                step = OvrTime.SliceStep.Delay;
            }
            return cancelled;
        }

        private IEnumerator<OvrTime.SliceStep> LoadResourceAsync()
        {
            resourceTimer?.TrackStatusEvent(OvrAvatarResourceTimer.AssetLifeTimeStatus.LoadStarted);

            foreach (var primitive in _primitives)
            {
                primitive.StartLoad(this);

                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }
            }

            foreach (var newPrimitive in _primitives)
            {
                while (!newPrimitive.hasCopiedAllResourceData)
                {
                    yield return OvrTime.SliceStep.Delay;
                }
            }
            foreach (var newImage in _images)
            {
                while (!newImage.hasCopiedAllResourceData)
                {
                    yield return OvrTime.SliceStep.Delay;
                }
            }

            resourceTimer?.TrackStatusEvent(OvrAvatarResourceTimer.AssetLifeTimeStatus.Loaded);

            // Notify nativeSDK that we no longer will access the resource data
            ReleaseNativeResource();

            OvrAvatarLog.LogVerbose($"Releasing native resources for resourceId {resourceId}", logScope);

            if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }

            // Wait until all primitives are fully loaded
            foreach (var newPrimitive in _primitives)
            {
                while (!newPrimitive.isLoaded && !CheckCancel(newPrimitive, out var step))
                {
                    yield return step;
                }
            }
            foreach (var newImage in _images)
            {
                while (!newImage.isLoaded && !CheckCancel(newImage, out var step))
                {
                    yield return step;
                }
            }

            _loadResourceAsyncCoroutine.Clear();

            if (OvrAvatarManager.hasInstance)
            {
                OvrAvatarManager.Instance.ResourceLoadComplete(this);
            }

            MarkResourceReadyToRender();

            // If we reach here, we have finished w/out being (directly) cancelled - congratulations!
            OvrAvatarLog.LogVerbose($"LoadResourceAsync completed for resourceId {resourceId}", logScope);
        }

        public void CancelLoad()
        {
            if (!_hasReleasedNativeResources)
            {
                ReleaseNativeResource();
            }

            if (_loadResourceAsyncCoroutine.IsValid)
            {
                OvrAvatarLog.LogVerbose($"Stopping LoadResourceAsync coroutine for resourceId {resourceId}");

                if (!_loadResourceAsyncCoroutine.Cancel())
                {
                    // If cancellation fails w/out crashing - something gnarly has happened
                    OvrAvatarLog.LogError($"Slice for resourceId {resourceId} failed to Cancel");
                }

                if (OvrAvatarManager.hasInstance)
                {
                    OvrAvatarManager.Instance.ResourceLoadCancelled(this);
                }
            }

            isCancelled = true;
        }

        // Unity no longer needs access to the native resouce, release it
        private void ReleaseNativeResource()
        {
            // TODO: Good use case for "ensure"
            OvrAvatarLog.Assert(resourceId != CAPI.ovrAvatar2Id.Invalid);

            if (resourceId != CAPI.ovrAvatar2Id.Invalid)
            {
                _hasReleasedNativeResources = CAPI.OvrAvatarAsset_ReleaseResource(resourceId);
                OvrAvatarLog.Assert(_hasReleasedNativeResources);
            }
        }

        private void MarkResourceReadyToRender()
        {
            // TODO: Good use case for "ensure"
            OvrAvatarLog.Assert(resourceId != CAPI.ovrAvatar2Id.Invalid);

            if (resourceId != CAPI.ovrAvatar2Id.Invalid)
            {
                bool readyToRender = CAPI.OvrAvatarAsset_ResourceReadyToRender(resourceId);
                OvrAvatarLog.Assert(readyToRender);
                resourceTimer?.TrackStatusEvent(OvrAvatarResourceTimer.AssetLifeTimeStatus.ReadyToRender);
            }
        }

        private void Unload()
        {
            OvrAvatarLog.LogDebug($"Unloading resource id: {resourceId}", logScope);
            Dispose();
        }

        // IDisposable interface
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_primitives.Count > 0)
                {
                    OvrAvatarLog.LogDebug(
                        $"Disposing {_primitives.Count} OvrAvatarPrimitive instances for resourceId {resourceId}");
                    foreach (var primitive in _primitives)
                    {
                        primitive.Dispose();
                    }
                    _primitives.Clear();
                }

                if (_images.Count > 0)
                {
                    OvrAvatarLog.LogDebug(
                        $"Disposing {_images.Count} OvrAvatarImage instances for resourceId {resourceId}");
                    foreach (var image in _images)
                    {
                        image.Dispose();
                    }
                    _images.Clear();
                }

                CancelLoad();
            }
            else
            {
                OvrAvatarLog.LogError($"Finalized {resourceId} w/out main thread dispose", logScope);
            }
            isDisposed = true;
        }

        // Called from `OvrAvatarManager.Shutdown`
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~OvrAvatarResourceLoader()
        {
            Dispose(false);
        }
    }
}
