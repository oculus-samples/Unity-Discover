// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using com.meta.xr.colocation;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Oculus.Platform.Models;
using UnityEngine;

namespace Discover.Colocation
{
    public class ColocationDriverNetObj : NetworkBehaviour
    {
        public static ColocationDriverNetObj Instance { get; private set; }

        public static Action<bool> OnColocationCompletedCallback;

        public static bool SkipColocation;

        [SerializeField] private PhotonNetworkData m_networkData;
        [SerializeField] private PhotonNetworkMessenger m_networkMessenger;
        [SerializeField] private GameObject m_anchorPrefab;

        private SharedAnchorManager m_sharedAnchorManager;
        private AutomaticColocationLauncher m_colocationLauncher;
        private User m_oculusUser;
        private ulong m_playerDeviceUid;
        
        private Transform m_ovrCameraRigTransform;

        private void Awake()
        {
            Debug.Assert(Instance == null, $"{nameof(ColocationDriverNetObj)} instance already exists");
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void Spawned()
        {
            Init();
        }

        private async void Init()
        {
            m_ovrCameraRigTransform = FindObjectOfType<OVRCameraRig>().transform;
            m_oculusUser = await OculusPlatformUtils.GetLoggedInUser();
            m_playerDeviceUid = OculusPlatformUtils.GetUserDeviceGeneratedUid();
            SetupForColocation();
        }

        private void SetupForColocation()
        {
            Debug.Log("SetUpAndStartColocation: Initialize messenger");
            m_networkMessenger.RegisterLocalPlayer(m_playerDeviceUid);

            // Instantiates the manager for the Oculus shared anchors, specifying the desired anchor prefab.
            Debug.Log("SetupForColocation: Instantiating shared anchor manager");
            m_sharedAnchorManager = new SharedAnchorManager { AnchorPrefab = m_anchorPrefab };

            NetworkAdapter.SetConfig(m_networkData, m_networkMessenger);

            Debug.Log("SetupForColocation: Initializing Colocation for the player");
            
            // Starts the colocation alignment process
            m_colocationLauncher = new AutomaticColocationLauncher();
            m_colocationLauncher.Init(
                NetworkAdapter.NetworkData,
                NetworkAdapter.NetworkMessenger,
                m_sharedAnchorManager,
                m_ovrCameraRigTransform.gameObject,
                m_playerDeviceUid,
                m_oculusUser?.ID ?? default
            );
            
            // Hooks the event to react to the colocation ready state
            m_colocationLauncher.ColocationReady += OnColocationReady;
            m_colocationLauncher.ColocationFailed += OnColocationFailed;
            
            if (HasStateAuthority)
            {
                m_colocationLauncher.CreateColocatedSpace();
            }
            else
            {
                // Don't try to colocate if we join remotely
                if (SkipColocation)
                {
                    OnColocationCompletedCallback?.Invoke(false);
                }
                else
                {
                    m_colocationLauncher.ColocateAutomatically();
                }
            }
        }

        private void OnColocationReady()
        {
            Debug.Log("Colocation is Ready!");
            
            // The AlignCameraToAnchor scripts updates on every frame which messes up Physics and create frame spikes.
            // We need to disable it and add our own align manager that is applied only on recenter
            var alignCamBehaviour = FindObjectOfType<AlignCameraToAnchor>();
            if (alignCamBehaviour != null)
            {
                alignCamBehaviour.enabled = false;
                var alignmentGameObject = alignCamBehaviour.gameObject;
                var alignManager = alignmentGameObject.AddComponent<AlignCameraToAnchorManager>();
                alignManager.CameraAlignmentBehaviour = alignCamBehaviour;
                alignManager.RealignToAnchor();
            }

            OnColocationCompletedCallback?.Invoke(true);
        }

        private void OnColocationFailed(ColocationFailedReason reason)
        {
            Debug.Log($"Colocation failed! ({reason})");
            OnColocationCompletedCallback?.Invoke(false);
        }
    }
}