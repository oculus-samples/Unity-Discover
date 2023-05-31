// Copyright (c) Meta Platforms, Inc. and affiliates.



#pragma warning disable IDE1006

using Oculus.Avatar2;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Assertions;

namespace Oculus.Interaction.AvatarIntegration
{
    public class HandTrackingInputManager : OvrAvatarInputManager
    {
        [SerializeField, Interface(typeof(IHmd))]
        private MonoBehaviour _hmd;
        private IHmd Hmd;

        [SerializeField, Interface(typeof(IHand))]
        private MonoBehaviour _leftHand;
        private IHand LeftHand;

        [SerializeField, Interface(typeof(IHand))]
        private MonoBehaviour _rightHand;
        private IHand RightHand;

        private bool _setupBodyTracking = false;

        protected void Awake()
        {
            Hmd = _hmd as IHmd;
            LeftHand = _leftHand as IHand;
            RightHand = _rightHand as IHand;
        }

        protected virtual void Start()
        {
            Assert.IsNotNull(Hmd);
            Assert.IsNotNull(LeftHand);
            Assert.IsNotNull(RightHand);
        }

        private void Update()
        {
            if (!_setupBodyTracking)
            {
                if (BodyTracking == null)
                {
                    return;
                }

                BodyTracking.InputTrackingDelegate =
                    new HandTrackingInputTrackingDelegate(transform, LeftHand, RightHand, Hmd);
                BodyTracking.HandTrackingDelegate = new HandTrackingDelegate(transform, LeftHand, RightHand);
                _setupBodyTracking = true;
            }
        }

        #region Inject
        public void InjectAllHandTrackingInputManager(Hmd hmd, IHand leftHand, IHand rightHand)
        {
            InjectHmd(hmd);
            InjectLeftHand(leftHand);
            InjectRightHand(rightHand);
        }
        public void InjectHmd(IHmd hmd)
        {
            _hmd = hmd as MonoBehaviour;
            Hmd = hmd;
        }
        public void InjectLeftHand(IHand leftHand)
        {
            _leftHand = leftHand as MonoBehaviour;
            LeftHand = leftHand;
        }
        public void InjectRightHand(IHand rightHand)
        {
            _rightHand = rightHand as MonoBehaviour;
            RightHand = rightHand;
        }
        #endregion
    }
}
