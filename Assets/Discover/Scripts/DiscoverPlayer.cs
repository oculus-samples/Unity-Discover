// Copyright (c) Meta Platforms, Inc. and affiliates.

using Cysharp.Threading.Tasks;
using Discover.Colocation;
using Discover.Networking;
using Discover.Utilities;
using Fusion;
using Meta.Utilities;
using Meta.Utilities.Avatars;
using Oculus.Avatar2;
using Photon.Voice.Fusion;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace Discover
{
    public class DiscoverPlayer : NetworkPlayerBehaviour<DiscoverPlayer>
    {
        [SerializeField, AutoSet] private AvatarEntity m_avatar;
        [SerializeField, AutoSet] private VoiceNetworkObject m_voiceNetObj;
        [SerializeField, AutoSet] private FollowAvatar m_followAvatar;

        [Networked(OnChanged = nameof(OnColocationGroupIdChanged))]
        private uint ColocationGroupId { get; set; } = uint.MaxValue;

        [Networked(OnChanged = nameof(OnPlayerNameChanged))]
        public string PlayerName { get; private set; }

        [Networked(OnChanged = nameof(OnProfilePicUrlChanged))]
        [Capacity(512)]
        private string ProfilePicUrl { get; set; }

        [Networked(OnChanged = nameof(OnIsMasterClientChanged))]
        private NetworkBool IsMasterClient { get; set; }

        [Networked(OnChanged = nameof(OnRemoteChanged))]
        public NetworkBool IsRemote { get; set; }
        
        [Networked]
        public ulong PlayerUID { get; private set; }

        [SerializeField] private UnityEvent<string> m_onPlayerNameChanged = new();
        [SerializeField] private UnityEvent<Sprite> m_onPlayerPicChanged = new();
        [SerializeField] private UnityEvent<bool> m_onIsMasterClientChanged = new();

        private bool m_isSpawned = false;

        protected void Awake()
        {
            DiscoverAppController.Instance.OnShowPlayerIdChanged += UpdatePlayerName;
            m_avatar.OnCreatedEvent.AddListener(_ => OnAvatarLoaded());
            m_avatar.OnDefaultAvatarLoadedEvent.AddListener(_ => OnAvatarLoaded());
            m_avatar.OnUserAvatarLoadedEvent.AddListener(_ => UpdateVisibility());
            DiscoverAppController.WhenInstantiated(c => c.OnHostMigrationOccured.AddListener(OnHostMigrationOccured));

            AvatarColocationManager.Instance.OnLocalPlayerColocationGroupUpdated += UpdateVisibility;
        }

        protected void OnDestroy()
        {
            if (DiscoverAppController.Instance != null)
            {
                DiscoverAppController.Instance.OnHostMigrationOccured.RemoveListener(OnHostMigrationOccured);
                DiscoverAppController.Instance.OnShowPlayerIdChanged -= UpdatePlayerName;
            }

            AvatarColocationManager.Instance.OnLocalPlayerColocationGroupUpdated -= UpdateVisibility;
        }

        private void OnHostMigrationOccured()
        {
            if (HasStateAuthority)
            {
                UpdateIsMasterClient();
            }
        }

        private void OnAvatarLoaded()
        {
            if (m_voiceNetObj.SpeakerInUse != null)
            {
                var head = m_avatar.GetJointTransform(CAPI.ovrAvatar2JointType.Head);
                m_voiceNetObj.SpeakerInUse.transform.SetParent(head, false);
            }

            UpdateVisibility();
        }

        private void UpdateIsMasterClient() => IsMasterClient = Runner.IsMasterClient();

        public static void OnIsMasterClientChanged(Changed<DiscoverPlayer> changed)
        {
            var player = changed.Behaviour;
            player.m_onIsMasterClientChanged?.Invoke(player.IsMasterClient);
        }

        public static void OnColocationGroupIdChanged(Changed<DiscoverPlayer> changed)
        {
            changed.Behaviour.UpdateVisibility();
        }

        public static void OnRemoteChanged(Changed<DiscoverPlayer> changed)
        {
            changed.Behaviour.UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            Assert.Check(m_avatar != null);

            m_followAvatar.enabled = HasStateAuthority;

            if (!m_isSpawned)
            {
                // invisible until spawned
                m_avatar.Hidden = true;
                if (m_voiceNetObj.SpeakerInUse != null)
                {
                    m_voiceNetObj.SpeakerInUse.enabled = false;
                }

                return;
            }
            if (!HasStateAuthority && (IsRemote || AvatarColocationManager.Instance.IsCurrentPlayerRemote))
            {
                // always show other avatars that are remote
                m_avatar.Hidden = false;
                if (m_voiceNetObj.SpeakerInUse != null)
                {
                    m_voiceNetObj.SpeakerInUse.enabled = true;
                }
            }
            else
            {
                var hideAvatar = HasStateAuthority || ColocationGroupId == uint.MaxValue ||
                                  IsPlayerColocated;
                m_avatar.Hidden = hideAvatar;
                if (m_voiceNetObj.SpeakerInUse != null)
                {
                    m_voiceNetObj.SpeakerInUse.enabled = !hideAvatar;
                }
            }
        }

        public static void OnPlayerNameChanged(Changed<DiscoverPlayer> changed)
        {
            changed.Behaviour.UpdatePlayerName();
        }

        public static void OnProfilePicUrlChanged(Changed<DiscoverPlayer> changed)
        {
            changed.LoadOld();
            var oldUrl = changed.Behaviour.ProfilePicUrl;

            changed.LoadNew();
            var newUrl = changed.Behaviour.ProfilePicUrl;

            if (oldUrl != newUrl)
            {
                changed.Behaviour.FetchProfilePic().Forget();
            }
        }

        private async UniTaskVoid FetchProfilePic()
        {
            if (string.IsNullOrEmpty(ProfilePicUrl))
                return;

            using var www = UnityWebRequestTexture.GetTexture(ProfilePicUrl);
            _ = await www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[DiscoverPlayer] Error downloading player profile pic ({www.result}): {www.error}", this);
                return;
            }

            var texture = DownloadHandlerTexture.GetContent(www);
            var sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), new(0.5f, 0.5f));
            m_onPlayerPicChanged?.Invoke(sprite);
        }

        public override async void Spawned()
        {
            base.Spawned();

            m_isSpawned = true;

            if (HasStateAuthority)
            {
                AvatarColocationManager.Instance.LocalPlayer = this;
                SetUpPlayerInfo();
                UpdateIsMasterClient();

                await UniTask.WaitUntil(() =>
                    ColocationDriverNetObj.Instance != null &&
                    PlayerUID != 0);
                
                await UniTask.WaitUntil(
                    () => PhotonNetworkData.Instance.GetPlayerWithPlayerId(PlayerUID) != null,
                    cancellationToken: PhotonNetworkData.Instance.GetCancellationTokenOnDestroy());
                ColocationGroupId = PhotonNetworkData.Instance.GetPlayerWithPlayerId(PlayerUID)?.colocationGroupId ?? uint.MaxValue;
                AvatarColocationManager.Instance.OnLocalPlayerColocationGroupUpdated?.Invoke();
            }
        }

        private void UpdatePlayerName()
        {
            if (DiscoverAppController.Instance.ShowPlayerId)
            {
                m_onPlayerNameChanged?.Invoke($"{Object.StateAuthority}");
            }
            else
            {
                m_onPlayerNameChanged?.Invoke(PlayerName);
            }
        }

        private async void SetUpPlayerInfo()
        {
            do
            {
                var user = await OculusPlatformUtils.GetLoggedInUser();
                if (user == null)
                    return;
                PlayerName = user.OculusID;
                ProfilePicUrl = user.ImageURL;
                PlayerUID = OculusPlatformUtils.GetUserDeviceGeneratedUid();
                await UniTask.Yield();
            } while (this != null && string.IsNullOrWhiteSpace(PlayerName));
        }

        public bool IsPlayerColocated
        {
            get
            {
                var localPlayer = AvatarColocationManager.Instance.LocalPlayer;
                var localGroup = localPlayer != null ? localPlayer.ColocationGroupId : uint.MaxValue;
                Debug.Log($"Avatar Colocation Group {ColocationGroupId} (Local {localGroup})", this);
                if (localGroup == uint.MaxValue)
                {
                    // assume colocated until local player has a colocation group
                    return true;
                }

                return ColocationGroupId == localGroup;
            }
        }
    }
}