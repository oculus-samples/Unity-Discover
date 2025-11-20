// Copyright (c) Meta Platforms, Inc. and affiliates.

#if USING_XR_MANAGEMENT && (USING_XR_SDK_OCULUS || USING_XR_SDK_OPENXR) && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

#pragma warning disable IDE1006

#nullable enable
using System.Reflection;
using Meta.XR.Samples;
using Oculus.Avatar2;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Assertions;

namespace Oculus.Interaction.AvatarIntegration
{
    [MetaCodeSample("Discover")]
    public class HandTrackingInputManager : OvrAvatarInputManager
    {
#if USING_XR_SDK
        [SerializeField]
        private OVRCameraRig? _ovrCameraRig = null;
#endif
        
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
        
        public OvrAvatarBodyTrackingMode BodyTrackingMode
        {
            get => _bodyTrackingMode;
            set
            {
                _bodyTrackingMode = value;
                InitializeBodyTracking();
            }
        }

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

        protected override void OnTrackingInitialized()
        {
#if USING_XR_SDK
        // On Oculus SDK version >= v46 Eye tracking and Face tracking need to be explicitly started by the application
        // after permission has been requested.
        OvrPluginInvoke("StartFaceTracking");
        OvrPluginInvoke("StartEyeTracking");
#endif

            IOvrAvatarInputTrackingDelegate? inputTrackingDelegate = null;
#if USING_XR_SDK
            inputTrackingDelegate = new SampleInputTrackingDelegate(_ovrCameraRig);
#endif // USING_XR_SDK
            var inputControlDelegate = new SampleInputControlDelegate();

            _inputTrackingProvider = new OvrAvatarInputTrackingDelegatedProvider(inputTrackingDelegate);
            _inputControlProvider = new OvrAvatarInputControlDelegatedProvider(inputControlDelegate);
        }
#if USING_XR_SDK
        // We use reflection here so that there are not compiler errors when using Oculus SDK v45 or below.
        private static void OvrPluginInvoke(string method, params object[] args)
        {
            typeof(OVRPlugin).GetMethod(method, BindingFlags.Public | BindingFlags.Static)?.Invoke(null, args);
        }
#endif

        private void Update()
        {
            if (_setupBodyTracking)
            {
                return;
            }

            if (BodyTrackingContext == null)
            {
                return;
            }
            if (BodyTrackingContext is not OvrAvatarBodyTrackingContext ovrBodyTracking)
            {
                return;
            }
            ovrBodyTracking.InputTrackingDelegate = new HandTrackingInputTrackingDelegate(transform, LeftHand, RightHand, Hmd);
            ovrBodyTracking.HandTrackingDelegate = new HandTrackingDelegate(transform, LeftHand, RightHand);
            _setupBodyTracking = true;
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
