// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_AVATARS

using Oculus.Avatar2;
using UnityEngine;

namespace Meta.Utilities.Input
{
    public class XRInputTrackingDelegate : OvrAvatarInputTrackingDelegate
    {
        protected OVRCameraRig m_ovrCameraRig = null;
        protected bool m_controllersEnabled = false;

        public XRInputTrackingDelegate(OVRCameraRig ovrCameraRig, bool controllersEnabled)
        {
            m_ovrCameraRig = ovrCameraRig;
            m_controllersEnabled = controllersEnabled;
        }

        public override bool GetRawInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState)
        {
            inputTrackingState = default;
            inputTrackingState.headsetActive = true;
            inputTrackingState.leftControllerActive = m_controllersEnabled;
            inputTrackingState.rightControllerActive = m_controllersEnabled;
            inputTrackingState.leftControllerVisible = false;
            inputTrackingState.rightControllerVisible = false;
            inputTrackingState.headset = ConvertTransform(m_ovrCameraRig.trackingSpace, m_ovrCameraRig.centerEyeAnchor);
            inputTrackingState.leftController = ConvertTransform(m_ovrCameraRig.trackingSpace, m_ovrCameraRig.leftControllerAnchor);
            inputTrackingState.rightController = ConvertTransform(m_ovrCameraRig.trackingSpace, m_ovrCameraRig.rightControllerAnchor);
            return true;
        }

        private static CAPI.ovrAvatar2Transform ConvertTransform(Transform trackingSpace, Transform centerEyeAnchor)
        {
            var matrix = trackingSpace.worldToLocalMatrix * centerEyeAnchor.localToWorldMatrix;
            return new CAPI.ovrAvatar2Transform(
                matrix.GetPosition(),
                matrix.rotation,
                matrix.lossyScale
            );
        }
    }
}

#endif
