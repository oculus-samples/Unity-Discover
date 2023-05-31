using System;
using System.Collections.Generic;
using System.Diagnostics;

using UnityEngine;

using Debug = UnityEngine.Debug;

namespace Oculus.Avatar2
{
    public partial class OvrAvatarEntity : MonoBehaviour
    {
        [Header("LOD")]
        private AvatarLOD _avatarLOD;

        private readonly LodData[] _visibleLodData = new LodData[CAPI.ovrAvatar2EntityLODFlagsCount];

        /// Number of LODs loaded for this avatar.
        private int lodObjectCount { get; set; } = 0;

        /// Index of lowest quality level of detail loaded for this avatar.
        public int LowestQualityLODIndex { get; private set; } = -1;

        /// Index of highest quality level of detail loaded for this avatar.
        public int HighestQualityLODIndex { get; private set; } = -1;

        /// Provides vertex and triangle counts for each level of detail.
        public IReadOnlyList<AvatarLODCostData> CopyVisibleLODCostData()
        {
            var lodCosts = new AvatarLODCostData[CAPI.ovrAvatar2EntityLODFlagsCount];
            var allLodCost = _visibleAllLodData.IsValid ? _visibleAllLodData.totalCost : default;
            for (int idx = 0; idx < _visibleLodData.Length; idx++)
            {
                ref readonly var lodObj = ref _visibleLodData[idx];
                lodCosts[idx] = AvatarLODCostData.Sum(in allLodCost, in lodObj.totalCost);
            }
            return lodCosts;
        }

        // TODO: This is a silly method to have - IReadOnlyDictionary maybe?
        private Dictionary<int, LodData> CopyVisibleLODData()
        {
            var lodDict = new Dictionary<int, LodData>(lodObjectCount);
            for (int idx = 0; idx < _visibleLodData.Length; idx++)
            {
                ref var lodObj = ref _visibleLodData[idx];
                if (lodObj.HasInstances)
                {
                    lodDict.Add(idx, lodObj);
                }
            }
            return lodDict;
        }

        /// Per-avatar level of detail information.
        public AvatarLOD AvatarLOD
        {
            get
            {
                if (_avatarLOD == null)
                {
                    _avatarLOD = gameObject.GetOrAddComponent<AvatarLOD>();

                    // In some edge cases `GetOrAddComponent` can return null
                    if (_avatarLOD != null)
                    {
                        _avatarLOD.Entity = this;
                    }
                }

                return _avatarLOD;
            }
        }

        // TODO: Setup LOD control via these properties, suppressing unused warnings for now
#pragma warning disable 0414
        // Intended LOD to render
        // TODO: Have LOD system drive this value
        private readonly uint _targetLodIndex = 0;
        // LOD currently being rendered
        // TODO: Drive this from render state + `ovrAvatar2Entity_[Get/Set]LodFlags`
        private readonly int _currentLodIndex = -1;
#pragma warning restore 0414

        private LodData _visibleAllLodData;

        // High level container for a given singular LOD, may combine multiple primitive instances
        public struct LodData
        {
            internal LodData(GameObject gob)
            {
                gameObject = gob;
                transform = gob.transform;

                instances = new HashSet<OvrAvatarRenderable>();
                totalCost = default;
            }

            public bool IsValid => gameObject != null;
            public bool HasInstances => instances != null && instances.Count > 0;

            // TODO: Refactor AvatarLOD and remove
            public int vertexCount => (int) totalCost.meshVertexCount;
            public int triangleCount => (int) totalCost.renderTriangleCount;

            // TODO: Remove gameObject and transform fields - manage these internally
            public readonly GameObject gameObject;
            public readonly Transform transform;

            // Discrete renderables, may be parented to various gameObjects
            private readonly HashSet<OvrAvatarRenderable> instances;

            public AvatarLODCostData totalCost;

