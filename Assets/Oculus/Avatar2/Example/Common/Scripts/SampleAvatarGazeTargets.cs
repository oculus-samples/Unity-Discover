using Oculus.Avatar2;
using System.Collections;
using UnityEngine;

// When added to a SampleAvatarEntity, this creates gaze targets for this avatar's head and hands
[RequireComponent(typeof(SampleAvatarEntity))]
public class SampleAvatarGazeTargets : MonoBehaviour
{
    private static readonly CAPI.ovrAvatar2JointType HEAD_GAZE_TARGET_JNT = CAPI.ovrAvatar2JointType.Head;
    private static readonly CAPI.ovrAvatar2JointType LEFT_HAND_GAZE_TARGET_JNT = CAPI.ovrAvatar2JointType.LeftHandIndexProximal;
    private static readonly CAPI.ovrAvatar2JointType RIGHT_HAND_GAZE_TARGET_JNT = CAPI.ovrAvatar2JointType.RightHandIndexProximal;
    private SampleAvatarEntity _avatarEnt;

    protected IEnumerator Start()
    {
        _avatarEnt = GetComponent<SampleAvatarEntity>();
        yield return new WaitUntil(() => _avatarEnt.HasJoints);

        CreateGazeTarget("HeadGazeTarget", HEAD_GAZE_TARGET_JNT, CAPI.ovrAvatar2GazeTargetType.AvatarHead);
        CreateGazeTarget("LeftHandGazeTarget", LEFT_HAND_GAZE_TARGET_JNT, CAPI.ovrAvatar2GazeTargetType.AvatarHand);
        CreateGazeTarget("RightHandGazeTarget", RIGHT_HAND_GAZE_TARGET_JNT, CAPI.ovrAvatar2GazeTargetType.AvatarHand);
    }

    private void CreateGazeTarget(string gameObjectName, CAPI.ovrAvatar2JointType jointType, CAPI.ovrAvatar2GazeTargetType targetType)
    {
        Transform jointTransform = _avatarEnt.GetSkeletonTransform(jointType);
        if (jointTransform)
        {
            var gazeTargetObj = new GameObject(gameObjectName);
            var gazeTarget = gazeTargetObj.AddComponent<OvrAvatarGazeTarget>();
            gazeTarget.TargetType = targetType;
            gazeTargetObj.transform.SetParent(jointTransform, false);
        }
        else
        {
            OvrAvatarLog.LogError($"SampleAvatarGazeTargets: No joint transform found for {gameObjectName}");
        }
    }
}
