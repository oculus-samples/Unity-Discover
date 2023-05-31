// Copyright (c) Meta Platforms, Inc. and affiliates.


using System.Text;
using Discover.Networking;
using Fusion;
using TMPro;
using UnityEngine;

namespace Discover.Menus
{
    public class InfoWindow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_text;

        private StringBuilder m_stringBuilder;

        private void Awake()
        {
            Debug.Log($"[InfoWindow] Awake");
            m_stringBuilder = new StringBuilder();
        }

        public void OnEnable()
        {
            Debug.Log($"[InfoWindow] OnEnable");
            SetupText();
        }

        public void SetupText()
        {
            Debug.Log($"[InfoWindow] setting up text");
            m_stringBuilder ??= new StringBuilder();

            _ = m_stringBuilder.Clear();


            _ = m_stringBuilder.Append($"<b>App Version</b>: {Application.version} \n");

            _ = m_stringBuilder.Append($"<b>Photon Room Name</b>: ");

            string roomName = null;
            string region = null;
            if (NetworkRunner.Instances != null && NetworkRunner.Instances.Count > 0)
            {
                roomName = NetworkRunner.Instances[0].SessionInfo.Name;
                var regionCode = NetworkRunner.Instances[0].SessionInfo.Region;
                region = RegionMapping.CodeToName(regionCode);
            }
            _ = m_stringBuilder.Append(
              string.IsNullOrWhiteSpace(roomName) ? "Not Connected to Photon \n" : $"{roomName} \n"
            );

            if (!string.IsNullOrEmpty(region))
            {
                _ = m_stringBuilder.Append($"<b>Photon Region</b>: {region}\n");
            }

            _ = m_stringBuilder.Append($"<b>Hardware Type</b>: {OVRPlugin.GetSystemHeadsetType()}\n");

            _ = m_stringBuilder.Append($"<b>OVR Plugin version</b>: {OVRPlugin.version}\n");

            Debug.Log($"[InfoWindow] info text is {m_stringBuilder}");
            m_text.text = m_stringBuilder.ToString();
        }
    }
}
