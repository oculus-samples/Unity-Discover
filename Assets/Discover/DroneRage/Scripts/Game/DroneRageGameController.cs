// Copyright (c) Meta Platforms, Inc. and affiliates.

using Cysharp.Threading.Tasks;
using Discover.DroneRage.Bootstrapper;
using Discover.DroneRage.Enemies;
using Discover.DroneRage.UI.WaveCompletionUI;
using Discover.DroneRage.Weapons;
using Discover.Utilities;
using Fusion;
using Oculus.Interaction.Input;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace Discover.DroneRage.Game
{
    [DefaultExecutionOrder(-1)]
    public class DroneRageGameController : NetworkSingleton<DroneRageGameController>
    {
        public delegate void GameOverHandler(bool victory);

        public event GameOverHandler OnGameOver;

        public struct GameOverData : INetworkStruct
        {
            public bool GameOver;
            public bool IsVictory;
        }

        [Networked(OnChanged = nameof(OnGameOverStateChanged))]
        public GameOverData GameOverState { get; set; }


        [SerializeField]
        private WaveCompletionUIController m_waveCompleteUI;

        [SerializeField] private Player.Player m_playerPrefab;
        [SerializeField] private NetworkedWeaponController m_leftGunPrefab;
        [SerializeField] private NetworkedWeaponController m_rightGunPrefab;

        [Networked]
        public int CurrentWave { get; private set; } = 0;

        protected override void InternalAwake()
        {
            Assert.IsNotNull(m_waveCompleteUI, $"{m_waveCompleteUI} cannot be null.");
        }

        private new void OnEnable()
        {
            base.OnEnable();
            Spawner.WhenInstantiated(s => s.OnWaveAdvance += OnWaveAdvance);
        }

        private void OnDisable()
        {
            if (Spawner.Instance != null)
                Spawner.Instance.OnWaveAdvance -= OnWaveAdvance;
        }

        private static void OnGameOverStateChanged(Changed<DroneRageGameController> changed)
        {
            Debug.Log("OnGameOverChanged called!", changed.Behaviour);
            if (changed.Behaviour.GameOverState.GameOver)
            {
                changed.Behaviour.OnGameOver?.Invoke(changed.Behaviour.GameOverState.IsVictory);
            }
        }

        private void OnWaveAdvance()
        {
            if (!HasStateAuthority)
                return;

            ++CurrentWave;
            if (CurrentWave > 0)
            {
                OnWaveAdvanceClientRPC(CurrentWave);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void OnWaveAdvanceClientRPC(int wave)
        {
            Debug.Log($"ShowWaveCompletedUIClientRPC called, {nameof(wave)} = {wave}");
            m_waveCompleteUI.gameObject.SetActive(true);
            m_waveCompleteUI.ShowWaveCompleteUI(wave);
            Player.Player.LocalPlayer.OnWaveAdvance();
        }

        public override void Spawned()
        {
            base.Spawned();
            InitPlayer(Runner.LocalPlayer);
        }

        private async void InitPlayer(PlayerRef targetPlayer)
        {
            Debug.Log($"InitPlayer - {targetPlayer.PlayerId}");

            await UniTask.WaitUntil(() => AppContainer != null, cancellationToken: this.GetCancellationTokenOnDestroy());

            var playerPrefabTransform = m_playerPrefab.transform;
            var player = AppContainer.NetInstantiate(m_playerPrefab, playerPrefabTransform.localPosition, playerPrefabTransform.localRotation);

            var leftGun = SpawnWeapon(targetPlayer, m_leftGunPrefab, player);
            var rightGun = SpawnWeapon(targetPlayer, m_rightGunPrefab, player);

            player.SetupPlayer();
            leftGun.EquipWeapon(Handedness.Left, player);
            rightGun.EquipWeapon(Handedness.Right, player);
        }

        private static NetworkApplicationContainer AppContainer => DroneRageAppContainerUtils.GetAppContainer();

        private static NetworkedWeaponController SpawnWeapon(PlayerRef owner, NetworkedWeaponController gunPrefab, Player.Player player)
        {
            var gunPrefabTransform = gunPrefab.transform;
            var gun = AppContainer.NetInstantiate(gunPrefab, gunPrefabTransform.localPosition, gunPrefabTransform.localRotation);

            var weapon = gun.ControlledWeapon;
            weapon.WeaponFired += player.OnWeaponFired;

            var pUpC = weapon.PowerUpCollector;
            if (pUpC != null)
            {
                pUpC.Player = player;
            }

            return gun;
        }

        public void TriggerGameOver(bool victory)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            GameOverState = new() { GameOver = true, IsVictory = victory };
        }


        [ContextMenu("Trigger Victory")]
        private void DebugTriggerVictory() => TriggerGameOver(true);

        [ContextMenu("Trigger Loss")]
        private void DebugTriggerLoss() => TriggerGameOver(false);

        [ContextMenu("Destroy All Enemies")]
        private void DebugDestroyAllEnemies()
        {
            foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                enemy.DestroySelf(false);
        }

        [ContextMenu("Advance Wave")]
        private async void DebugAdvanceWave()
        {
            var wave = CurrentWave;
            for (var i = 0; i != 128; ++i)
            {
                DebugDestroyAllEnemies();
                await UniTask.Delay(250);

                if (wave != CurrentWave)
                    return;
            }
        }
    }
}