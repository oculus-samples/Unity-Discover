using System;
using System.Collections.Generic;
using System.Diagnostics;

using UnityEngine;
using UnityEngine.Profiling;

using CultureInfo = System.Globalization.CultureInfo;
using Debug = UnityEngine.Debug;

namespace Oculus.Avatar2
{
    /**
     * Per-avatar LOD information.
     * This component is added to every avatar managed by the LOD manager.
     * It is informational and not intended to be changed by the application.
     * @see OvrAvatarLODManager
     */
    public sealed class AvatarLOD : MonoBehaviour
    {
        private const string logScope = "AvatarLOD";

        /// Cached transform for this AvatarLOD
        public Transform CachedTransform { get; private set; } = null;

        /// The entity whose LOD is managed by this instance
        public OvrAvatarEntity Entity { get; internal set; } = null;

        // Whether the entity associated with this AvatarLOD is non-null and active
        public bool EntityActive => !(Entity is null) && Entity.isActiveAndEnabled;

        /// True if the avatar has been culled by the LOD manager.
        public bool culled { get; private set; }

        /// If enabled, the overrideLevel value will be used instead of the calculated LOD.
        public bool overrideLOD = false;

        private bool _prevOverrideLOD = false;

        /// Desired level of detail for this avatar.
        public int overrideLevel
        {
            get => Mathf.Clamp(_overrideLevel, -1, maxLodLevel);
            set => _overrideLevel = value;
        }

        // TODO: Initialize to `int.MinValue`?
        private int _overrideLevel = default;
        private int _prevOverrideLevel = default;

        /// Transform on the avatar center joint.
        public Transform centerXform;

        public readonly List<Transform> extraXforms = new List<Transform>();
        private readonly List<AvatarLODGroup> _lodGroups = new List<AvatarLODGroup>();

        /// Vertex counts for each level of detail for this avatar.
        public readonly List<int> vertexCounts = new List<int>();

        /// Triangle counts for each level of detail for this avatar.
        public readonly List<int> triangleCounts = new List<int>();

        private int _minLodLevel = -1;

        /// Minimum LOD level loaded for this avatar.
        public int minLodLevel => _minLodLevel;

        private int _maxLodLevel = -1;

        /// Maximum LOD level loaded for this avatar.
        public int maxLodLevel => _maxLodLevel;

        /// Distance of avatar center joint from the LOD camera.
        public float distance;

        /// Screen percent occupied by the avatar (0 - 1).
        public float screenPercent;

        /// LOD level calculated based on screen percentage (before dynamic processing).
        public int wantedLevel;

        /// LOD level calculated after dynamic processing.
        public int dynamicLevel;

        ///
        /// Importance of avatar for display purposes (geometric LOD).
        /// This is from a logarithmic function by OvrAvatarLODManager.
        /// @see OvrAvatarLODManager.dynamicLodWantedLogScale
        ///
        public float lodImportance;

        ///
        /// Importance of avatar for update (animation) purposes.
        /// This is from a logarithmic function by OvrAvatarLODManager.
        /// @see OvrAvatarLODManager.screenPercentToUpdateImportanceCurvePower
        ///  @see OvrAvatarLODManager.screenPercentToUpdateImportanceCurveMultiplier.
        ///
        public float updateImportance;

        /// Network streaming fidelity for this avatar.
        public OvrAvatarEntity.StreamLOD dynamicStreamLod;

        /// Event invoked when the avatar's cull status has changed.
        /// This event is also available from OvrAvatarLODManager.
        /// @see OvrAvatarLODManager.CullChangedEvent
        public Action<bool> CulledChangedEvent;

        /// Event invoked when the avatar's cull status has changed.
        /// This event is also available from OvrAvatarLODManager.
        /// @see OvrAvatarLODManager.OnCullChangedEvent
        public event Action<AvatarLOD, bool> OnCulledChangedEvent;

        private bool forceDisabled_;

