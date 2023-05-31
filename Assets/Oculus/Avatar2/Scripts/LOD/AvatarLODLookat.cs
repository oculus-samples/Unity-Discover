using UnityEngine;

namespace Oculus.Avatar2.Utils {
  public class AvatarLODLookat : MonoBehaviour {
    [SerializeField]
    public Transform xform;
    [SerializeField]
    public UpVectorMode upVectorMode = UpVectorMode.WORLD_UP;
    [SerializeField]
    public Vector3 upVector = Vector3.up;
    [SerializeField]
    public GameObject upObject = null;
    private Transform camXform = null;

    public enum UpVectorMode {
      WORLD_UP, // World Pos Y  (Default)
      OBJECT_UP, // Vector from self to upObject
      OBJECT_AIM // Use upVector
    }

    protected virtual void Start() {
    }

    protected virtual void Update() {
      if (xform == null)
        xform = this.transform;

      if (AvatarLODManager.Instance.CurrentCamera == null)
        return;

      camXform = AvatarLODManager.Instance.CurrentCamera.transform;

      if (upVectorMode == UpVectorMode.WORLD_UP) {
        xform.rotation = Quaternion.LookRotation(
           camXform.position - xform.position,
          upVector);
      } else if (upVectorMode == UpVectorMode.OBJECT_UP) {
        if (upObject == null) {
          upObject = AvatarLODManager.Instance.CurrentCamera.gameObject;
        }
        xform.rotation = Quaternion.LookRotation(
            camXform.position - xform.position,
          upObject.transform.rotation * upVector);
      } else if (upObject != null) {
        // OBJECT_AIM
        xform.rotation = Quaternion.LookRotation(
            camXform.position - xform.position,
          xform.position - upObject.transform.position);
      }
    }
  }
}
