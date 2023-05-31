using UnityEngine;

namespace Oculus.Avatar2 {
  public class AvatarLODParent : MonoBehaviour {
    public bool beingDestroyed;  // Because Destroy doesn't happend until end of frame

    private void ResetLODChildrenParentState() {
      if (beingDestroyed) {
        return;
      }
      foreach (Transform child in transform) {
        AvatarLOD lod = child.GetComponent<AvatarLOD>();
        if (lod) {
          AvatarLODManager.ParentStateChanged(lod);
        }
      }
    }

    public void DestroyIfOnlyLODChild(AvatarLOD inLod) {
      if (beingDestroyed) {
        return;
      }
      bool lodFound = false;
      foreach (Transform child in transform) {
        AvatarLOD lod = child.GetComponent<AvatarLOD>();
        if (lod && lod != inLod) {
          lodFound = true;
          break;
        }
      }

      if (!lodFound) {
        beingDestroyed = true;  // Need this status immediately, not end of frame
        Destroy(this);  // Can't use DestroyImmediate in build
      }
    }

    protected virtual void OnEnable() {
      ResetLODChildrenParentState();
    }

    protected virtual void OnDisable() {
      ResetLODChildrenParentState();
    }
    
  }
}
