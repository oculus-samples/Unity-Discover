using System;
using UnityEngine;


namespace Oculus.Avatar2 {
  public class AvatarLODGameObjectGroup : AvatarLODGroup {
    private const string logScope = "AvatarLODGameObjectGroup";

    [SerializeField]
    private GameObject[] gameObjects_ = Array.Empty<GameObject>();

    public GameObject[] GameObjects {
      get { return this.gameObjects_; }
      set {
        this.gameObjects_ = value;
        count = GameObjects.Length;
        ResetLODGroup();
      }
    }

    public override void ResetLODGroup() {
      if (!Application.isPlaying) return;
      for (int i = 0; i < GameObjects.Length; i++) {
        if(GameObjects[i] == null) continue;
        GameObjects[i].SetActive(false);
      }

      UpdateAdjustedLevel();
      UpdateLODGroup();
    }

    public override void UpdateLODGroup() {
      if (prevAdjustedLevel_ >= 0)
      {
        if (prevAdjustedLevel_ < GameObjects.Length)
        {
          GameObjects[prevAdjustedLevel_]?.SetActive(false);
        }
        else
        {
          OvrAvatarLog.LogWarning("prevAdjustedLevel outside bounds of GameObjects array", logScope, this);
        }
      }

      if (adjustedLevel_ >= 0)
      {
        if (adjustedLevel_ < GameObjects.Length)
        {
          GameObjects[adjustedLevel_].SetActive(true);
        }
        else
        {
          OvrAvatarLog.LogWarning("adjustedLevel outside bounds of GameObjects array", logScope, this);
        }
      }

      prevLevel_ = Level;
      prevAdjustedLevel_ = adjustedLevel_;
    }
  }
}
