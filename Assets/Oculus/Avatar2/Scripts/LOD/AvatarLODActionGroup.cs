using System;
using System.Collections.Generic;


namespace Oculus.Avatar2 {
  public class AvatarLODActionGroup : AvatarLODGroup {
    private List<Action> actions_ = new List<Action>();

    public Action outOfRangeAction = null;

    public List<Action> Actions {
      //Lambdas
      set {
        this.actions_ = value;
        count = actions_.Count;
        ResetLODGroup();
      }
    }

    public void AddAction(Action action) {
      this.actions_.Add(action);
      count = actions_.Count;
      ResetLODGroup();
    }

    public override void ResetLODGroup() {
      UpdateAdjustedLevel();
      UpdateLODGroup();
    }

    public override void UpdateLODGroup() {
      if (adjustedLevel_ == -1) {
        outOfRangeAction?.Invoke();
      } else if(adjustedLevel_ < actions_.Count) {
        actions_[adjustedLevel_]?.Invoke();
      }

      prevLevel_ = Level;
      prevAdjustedLevel_ = adjustedLevel_;
    }
  }
}