        private int _level;
        private int _prevLevel;

        public int Level
        {
            get => _level;
            set
            {
                if (value == _prevLevel) { return; }
                _level = value;

                if (!overrideLOD)
                {
                    UpdateLOD();
                    UpdateDebugLabel();
                }

                _prevLevel = _level;
            }
        }

        public UInt32 UpdateCost
        {
            get
            {
                // Clear
                // ASSUMPTION: Not more than 31 lods, so using int as bitfields is sufficient
                int levelsWithCost = 0;

                // Check costs for all lod groups
                foreach (var lodGroup in _lodGroups) { levelsWithCost |= lodGroup.LevelsWithAnimationUpdateCost; }

                UInt32 cost = 0;
                for (int i = minLodLevel; i <= maxLodLevel; i++)
                {
                    cost += ((levelsWithCost & (i << i)) != 0) ? (UInt32)vertexCounts[i] : 0;
                }

                return cost;
            }
        }

        private void Awake()
        {
#if UNITY_EDITOR || UNITY_DEVELOPMENT
            _avatarId = ++_avatarIdSource;
#endif // UNITY_EDITOR || UNITY_DEVELOPMENT

            CachedTransform = transform;
        }

        private void Start()
        {
            AvatarLODManager.Instance.AddLOD(this);
            if (centerXform == null) { centerXform = CachedTransform; }
        }

        private void OnDestroy()
        {
            DestroyLODParentIfOnlyChild();
            AvatarLODManager.RemoveLOD(this);
        }

        // Returns true upon a state transition
        public bool SetCulled(bool nextCulled)
        {
            if (nextCulled == culled) { return false; }

            culled = nextCulled;
            CulledChangedEvent?.Invoke(culled);
            OnCulledChangedEvent?.Invoke(this, culled);
            return true;
        }

        private static List<AvatarLODParent> _parentsCache = null;

        private bool HasValidLODParent()
        {
            CachedTransform.parent.GetComponents(_parentsCache ??= new List<AvatarLODParent>());

            bool found = false;
            foreach (var lodParent in _parentsCache)
            {
                if (!lodParent.beingDestroyed)
                {
                    found = true;
                    break;
                }
            }
            _parentsCache.Clear();
            return found;
        }

        private void DestroyLODParentIfOnlyChild()
        {
            var cachedParent = CachedTransform.parent;
            if (cachedParent != null)
            {
                cachedParent.gameObject.GetComponents(_parentsCache ??= new List<AvatarLODParent>());
                foreach (var lodParent in _parentsCache) { lodParent.DestroyIfOnlyLODChild(this); }
                _parentsCache.Clear();
            }
        }


        private void OnBeforeTransformParentChanged()
        {
            DestroyLODParentIfOnlyChild();
        }

        private void OnTransformParentChanged()
        {
            var parentTx = CachedTransform.parent;
            if (parentTx != null && !HasValidLODParent()) { parentTx.gameObject.AddComponent<AvatarLODParent>(); }

            AvatarLODManager.ParentStateChanged(this);
        }

        // This behaviour is manually updated at a specific time during OvrAvatarManager::Update()
        // to prevent issues with Unity script update ordering
        internal void UpdateOverride()
        {
            if (!isActiveAndEnabled || forceDisabled_) { return; }

            Profiler.BeginSample("AvatarLOD::UpdateOverride");

            bool needsUpdateLod = (overrideLOD && overrideLevel != _prevOverrideLevel) ||
                                  (overrideLOD != _prevOverrideLOD);

            _prevOverrideLevel = overrideLevel;
            _prevOverrideLOD = overrideLOD;

            if (needsUpdateLod) { UpdateLOD(); }

#if UNITY_EDITOR || UNITY_DEVELOPMENT
            var needsDebugLabelUpdate = AvatarLODManager.Instance.debug.displayLODLabels ||
                                        AvatarLODManager.Instance.debug.displayAgeLabels;

            if (needsDebugLabelUpdate || needsUpdateLod) { UpdateDebugLabel(); }
#endif // UNITY_EDITOR || UNITY_DEVELOPMENT

            Profiler.EndSample();
        }

