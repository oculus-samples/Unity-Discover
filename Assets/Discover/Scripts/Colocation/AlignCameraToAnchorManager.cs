// Copyright (c) Meta Platforms, Inc. and affiliates.

using com.meta.xr.colocation;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Discover.Colocation
{
    /// <summary>
    ///     Manages the AlignCameraToAnchor to realign when user recenter
    /// </summary>
    public class AlignCameraToAnchorManager : MonoBehaviour
    {
        public AlignCameraToAnchor CameraAlignmentBehaviour { get; set; }

        private void OnEnable()
        {
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose += RealignToAnchor;
            }
            OVRManager.HMDMounted += RealignToAnchor;
        }

        private void OnDisable()
        {
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose -= RealignToAnchor;
            }
            OVRManager.HMDMounted -= RealignToAnchor;
        }

        [ContextMenu("Realign")]
        public async void RealignToAnchor()
        {
#if UNITY_EDITOR
            // When using Link there is a delay between the recenter or the HDMMount event and the anchor being updated
            // We need to add a delay to ensure to align after the anchor changed.
            await UniTask.Delay(1000);
#endif
            if (CameraAlignmentBehaviour != null)
            {
                CameraAlignmentBehaviour.RealignToAnchor();
            }
            else
            {
                Debug.LogError($"[{typeof(AlignCameraToAnchorManager)}] CameraAlignmentBehaviour is null");
            }
        }
    }
}