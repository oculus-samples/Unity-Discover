using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Avatar2
{
    public sealed class OvrAvatarStatsTracker
    {
        #region Singleton (Lazy Initialize)
        private static OvrAvatarStatsTracker _instance;

        public static OvrAvatarStatsTracker Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new OvrAvatarStatsTracker();
                }

                return _instance;
            }
        }

        void Awake()
        {
            _instance = this;
        }
        #endregion

        private readonly List<CAPI.ovrAvatar2Id> loadedResourceIds = new List<CAPI.ovrAvatar2Id>();
        private readonly List<CAPI.ovrAvatar2Id> failedResourceIds = new List<CAPI.ovrAvatar2Id>();

        public int numberPrimitivesLoaded => loadedResourceIds.Count;

        public int numberPrimitivesFailed => failedResourceIds.Count;

        private float _maxLoadTime = 0;
        // max load time, period between files requested and files ready
        public float maxLoadTime
        {
            get { return _maxLoadTime; }
            private set { _maxLoadTime = value; }
        }
        private float _cumulativeLoadTime = 0;
        // average load time, period between files requested and files ready
        public float averageLoadTime => _cumulativeLoadTime / numberPrimitivesLoaded;

        private float _maxFailedTime = 0;
        // max load time, period between files requested and files ready
        public float maxFailedTime
        {
            get { return _maxFailedTime; }
            private set { _maxFailedTime = value; }
        }
        private float _cumulativeFailedTime = 0;
        // average load time, period between files requested and files ready
        public float averageFailedTime => _cumulativeFailedTime / numberPrimitivesFailed;

        private float _maxReadyTime = 0;
        // max ready time, period between construction and ready to render
        public float maxReadyTime
        {
            get { return _maxReadyTime; }
            private set { _maxReadyTime = value; }
        }
        private float _cumulativeReadyTime = 0;
        // average ready time, period between construction and ready to render
        public float averageReadyTime => _cumulativeReadyTime / numberPrimitivesLoaded;

        private void ResolveLoadedId(CAPI.ovrAvatar2Id resourceId)
        {
            if (!loadedResourceIds.Contains(resourceId))
            {
                loadedResourceIds.Add(resourceId);
            }
        }
        private void ResolveFailedId(CAPI.ovrAvatar2Id resourceId)
        {
            if (!failedResourceIds.Contains(resourceId))
            {
                failedResourceIds.Add(resourceId);
            }
        }

        internal void TrackLoadDuration(CAPI.ovrAvatar2Id resourceId, float time)
        {
            ResolveLoadedId(resourceId);
            _cumulativeLoadTime += time;
            if (time > _maxLoadTime)
            {
                _maxLoadTime = time;
            }
        }

        internal void TrackFailedDuration(CAPI.ovrAvatar2Id resourceId, float time)
        {
            ResolveFailedId(resourceId);
            _cumulativeFailedTime += time;
            if (time > _maxFailedTime)
            {
                _maxFailedTime = time;
            }
        }

        internal void TrackReadyDuration(CAPI.ovrAvatar2Id resourceId, float time)
        {
            ResolveLoadedId(resourceId);
            _cumulativeReadyTime += time;
            if (time > _maxReadyTime)
            {
                _maxReadyTime = time;
            }
        }
    }
}
