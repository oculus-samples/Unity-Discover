// Copyright (c) Meta Platforms, Inc. and affiliates.

using Fusion;
using Meta.XR.Samples;
using TMPro;
using UnityEngine;

namespace Discover.UI.Taskbar
{
    [MetaCodeSample("Discover")]
    public class RoomNameUpdater : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_text;

        private void OnEnable()
        {
            if (NetworkRunner.Instances is { Count: > 0 })
            {
                var roomName = NetworkRunner.Instances[0].SessionInfo.Name;
                m_text.text = $"Room: {roomName}";
            }
            else
            {
                m_text.text = $"Room: N/A";
            }
        }
    }
}