using System;
using System.Collections.Generic;
using UnityEngine;


namespace Oculus.Avatar2 {
  public class AvatarLODSkinnableGroup : AvatarLODGroup {
    private const string logScope = "AvatarLODSkinnableGroup";
    private const int INVALID_LEVEL = -1;

    private GameObject[] _gameObjects = Array.Empty<GameObject>();

    private OvrAvatarSkinnedRenderable[][] _childRenderables = Array.Empty<OvrAvatarSkinnedRenderable[]>();
    private int _activeAndEnabledLevel = INVALID_LEVEL;

    private int _pendingTransitionLevel = INVALID_LEVEL;
    private readonly HashSet<OvrAvatarSkinnedRenderable> _pendingTransitionIncompleteRenderables =
      new HashSet<OvrAvatarSkinnedRenderable>();

    private byte _animatingLevels;
    internal override byte LevelsWithAnimationUpdateCost => _animatingLevels;

    private void FindAndCacheChildRenderables()
    {
      _childRenderables = new OvrAvatarSkinnedRenderable[count][];
      for (int i = 0; i < GameObjects.Length; i++) {
        var gameObj = GameObjects[i];
        if (gameObj != null) {
          _childRenderables[i] = gameObj.GetComponentsInChildren<OvrAvatarSkinnedRenderable>();
        } else {
          _childRenderables[i] = Array.Empty<OvrAvatarSkinnedRenderable>();
        }
      }
    }

    public GameObject[] GameObjects {
      get => _gameObjects;
      set {
        // Disable and pending transitions and stop listening for transition completion
        // This must be done first, since the renderables will be destroyed
        DisablePendingLevelRenderablesAnimationAndStopListening();
        _pendingTransitionLevel = INVALID_LEVEL;

        _gameObjects = value;
        count = GameObjects.Length;

        // Filter out the skinned renderables
        FindAndCacheChildRenderables();
        ResetLODGroup();
      }
    }

    public override void ResetLODGroup() {
      if (!Application.isPlaying) { return; }

      // Not 100% sure what "Reset" means in this context,
      // just using AvatarLODGameObjectGroup as an example

      // Disable and pending transitions and stop listening
      // for transition completion
      DisablePendingLevelRenderablesAnimationAndStopListening();
      _pendingTransitionLevel = INVALID_LEVEL;

      // Deactive all game objects
      foreach (GameObject t in GameObjects)
      {
        if(t == null) { continue; }
        t.SetActive(false);
      }
      _activeAndEnabledLevel = INVALID_LEVEL;

      // Stop all animations
      foreach (var renderablesForLevel in _childRenderables)
      {
        foreach (var r in renderablesForLevel)
        {
          r.IsAnimationEnabled = false;
        }
      }
      ClearAnimatingLevels();

      UpdateAdjustedLevel();
      UpdateLODGroup();
    }

    public override void UpdateLODGroup()
    {
      base.UpdateLODGroup();

      // If same level that is pending is requested again, do nothing
      bool isRequestedLevelAlreadyPending = adjustedLevel_ == _pendingTransitionLevel;
      if (isRequestedLevelAlreadyPending)
      {
        return;
      }

      // If the requested level is already the level that is "active and enabled" (visible)
      // then the pending transaction just needs to be cancelled
      bool isRequestedLevelAlreadyActive = _activeAndEnabledLevel == adjustedLevel_;
      if (isRequestedLevelAlreadyActive)
      {
        DisablePendingLevelRenderablesAnimationAndStopListening();
        _pendingTransitionLevel = adjustedLevel_;
      }
      else
      {
        // Transition "out of" previous LOD and into a new one (if applicable)
        if (adjustedLevel_ < GameObjects.Length)
        {
          DisablePendingLevelRenderablesAnimationAndStopListening();

          _pendingTransitionLevel = adjustedLevel_;

          EnableSkinnedRenderablesAnimationAndListenForCompletion();
        }
        else
        {
          OvrAvatarLog.LogWarning("adjustedLevel outside bounds of GameObjects array", logScope, this);
        }
      }
    }

