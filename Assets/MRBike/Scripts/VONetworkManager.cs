// Copyright (c) Meta Platforms, Inc. and affiliates.

using Fusion;
using UnityEngine;

namespace MRBike
{
    public class VONetworkManager : NetworkBehaviour
    {
        [SerializeField] private TaskVOPlayer m_taskVoPlayer;
        [SerializeField] private bool m_debug = false;

        public void PlayVO(int clipNum)
        {
            OnPlayVoiceOverRPC(clipNum);

            if (m_debug)
                Debug.Log($"[bike] -- VO RPC sent :: Task number {clipNum}");
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void OnPlayVoiceOverRPC(int clipNum)
        {
            m_taskVoPlayer.PlayOnce(clipNum);
        }
    }
}
