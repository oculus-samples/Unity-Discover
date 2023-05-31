using Oculus.Avatar2;
using System.Collections;
using UnityEngine;

/* This class is an example of how to attach GameObjects to an avatar's critical joints. It retrieves all of a SampleAvatarEntity's
 * critical joints, and attache a cube primitive to each of them. As the avatar tracks body movement, the attached objects move with it.
 */
[RequireComponent(typeof(SampleAvatarEntity))]
public class SampleAvatarAttachments : MonoBehaviour
{
    private SampleAvatarEntity _avatarEnt;

    [SerializeField]
    private Vector3 AttachmentScale = new Vector3(0.1f, 0.1f, 0.1f);

    [SerializeField]
    private Color AttachmentColor = new Color(1.0f, 0.0f, 0.0f);

    protected IEnumerator Start()
    {
        _avatarEnt = GetComponent<SampleAvatarEntity>();
        yield return new WaitUntil(() => _avatarEnt.HasJoints);

        var criticalJoints = _avatarEnt.GetCriticalJoints();

        foreach (var jointType in criticalJoints)
        {
            Transform jointTransform = _avatarEnt.GetSkeletonTransform(jointType);

            if (!jointTransform)
            {
                OvrAvatarLog.LogError($"SampleAvatarAttachments: No joint transform found for {jointType} on {_avatarEnt.name} ");
                continue;
            }

            var attachmentObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            attachmentObj.transform.localScale = AttachmentScale;
            attachmentObj.GetComponent<Renderer>().material.color = AttachmentColor;
            attachmentObj.transform.SetParent(jointTransform, false);
        }
    }
}