            internal void AddInstance(OvrAvatarRenderable newInstance)
            {
                if (instances.Add(newInstance))
                {
                    totalCost = AvatarLODCostData.Sum(in totalCost, in newInstance.CostData);
                }
            }
            internal bool RemoveInstance(OvrAvatarRenderable oldInstance)
            {
                bool didRemove = instances.Remove(oldInstance);
                if (didRemove)
                {
                    totalCost = AvatarLODCostData.Subtract(in totalCost, in oldInstance.CostData);
                }
                OvrAvatarLog.Assert(didRemove, logScope);
                return didRemove;
            }
            internal void Clear()
            {
                instances.Clear();
                totalCost = default;
            }
        }

        private void InitAvatarLOD()
        {
            AvatarLOD.CulledChangedEvent += OnCullChangedEvent;  // Access to the public AvatarLod causes the component to be GetOrAdded (see above)
        }

        internal void UpdateAvatarLODOverride()    // internal so it can be called from the LOD Manager, which runs at a slower framerate
        {
            if (_avatarLOD != null)
            {
                _avatarLOD.UpdateOverride();
            }
        }

        private  void ShutdownAvatarLOD()
        {
            if (_avatarLOD != null) // check the private instance to avoid creating a new one on the spot
            {
                _avatarLOD.CulledChangedEvent -= OnCullChangedEvent;

                Destroy(_avatarLOD);
                _avatarLOD = null;
            }
        }

        protected virtual void ComputeImportanceAndCost(out float importance, out UInt32 cost)
        {
            var avatarLod = AvatarLOD;
            var avatarLevel = avatarLod.Level;
            if (0 <= avatarLevel)
            {
                importance = avatarLod.updateImportance;
                cost = avatarLod.UpdateCost;
            }
            else
            {
                importance = 0f;
                cost = 0;
            }
        }

        internal void SendImportanceAndCost()    // internal so it can be called from the LOD Manager, which runs at a slower framerate
        {
            ComputeImportanceAndCost(out float importance, out UInt32 cost);

            // set importance for next frame
            CAPI.ovrAvatar2Importance_SetImportanceAndCost(entityId, importance, cost);
        }

        [Conditional("UNITY_DEVELOPMENT")]
        [Conditional("UNITY_EDITOR")]
        internal void TrackUpdateAge()
        {
#if UNITY_EDITOR || UNITY_DEVELOPMENT
            var avatarLod = AvatarLOD;
            // Track of the last update time for debug tools
            if (EntityActive)
            {
                avatarLod.previousUpdateAgeWindowSeconds = avatarLod.lastUpdateAgeSeconds + Time.deltaTime;
                avatarLod.lastUpdateAgeSeconds = 0;
            }
            else
            {
                avatarLod.lastUpdateAgeSeconds += Time.deltaTime;
            }
#endif // UNITY_EDITOR || UNITY_DEVELOPMENT
        }

        protected virtual void OnCullChangedEvent(bool culled)
        {
            OnCulled?.Invoke(culled);

#if AVATAR_CULLING_DEBUG
            // Use to easily debug but do not check in enabled, its way too verbose and allocates string:
            OvrAvatarLog.LogInfo("Caught culling event for Avatar " + AvatarLOD.name + (culled ? "":" NOT") + " CULLED", logScope, this);
#endif
        }

        private void SetupLodGroups()
        {
            bool setupLodGroups = false;

            var avatarLod = AvatarLOD;
            if (lodObjectCount > 1)
            {
                // TODO: Update avatarLOD to take array, didn't want to change *too* many classes all at once
                var lodDict = CopyVisibleLODData();

                // Don't add if effective lodCount is <= 1
                // TODO: It seems like this should be handled by `AvatarLOD`?
                setupLodGroups = lodDict.Count > 1;
                if (setupLodGroups)
                {
                    avatarLod.AddLODGameObjectGroupBySdkRenderers(lodDict);
                }
            }

            if (!setupLodGroups)
            {
                avatarLod.ClearLODGameObjects();
            }
            avatarLod.AddLODActionGroup(gameObject, UpdateAvatarLodColor, 5);
        }

