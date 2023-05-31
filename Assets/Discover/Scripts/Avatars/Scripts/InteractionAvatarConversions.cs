// Copyright (c) Meta Platforms, Inc. and affiliates.



#pragma warning disable IDE1006

using Oculus.Avatar2;
using UnityEngine;

namespace Oculus.Interaction.AvatarIntegration
{
    public class InteractionAvatarConversions
    {
        public static CAPI.ovrAvatar2Transform PoseToAvatarTransform(Pose pose)
        {
            return new CAPI.ovrAvatar2Transform(
                pose.position,
                pose.rotation
            );
        }

        public static CAPI.ovrAvatar2Transform PoseToAvatarTransformFlipZ(Pose pose)
        {
            var position =
                new CAPI.ovrAvatar2Vector3f
                {
                    x = pose.position.x,
                    y = pose.position.y,
                    z = -pose.position.z
                };

            var orientation =
                new CAPI.ovrAvatar2Quatf
                {
                    w = pose.rotation.w,
                    x = -pose.rotation.x,
                    y = -pose.rotation.y,
                    z = pose.rotation.z
                };

            return new CAPI.ovrAvatar2Transform(position, orientation);
        }

        public static CAPI.ovrAvatar2Quatf UnityToAvatarQuaternionFlipX(Quaternion quat)
        {
            return new CAPI.ovrAvatar2Quatf
            {
                w = quat.w,
                x = quat.x,
                y = -quat.y,
                z = -quat.z
            };
        }
    }
}
