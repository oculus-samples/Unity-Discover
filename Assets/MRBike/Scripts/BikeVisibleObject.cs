// Copyright (c) Meta Platforms, Inc. and affiliates.

using Fusion;
using Meta.Utilities;
using UnityEngine;

namespace MRBike
{
    public class BikeVisibleObject : NetworkBehaviour
    {
        [SerializeField] private int m_partNum;
        [SerializeField] private GameObject m_visiblePart;
        [SerializeField] private bool m_startVisible = false;
        // this is for self contain parts that contains colliders
        [SerializeField] private bool m_toggleSelfObject = true;

        // parts found on the visiblePart
        [SerializeField] private GrabObjectFollower m_rotator;
        [SerializeField] private BikeNetworkEvent m_networkEvent;
        [SerializeField] private ColorIndicator m_colorIndicator;

        [AutoSetFromParent]
        [SerializeField] private BikeObjectVisibilityManager m_visibilityManager;

        [Networked(OnChanged = nameof(OnVisibilityChanged))] private bool IsVisible { get; set; }
        [Networked(OnChanged = nameof(OnRotatorGrabbedChanged))] private bool RotatorGrabbed { get; set; }
        [Networked(OnChanged = nameof(OnAffordanceChanged))] private bool AffordanceActive { get; set; }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                IsVisible = m_startVisible;
            }

            OnVisibilityUpdate();
        }

        public static void OnVisibilityChanged(Changed<BikeVisibleObject> changed)
        {
            changed.Behaviour.OnVisibilityUpdate();
        }

        private void OnVisibilityUpdate()
        {
            if (m_toggleSelfObject)
            {
                // we enable/disable this component gameObject as some objects have colliders logic and needs to be sync
                gameObject.SetActive(IsVisible);
            }
            m_visiblePart.SetActive(IsVisible);
        }

        public static void OnRotatorGrabbedChanged(Changed<BikeVisibleObject> changed)
        {
            changed.Behaviour.OnRotatorGrabbedUpdated();
        }

        private void OnRotatorGrabbedUpdated()
        {
            if (m_rotator == null)
            {
                Debug.LogError($"rotator is null on {name}");
                m_rotator = m_visiblePart.GetComponent<GrabObjectFollower>();
            }
            if (m_rotator == null)
            {
                return;
            }

            if (RotatorGrabbed)
            {
                m_rotator.Grab();
            }
            else
            {
                m_rotator.Release();
            }
        }

        public static void OnAffordanceChanged(Changed<BikeVisibleObject> changed)
        {
            changed.Behaviour.OnAffordanceUpdate();
        }

        private void OnAffordanceUpdate()
        {
            if (m_colorIndicator == null)
            {
                Debug.LogError($"m_colorIndicator is null on {name}");
                m_colorIndicator = m_visiblePart.GetComponent<ColorIndicator>();
            }

            if (m_colorIndicator == null)
            {
                Debug.LogError($"m_colorIndicator is still null on {name}");
                return;
            }

            if (AffordanceActive)
            {
                m_colorIndicator.Activate();
            }
            else
            {
                m_colorIndicator.Deactivate();
            }
        }

        public void Show()
        {
            IsVisible = true;
            if (!HasStateAuthority)
            {
                ShowRPC();
            }
        }

        public void Hide()
        {
            IsVisible = false;
            if (!HasStateAuthority)
            {
                HideRPC();
            }
        }

        public void Show(int showPart)
        {
            m_visibilityManager.ShowNetworkObject(showPart);
        }

        public void Hide(int hidePart)
        {
            m_visibilityManager.HideNetworkObject(hidePart);
        }

        public void TriggerNetworkEvent(int partNum)
        {
            m_visibilityManager.SendNetworkTrigger(partNum);
        }

        public void Trigger(int triggerPart)
        {
            m_visibilityManager.SendNetworkTrigger(triggerPart);
        }

        public void Trigger()
        {
            OnTriggerRPC();
        }

        public void RotatorGrab()
        {
            RotatorGrabbed = true;
            if (!HasStateAuthority)
            {
                RotatorGrabRPC();
            }
        }

        public void RotatorGrab(int partNum)
        {

            m_visibilityManager.RotatorGrabNetworkObject(partNum);
        }

        public void RotatorRelease()
        {
            RotatorGrabbed = false;
            if (!HasStateAuthority)
            {
                RotatorReleaseRPC();
            }
        }
        public void RotatorRelease(int partNum)
        {
            m_visibilityManager.RotatorReleaseNetworkObject(partNum);
        }

        public void AffordanceActivate()
        {
            AffordanceActive = true;
            if (!HasStateAuthority)
            {
                AffordanceActivateRPC();
            }
        }

        public void AffordanceActivate(int effectNum)
        {
            m_visibilityManager.AffordanceActivate(effectNum);
        }

        public void AffordanceDeactivate()
        {
            AffordanceActive = false;
            if (!HasStateAuthority)
            {
                AffordanceDeactivateRPC();
            }
        }

        public void AffordanceDeactivate(int effectNum)
        {
            m_visibilityManager.AffordanceDeactivate(effectNum);
        }

        private void Awake()
        {
            if (m_visibilityManager == null)
            {
                m_visibilityManager = GetComponentInParent<BikeObjectVisibilityManager>();
            }

            Debug.Assert(m_visibilityManager != null,
                $"{nameof(BikeVisibleObject)} No {nameof(BikeObjectVisibilityManager)} found for {name}");
            m_visibilityManager.RegisterVisibleObject(this, m_partNum);
        }

        private void OnValidate()
        {
            if (m_toggleSelfObject)
            {
                var allBikeVis = GetComponentsInChildren<BikeVisibleObject>();
                if (allBikeVis.Length > 1)
                {
                    Debug.LogError($"{name} can't toggle self visibility because it" +
                                   $" has another BikeVisibleObject as a child");
                }
            }
        }

        #region RPCs

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void ShowRPC()
        {
            IsVisible = true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void HideRPC()
        {
            IsVisible = false;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RotatorGrabRPC()
        {
            RotatorGrabbed = true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RotatorReleaseRPC()
        {
            RotatorGrabbed = false;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void AffordanceActivateRPC()
        {
            AffordanceActive = true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void AffordanceDeactivateRPC()
        {
            AffordanceActive = false;
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void OnTriggerRPC()
        {
            if (m_networkEvent == null)
            {
                Debug.LogError($"m_networkEvent is null on {name}");
                m_networkEvent = m_visiblePart.GetComponent<BikeNetworkEvent>();
            }
            if (m_networkEvent != null)
            {
                m_networkEvent.OnEventActivate();
            }
        }

        #endregion
    }
}
