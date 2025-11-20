// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Utilities;
using Meta.Utilities.Avatars;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover
{
    [MetaCodeSample("Discover")]
    public class FollowAvatar : MonoBehaviour
    {
        [SerializeField, AutoSet] private AvatarEntity m_avatar;

        protected void Update()
        {
            var body = m_avatar.BodyTracking;
            if (body == null)
                return;

            body.transform.GetPositionAndRotation(out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
