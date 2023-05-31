using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Oculus.Avatar2;

namespace Oculus.Avatar2
{
    internal sealed class OvrAvatarResourceTimer
    {
        internal enum AssetLifeTimeStatus
        {
            Created,
            LoadStarted,
            LoadFailed,
            Loaded,
            Unloaded,
            ReadyToRender
        }

        private const string logScope = "ResourceTimers";

        private OvrAvatarResourceLoader parentLoader = null;

        private float _resourceCreatedTime = 0;
        internal float resourceCreatedTime
        {
            get { return _resourceCreatedTime; }
            private set
            {
                _resourceCreatedTime = value;
            }
        }

        private float _resourceLoadStartedTime = 0;
        internal float resourceLoadStartedTime
        {
            get { return _resourceLoadStartedTime; }
            private set
            {
                _resourceLoadStartedTime = value;
            }
        }

        private float _resourceLoadedTime;
        internal float resourceLoadedTime
        {
            get { return _resourceLoadedTime; }
            private set
            {
                _resourceLoadedTime = value;
                if (resourceCreatedTime != 0)
                {
                    float loadingTime = _resourceLoadedTime - resourceCreatedTime;
                    OvrAvatarLog.LogDebug($"Resource {parentLoader.resourceId} asset loading time: {loadingTime}", logScope);
                    OvrAvatarStatsTracker.Instance.TrackLoadDuration(parentLoader.resourceId, loadingTime);
                }
            }
        }


        private float _resourceLoadFailedTime;
        internal float resourceLoadFailedTime
        {
            get { return _resourceLoadFailedTime; }
            private set
            {
                _resourceLoadFailedTime = value;
                if (resourceCreatedTime != 0)
                {
                    float loadingTime = _resourceLoadFailedTime - resourceCreatedTime;
                    OvrAvatarLog.LogDebug($"Resource {parentLoader.resourceId} had a failure loading after: {loadingTime}", logScope);
                    OvrAvatarStatsTracker.Instance.TrackFailedDuration(parentLoader.resourceId, loadingTime);
                }
            }
        }

        private float _resourceReadyToRenderTime;
        internal float resourceReadyToRenderTime
        {
            get { return _resourceReadyToRenderTime; }
            private set
            {
                _resourceReadyToRenderTime = value;
                if (resourceCreatedTime != 0)
                {
                    float totalTime = _resourceReadyToRenderTime - resourceCreatedTime;
                    OvrAvatarLog.LogDebug($"Resource {parentLoader.resourceId} total creation time: {totalTime}", logScope);
                    OvrAvatarStatsTracker.Instance.TrackReadyDuration(parentLoader.resourceId, totalTime);
                }
            }
        }
        private float _resourceUnloadedTime;
        internal float resourceUnloadedTime
        {
            get { return _resourceUnloadedTime; }
            private set
            {
                _resourceUnloadedTime = value;
                if (resourceCreatedTime != 0)
                {
                    float totalTime = _resourceUnloadedTime - resourceCreatedTime;
                    OvrAvatarLog.LogDebug($"Resource {parentLoader.resourceId} unloaded after a lifetime of {totalTime}", logScope);
                }
            }
        }

        // TODO (jsepulveda, 8/25/21)
        // For now we're tracking these status changes from direct calls to this function
        // but in the future we should recieve asynchronous callbacks from the SDK.
        internal void TrackStatusEvent(AssetLifeTimeStatus status)
        {
            float currentTime = Time.realtimeSinceStartup;

            switch(status) {
                case AssetLifeTimeStatus.LoadFailed:
                    {
                        resourceLoadFailedTime = currentTime;
                    } break;
                case AssetLifeTimeStatus.Loaded:
                    {
                        resourceLoadedTime = currentTime;
                    }
                    break;
                case AssetLifeTimeStatus.Unloaded:
                    {
                        resourceUnloadedTime = currentTime;
                    }
                    break;
                case AssetLifeTimeStatus.Created:
                    {
                        resourceCreatedTime = currentTime;
                    }
                    break;
                case AssetLifeTimeStatus.LoadStarted:
                    {
                        resourceLoadStartedTime = currentTime;
                    }
                    break;
                case AssetLifeTimeStatus.ReadyToRender:
                    {
                        resourceReadyToRenderTime = currentTime;
                    }
                    break;

            }
        }

        private OvrAvatarResourceTimer() { }
        public OvrAvatarResourceTimer(OvrAvatarResourceLoader loader)
        {
            parentLoader = loader;
        }
    }

}