        private void UpdateLOD()
        {
            if (forceDisabled_) { return; }

            if (_lodGroups != null && _lodGroups.Count > 0)
            {
                foreach (var lodGroup in _lodGroups) { lodGroup.Level = overrideLOD ? overrideLevel : Level; }
            }
        }

        private void AddLODGroup(AvatarLODGroup group)
        {
            _lodGroups.Add(group);
            group.parentLOD = this;
            group.Level = overrideLOD ? overrideLevel : Level;
        }

        internal void RemoveLODGroup(AvatarLODGroup group)
        {
            _lodGroups.Remove(group);
        }

        internal void ClearLODGameObjects()
        {
            // Vertex counts will be reset by this function.
            vertexCounts.Clear();
            triangleCounts.Clear();

            _minLodLevel = -1;
            _maxLodLevel = -1;

#if UNITY_EDITOR || UNITY_DEVELOPMENT
            CAPI.ovrAvatar2LOD_UnregisterAvatar(_avatarId);
#endif // UNITY_EDITOR || UNITY_DEVELOPMENT
        }

        public void AddLODGameObjectGroupByAvatarSkinnedMeshRenderers(GameObject parentGameObject, Dictionary<string, List<GameObject>> suffixToObj)
        {
            foreach (var kvp in suffixToObj)
            {
                AvatarLODSkinnableGroup gameObjectGroup = parentGameObject.GetOrAddComponent<AvatarLODSkinnableGroup>();
                gameObjectGroup.GameObjects = kvp.Value.ToArray();
                AddLODGroup(gameObjectGroup);
            }
        }

        public void AddLODGameObjectGroupBySdkRenderers(Dictionary<int, OvrAvatarEntity.LodData> lodObjects)
        {
            // Vertex counts will be reset by this function.
            vertexCounts.Clear();
            triangleCounts.Clear();

            if (lodObjects.Count > 0)
            {
                // first see what the limits could be...
                _minLodLevel = int.MaxValue;
                _maxLodLevel = int.MinValue;

                foreach (var entry in lodObjects)
                {
                    int lodIndex = entry.Key;
                    if (_minLodLevel > lodIndex) { _minLodLevel = lodIndex; }
                    if (_maxLodLevel < lodIndex) { _maxLodLevel = lodIndex; }
                }

                OvrAvatarLog.LogVerbose($"Set lod range (min:{_minLodLevel}, max:{_maxLodLevel})", logScope, this);
            }
            else
            {
                OvrAvatarLog.LogError("No LOD data specified", logScope, this);

                _maxLodLevel = _minLodLevel = -1;
            }

            // first find common parent and children;
            if (maxLodLevel >= vertexCounts.Count || maxLodLevel >= triangleCounts.Count)
            {
                vertexCounts.Capacity = maxLodLevel;
                triangleCounts.Capacity = maxLodLevel;
                while (maxLodLevel >= vertexCounts.Count) { vertexCounts.Add(0); }
                while (maxLodLevel >= triangleCounts.Count) { triangleCounts.Add(0); }
            }

            GameObject[] children = new GameObject[maxLodLevel + 1];
            Transform commonParent = null;
            for (int lodIdx = minLodLevel; lodIdx <= maxLodLevel; ++lodIdx)
            {
                if (lodObjects.TryGetValue(lodIdx, out var lodData))
                {
                    vertexCounts[lodIdx] = lodData.vertexCount;
                    triangleCounts[lodIdx] = lodData.triangleCount;

                    children[lodIdx] = lodData.gameObject;

                    var localParentTx = lodData.transform.parent;

                    OvrAvatarLog.AssertConstMessage(commonParent == null || commonParent == localParentTx
                        , "Expected all lodObjects to have the same parent object.", logScope, this);

                    commonParent = localParentTx;
                }
            }

            if (commonParent != null)
            {
                var gameObjectGroup = commonParent.gameObject.GetOrAddComponent<AvatarLODSkinnableGroup>();
                gameObjectGroup.GameObjects = children;
                AddLODGroup(gameObjectGroup);
            }

#if UNITY_EDITOR || UNITY_DEVELOPMENT
            // Register avatar with native runtime LOD scheme
            // Temporary for LOD editing bring up
            CAPI.ovrAvatar2LODRegistration reg;
            reg.avatarId = _avatarId;
            reg.lodWeights = vertexCounts.ToArray();
            reg.lodThreshold = _maxLodLevel;

            CAPI.ovrAvatar2LOD_RegisterAvatar(reg);
#endif // UNITY_EDITOR || UNITY_DEVELOPMENT
        }

