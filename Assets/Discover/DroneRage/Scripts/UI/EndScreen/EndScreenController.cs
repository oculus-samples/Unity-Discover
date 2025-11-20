// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Linq;
using System.Text;
using Discover.DroneRage.UI.WaveCompletionUI;
using Meta.Utilities;
using TMPro;
using UnityEngine;

namespace Discover.DroneRage.UI.EndScreen
{
    public class EndScreenController : Singleton<EndScreenController>
    {
        [SerializeField]
        private string m_gameWonText = "> V1CT0RY";

        [SerializeField]
        private string m_gameLostText = "> GAME.0VER";

        [SerializeField]
        private string m_wavesText = "Waves Survived";

        [SerializeField]
        private string m_killsText = "Drones Destroyed";

        [SerializeField]
        private string m_damageText = "Damage Dealt";

        [SerializeField]
        private string m_accuracyText = "Accuracy";


        [SerializeField]
        private CanvasGroup m_canvasGroup;

        [SerializeField]
        private float m_fadeInDelay = 3.0f;

        [SerializeField]
        private float m_fadeInTime = 0.5f;


        [SerializeField]
        private TMP_Text m_title;


        [SerializeField]
        private TMP_Text m_scoreText;


        [SerializeField]
        private float m_autoScrollDelay = 5.0f;

        [SerializeField]
        private float m_autoScrollTime = 5.0f;

        private int m_shownPlayerIndex = 0;

        private bool m_autoscroll = true;
        private float m_timeToScroll = 0.0f;

        private Coroutine m_screenFadeCoroutine;

        public event System.Action<bool> OnStateChanged;

        private void OnDisable()
        {
            StopScreenFade();
        }

        private new void OnDestroy()
        {
            StopScreenFade();

            base.OnDestroy();
        }

        private void Update()
        {
            if (m_autoscroll)
            {
                m_timeToScroll -= Time.deltaTime;
                if (m_timeToScroll <= 0)
                {
                    m_timeToScroll = m_autoScrollTime;
                    m_shownPlayerIndex = WrapIndex(m_shownPlayerIndex + 1, Player.Player.NumPlayers);
                    ShowStatisticsForPlayer(m_shownPlayerIndex);
                }
            }
        }

        public void ShowWinScreen()
        {
            m_title.text = m_gameWonText;
            ShowEndScreen();
        }

        public void ShowLoseScreen()
        {
            m_title.text = m_gameLostText;
            ShowEndScreen();
        }

        private void ShowEndScreen()
        {
            OnStateChanged?.Invoke(true);

            UpdateStatistics();

            gameObject.SetActive(true);

            StartScreenFade();
        }

        private void UpdateStatistics()
        {
            m_shownPlayerIndex = Player.Player.LocalPlayer.PlayerUid;
            m_timeToScroll = m_autoScrollTime + m_autoScrollDelay;
            m_autoscroll = true;

            ShowStatisticsForPlayer(m_shownPlayerIndex);
        }

        private void ShowStatisticsForPlayer(int playerIndex)
        {
            var player = Player.Player.Players.FirstOrDefault(p => p.PlayerUid == playerIndex);
            if(player == null) player = Player.Player.Players.First();
            var playerName = DiscoverPlayer.Get(player.Object.StateAuthority).PlayerName;

            var accuracy = (float)player.PlayerStats.CalculateAccuracy();
            if (accuracy < 0.01f)
            {
                accuracy = 0.2777f;
            }

            var sb = new StringBuilder()
                .AppendLine($"<b><size=52>{playerName}{(player == Player.Player.LocalPlayer ? " (You)" : "")}</size></b>")
                .AppendLine($"{player.PlayerStats.WavesSurvived} {m_wavesText}")
                .AppendLine($"{player.PlayerStats.EnemiesKilled} {m_killsText}")
                .AppendLine($"{Mathf.Ceil(player.PlayerStats.DamageDealt)} {m_damageText}")
                .AppendLine($"{accuracy:P2} {m_accuracyText}");
            m_scoreText.text = sb.ToString();
        }

        public void OnExitPressed()
        {
            NetworkApplicationManager.Instance.CloseApplication();
        }

        public void OnNextPressed()
        {
            m_shownPlayerIndex = WrapIndex(m_shownPlayerIndex + 1, Player.Player.NumPlayers);
            ShowStatisticsForPlayer(m_shownPlayerIndex);
            m_autoscroll = false;
            if (m_scoreText.TryGetComponent<TextTypewriterEffect>(out var typewriterEffect))
            {
                typewriterEffect.enabled = false;
            }
        }

        public void OnPrevPressed()
        {
            m_shownPlayerIndex = WrapIndex(m_shownPlayerIndex - 1, Player.Player.NumPlayers);
            ShowStatisticsForPlayer(m_shownPlayerIndex);
            m_autoscroll = false;
            if (m_scoreText.TryGetComponent<TextTypewriterEffect>(out var typewriterEffect))
            {
                typewriterEffect.enabled = false;
            }
        }

        private void StopScreenFade()
        {
            if (m_screenFadeCoroutine != null)
            {
                StopCoroutine(m_screenFadeCoroutine);
                m_screenFadeCoroutine = null;
                m_canvasGroup.alpha = 1;
            }
        }

        private void StartScreenFade()
        {
            StopScreenFade();
            m_screenFadeCoroutine = StartCoroutine(ScreenFadeSequence());
        }

        private IEnumerator ScreenFadeSequence()
        {
            m_canvasGroup.alpha = 0.0f;
            yield return new WaitForSeconds(m_fadeInDelay);

            var time = 0f;
            while (time < m_fadeInTime)
            {
                time += Time.deltaTime;
                var progress = Mathf.Clamp01(time / m_fadeInTime);
                var ease = 1 - (1 - progress) * (1 - progress); // outQuad
                var value = Mathf.Lerp(0, 1, ease);
                m_canvasGroup.alpha = value;
                yield return null;
            }
            m_canvasGroup.alpha = 1;
            m_screenFadeCoroutine = null;
        }

        private static int WrapIndex(int x, int m)
        {
            return (x % m + m) % m;
        }
    }
}
