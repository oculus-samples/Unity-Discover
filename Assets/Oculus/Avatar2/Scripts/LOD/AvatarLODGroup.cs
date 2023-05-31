using System;
using System.Collections.Generic;
using UnityEngine;


namespace Oculus.Avatar2 {
  public class AvatarLODGroup : MonoBehaviour, IDisposable {
    protected int count;

    public AvatarLOD parentLOD = null;

    public bool remapInOrder = false;

    protected int level_ = -1;
    protected int prevLevel_ = -1;
    protected int adjustedLevel_ = -1;

    public int AdjustedLevel {
      get { return this.adjustedLevel_; }
    }

    protected int prevAdjustedLevel_ = -1;

    public int Level {
      get { return this.level_; }
      set {
        if (value == prevLevel_ && value == prevAdjustedLevel_)
            return;
        this.level_ = value;

        // Update if parentLOD previously did not have LODs loaded yet
        UpdateAdjustedLevel();
        if (adjustedLevel_ != prevAdjustedLevel_) {
          UpdateLODGroup();
        }
      }
    }

    protected virtual void Start() {
    }
    /*
    public virtual void Update() {

    }
    */

    protected virtual void OnDestroy() {
      Dispose();
    }

    public void Dispose() {
      if (parentLOD) { parentLOD.RemoveLODGroup(this); }
      parentLOD = null;
    }

    public virtual void ResetLODGroup() {
    }

    protected virtual void UpdateAdjustedLevel() {
      if (Level < 0 || !parentLOD) {
        adjustedLevel_ = -1;
        return;
      }

      adjustedLevel_ = parentLOD.CalcAdjustedLod(Level);
    }

    public virtual void UpdateLODGroup() {
      prevLevel_ = Level;
      prevAdjustedLevel_ = adjustedLevel_;
    }

    internal virtual byte LevelsWithAnimationUpdateCost => (byte) (1 << Level);
  }
}