        private void ResetLodCullingPoints()
        {
            if (_avatarLOD != null)
            {
                _avatarLOD.Reset();
            }
        }

        private void SetupLodCullingPoints()
        {
            // TODO: This seems like mostly logic which should live in AvatarLODManager?
            // populate the centerXform and the extraXforms for culling
            if (HasJoints)
            {
                var avatarLod = AvatarLOD;
                var lodManager = AvatarLODManager.Instance;

                var skelJoint = GetSkeletonTransformByType(lodManager.JointTypeToCenterOn);
                bool hasSkelJoint = skelJoint != null;
                if(!hasSkelJoint) {
                    OvrAvatarLog.LogError($"SkeletonJoint not found for center joint {lodManager.JointTypeToCenterOn}", logScope, this);
                }

                avatarLod.centerXform = hasSkelJoint ? skelJoint : _baseTransform;

                avatarLod.extraXforms.Clear();

                foreach (var jointType in lodManager.jointTypesToCullOnArray)
                {
                    var cullJoint = GetSkeletonTransformByType(jointType);
                    if (cullJoint)
                    {
                        avatarLod.extraXforms.Add(cullJoint);
                    }
                    else
                    {
                        OvrAvatarLog.LogError($"Unable to find cullJoint for jointType {jointType}", logScope, this);
                    }
                }
            }
            else
            {
                // If there are no skeletal joints, reset AvatarLOD to default settings
                TeardownLodCullingPoints();
            }
        }

        private void TeardownLodCullingPoints()
        {
            if (_avatarLOD != null)
            {
                // reset JointToCenterOn
                _avatarLOD.centerXform = _baseTransform;

                // reset extraXforms
                _avatarLOD.extraXforms.Clear();
            }
        }

        // Used for tracking Entity's valid LOD range
        private void ResetLODRange()
        {
            LowestQualityLODIndex = HighestQualityLODIndex = -1;
        }
        private void ExpandLODRange(uint lod)
        {
            // TODO: Initial values of -1/-1 aren't super clean
            if (LowestQualityLODIndex < lod) { LowestQualityLODIndex = (int) lod; }
            if (HighestQualityLODIndex < 0 || HighestQualityLODIndex > lod) { HighestQualityLODIndex = (int) lod; }
        }
        private void RefreshLODRange()
        {
            ResetLODRange();
            for (uint lodIdx = 0; lodIdx < _visibleLodData.Length; ++lodIdx)
            {
                if (_visibleLodData[lodIdx].HasInstances)
                {
                    ExpandLODRange(lodIdx);
                }
            }
        }

        // Perform runtime configuration when `IsLocal==true`
        private void ConfigureLocalAvatarSettings()
        {
            OvrAvatarLog.Assert(IsLocal, logScope, this);

            // If we are local, update AvatarLODManager to assign `firstPersonAvatarLod`
            // NOTE: `CAPI.ovrAvatar2EntityViewFlags.FirstPerson` is not actually required to use this property
            // "firstPerson" refers to whether there is a camera being used to render from this avatar's perspective
            // TODO: This needs to be improved to support "possessing" multiple avatars :/
            var lodManager = AvatarLODManager.Instance;
            if (lodManager == null) { return; }

            var childCamera = GetComponentInChildren<Camera>();
            // Check if we have a camera
            if (childCamera == null) { return; }

            // Confirm that it is the same camera LODManager is using
            var lodCamera = lodManager.CurrentCamera;
            if (!(lodCamera is null) && lodCamera != childCamera) { return; }

            lodManager.firstPersonAvatarLod = AvatarLOD;
            // No need to motion smooth, as we will skin every frame
            MotionSmoothingSettings = MotionSmoothingOptions.FORCE_OFF;

            OvrAvatarLog.LogDebug(
                "Disabled motion smoothing per AvatarLODManager config", logScope, this);
        }
    }
}