        public AvatarLODActionGroup AddLODActionGroup(GameObject go, Action[] actions)
        {
            var actionLODGroup = go.GetOrAddComponent<AvatarLODActionGroup>();
            if (actions?.Length > 0)
            {
                actionLODGroup.Actions = new List<Action>(actions);
            }
            AddLODGroup(actionLODGroup);
            return actionLODGroup;
        }

        public AvatarLODActionGroup AddLODActionGroup(GameObject go, Action action, int levels)
        {
            var actions = new Action[levels];
            if (action != null)
            {
                for (int i = 0; i < levels; i++)
                {
                    actions[i] = action;
                }
            }

            return AddLODActionGroup(go, actions);
        }

        // Find a valid LOD near the requested one
        public int CalcAdjustedLod(int lod)
        {
            var adjustedLod = Mathf.Clamp(lod, minLodLevel, maxLodLevel);
            if (adjustedLod != -1 && vertexCounts[adjustedLod] == 0)
            {
                adjustedLod = GetNextLod(lod);
                if (adjustedLod == -1) { adjustedLod = GetPreviousLod(lod); }
            }
            return adjustedLod;
        }

        private int GetNextLod(int lod)
        {
            if (maxLodLevel >= 0)
            {
                for (int nextLod = lod + 1; nextLod <= maxLodLevel; ++nextLod)
                {
                    if (vertexCounts[nextLod] != 0) { return nextLod; }
                }
            }
            return -1;
        }

        internal int GetPreviousLod(int lod)
        {
            if (minLodLevel >= 0)
            {
                for (int prevLod = lod - 1; prevLod >= minLodLevel; --prevLod)
                {
                    if (vertexCounts[prevLod] != 0) { return prevLod; }
                }
            }
            return -1;
        }

        // Returns true when the entity is active and the LODs have been setup.
        public bool AreLodsActive()
        {
            return EntityActive && minLodLevel >= 0 && maxLodLevel >= 0;
        }

        public void Reset()
        {
            ResetXforms();
        }

        private void ResetXforms()
        {
            centerXform = transform;
            extraXforms.Clear();
        }

#if UNITY_EDITOR || UNITY_DEVELOPMENT
        // AvatarLODManager.Initialize doesn't run for all the avatars added
        // in LODScene so assign a unique id internally on construction.
        private static Int32 _avatarIdSource = default;

        // Temporary to bring up runtime LOD system
        // Unique ID for this avatar
        private Int32 _avatarId;

        /// Clock time since last update (in seconds).
        public float lastUpdateAgeSeconds;

        /// Total maximum age during previous two updates (in seconds).
        public float previousUpdateAgeWindowSeconds;

        private GameObject _debugCanvas;
#endif // UNITY_EDITOR || UNITY_DEVELOPMENT