    private bool IsTransitionComplete => _pendingTransitionIncompleteRenderables.Count == 0;

    private void DisablePendingLevelRenderablesAnimationAndStopListening()
    {
      // Disable animation for previously pending level (if it was valid)
      if (!IsTransitionComplete)
      {
        // Previously requested transition hasn't completed yet,
        // set all renderables to have animation disabled and stop listening for animation data completion
        RemoveFromAnimatingLevels(_pendingTransitionLevel);
        var renderables = _childRenderables[_pendingTransitionLevel];
        foreach (var r in renderables)
        {
          r.IsAnimationEnabled = false;
          r.AnimationDataComplete -= OnRenderableAnimDataComplete;
        }

        _pendingTransitionIncompleteRenderables.Clear();
      }
    }

    private void EnableSkinnedRenderablesAnimationAndListenForCompletion()
    {
      // Special case:
      // If the active level is "invalid" (i.e. nothing visible)
      // then no "transition" is required (just show something instead of potentially waiting some frames)
      bool needsTransition = IsLevelValid(_activeAndEnabledLevel);

      if (IsLevelValid(_pendingTransitionLevel))
      {
        AddToAnimatingLevels(_pendingTransitionLevel);
        var renderables = _childRenderables[_pendingTransitionLevel];
        foreach (var r in renderables)
        {
          r.IsAnimationEnabled = true;

          // See if need to listen for data completion or not
          if (!r.IsAnimationDataCompletelyValid && needsTransition)
          {
            _pendingTransitionIncompleteRenderables.Add(r);
            r.AnimationDataComplete += OnRenderableAnimDataComplete;
          }
        }
      }

      // Edge case here where all data is already completely valid and thus, the transition
      // is already complete
      if (IsTransitionComplete)
      {
        OnLevelTransitionCompleted();
      }
    }

    private void OnLevelTransitionCompleted()
    {
      // ASSUMPTION: The pending requests should never be completed
      // for the already active level (this should be caught upstream)
      Debug.Assert(_activeAndEnabledLevel != _pendingTransitionLevel);

      // Deactivate old game object and active new one.
      bool isOldLevelValid = IsLevelValid(_activeAndEnabledLevel);
      if (isOldLevelValid)
      {
        DeactiveGameObjectForLevelAndDisableAnimation(_activeAndEnabledLevel);
      }

      // Enable the new level
      if (IsLevelValid(_pendingTransitionLevel))
      {
        GameObjects[_pendingTransitionLevel].SetActive(true);
      }

      _activeAndEnabledLevel = _pendingTransitionLevel;
    }

    private void DeactiveGameObjectForLevelAndDisableAnimation(int level)
    {
      // ASSUMPTION: caller checks for level validity
      GameObjects[level].SetActive(false);

      // Disable animation for the skinned renderables as well
      RemoveFromAnimatingLevels(level);
      foreach (var r in _childRenderables[level])
      {
        r.IsAnimationEnabled = false;
      }
    }

    private void OnRenderableAnimDataComplete(OvrAvatarSkinnedRenderable sender)
    {
      _pendingTransitionIncompleteRenderables.Remove(sender);
      sender.AnimationDataComplete -= OnRenderableAnimDataComplete;

      // See if all renderers are complete
      if (IsTransitionComplete)
      {
        OnLevelTransitionCompleted();
      }
    }

    private void ClearAnimatingLevels()
    {
      _animatingLevels = 0;
    }

    private void AddToAnimatingLevels(int lev)
    {
      if (IsLevelValid(lev))
      {
        _animatingLevels |= (byte)(1 << lev);
      }
    }

    private void RemoveFromAnimatingLevels(int lev)
    {
      if (IsLevelValid(lev))
      {
        _animatingLevels &= (byte)~(1 << lev);
      }
    }

    private bool IsLevelValid(int level)
    {
      return level != INVALID_LEVEL && level >= 0 && level < GameObjects.Length;
    }

  } // end class AvatarLODSkinnableGroup
} // end namespace