        [Conditional("UNITY_DEVELOPMENT")]
        [Conditional("UNITY_EDITOR")]
        internal void TrackUpdateAge(float deltaTime)
        {
#if UNITY_EDITOR || UNITY_DEVELOPMENT
            // Track of the last update time for debug tools
            if (EntityActive)
            {
                previousUpdateAgeWindowSeconds = lastUpdateAgeSeconds + deltaTime;
                lastUpdateAgeSeconds = 0;
            }
            else { lastUpdateAgeSeconds += Time.deltaTime; }
#endif // UNITY_EDITOR || UNITY_DEVELOPMENT
        }

        [Conditional("UNITY_DEVELOPMENT")]
        [Conditional("UNITY_EDITOR")]
        public void UpdateDebugLabel()
        {
#if UNITY_EDITOR || UNITY_DEVELOPMENT
            if (AvatarLODManager.Instance.debug.displayLODLabels || AvatarLODManager.Instance.debug.displayAgeLabels)
            {
                if (_debugCanvas == null && AvatarLODManager.Instance.avatarLodDebugCanvas != null)
                {
                    GameObject
                        canvasPrefab
                            = AvatarLODManager.Instance
                                .avatarLodDebugCanvas; //LoadAssetWithFullPath<GameObject>($"{AvatarPaths.ASSET_SOURCE_PATH}/LOD/Prefabs/AVATAR_LOD_DEBUG_CANVAS.prefab");
                    if (canvasPrefab != null)
                    {
                        _debugCanvas = Instantiate(canvasPrefab, centerXform);

                        // Set position instead of localPosition to keep the label in a steady readable location.
                        _debugCanvas.transform.position = _debugCanvas.transform.parent.position +
                                                          AvatarLODManager.Instance.debug.displayLODLabelOffset;

                        _debugCanvas.SetActive(true);
                    }
                    else
                    {
                        OvrAvatarLog.LogWarning(
                            "DebugLOD will require the avatarLodDebugCanvas prefab to be specified. This has a simple UI card that allows for world space display of LOD.");
                    }
                }

                if (_debugCanvas != null)
                {
                    var text = _debugCanvas.GetComponentInChildren<UnityEngine.UI.Text>();

                    // Set position instead of localPosition to keep the label in a steady readable location.
                    _debugCanvas.transform.position = _debugCanvas.transform.parent.position +
                                                      AvatarLODManager.Instance.debug.displayLODLabelOffset;

                    if (AvatarLODManager.Instance.debug.displayLODLabels)
                    {
                        int actualLevel = overrideLOD ? overrideLevel : Level;
                        text.color = actualLevel == -1 ? Color.gray : AvatarLODManager.LOD_COLORS[actualLevel];
                        text.text = actualLevel.ToString();
                        text.fontSize = 40;
                    }

                    if (AvatarLODManager.Instance.debug.displayAgeLabels)
                    {
                        text.text = previousUpdateAgeWindowSeconds.ToString(CultureInfo.InvariantCulture);
                        text.color = new Color(
                            Math.Max(Math.Min(-1.0f + 2.0f * previousUpdateAgeWindowSeconds, 1.0f), 0.0f)
                            , Math.Max(
                                Math.Min(
                                    previousUpdateAgeWindowSeconds * 2.0f, 2.0f - 2.0f * previousUpdateAgeWindowSeconds)
                                , 0f), Math.Max(Math.Min(1.0f - 2.0f * previousUpdateAgeWindowSeconds, 1.0f), 0.0f));
                        text.fontSize = 10;
                    }
                }
            }
            else
            {
                if (_debugCanvas != null)
                {
                    _debugCanvas.SetActive(false);
                    _debugCanvas = null;
                }
            }
#endif // UNITY_EDITOR || UNITY_DEVELOPMENT
        }

        [Conditional("UNITY_DEVELOPMENT")]
        [Conditional("UNITY_EDITOR")]
        internal void ForceUpdateLOD<T>()
        {
            foreach (var lodGroup in _lodGroups)
            {
                if (lodGroup is T) { lodGroup.UpdateLODGroup(); }
            }
        }
    }
}
