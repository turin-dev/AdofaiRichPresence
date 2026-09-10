using System;
using DiscordRPC;
using UnityModManagerNet;

namespace AdofaiRichPresence.Core {
    internal class PresenceManager : IDisposable {
        private readonly UnityModManager.ModEntry.ModLogger logger;
        private DiscordRpcClient client;
        private string connectedApplicationId;
        private float timeSinceLastUpdate;
        private DateTime sessionStart;

        internal PresenceManager(UnityModManager.ModEntry.ModLogger logger) {
            this.logger = logger;
            sessionStart = DateTime.UtcNow;
        }

        internal void Tick(Settings settings, float deltaTime) {
            EnsureClient(settings.DiscordApplicationId);
            if (client == null) {
                return;
            }
            client.Invoke();

            timeSinceLastUpdate += deltaTime;
            if (timeSinceLastUpdate < settings.UpdateIntervalSeconds) {
                return;
            }
            timeSinceLastUpdate = 0f;

            GameSnapshot snap = GameState.Capture();
            client.SetPresence(BuildPresence(snap, settings));
        }

        private void EnsureClient(string applicationId) {
            if (string.IsNullOrEmpty(applicationId)) {
                if (client != null) {
                    client.Dispose();
                    client = null;
                    connectedApplicationId = null;
                }
                return;
            }
            if (client != null && connectedApplicationId == applicationId) {
                return;
            }
            client?.Dispose();
            try {
                client = new DiscordRpcClient(applicationId);
                client.Initialize();
                connectedApplicationId = applicationId;
                sessionStart = DateTime.UtcNow;
            } catch (Exception e) {
                logger?.Error("Discord RPC 초기화 실패: " + e.Message);
                client = null;
                connectedApplicationId = null;
            }
        }

        private RichPresence BuildPresence(GameSnapshot snap, Settings settings) {
            string details;
            string state;
            bool inLevel = snap.Mode == GameMode.Playing || snap.Mode == GameMode.Paused;

            switch (snap.Mode) {
                case GameMode.Playing:
                case GameMode.Paused:
                    details = settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.LevelName)
                        ? Truncate(snap.LevelName + (string.IsNullOrEmpty(snap.Artist) ? "" : " - " + snap.Artist), 128)
                        : (snap.Mode == GameMode.Paused ? "일시정지" : "플레이 중");
                    state = BuildStateLine(snap, settings);
                    break;
                case GameMode.Editor:
                    details = "레벨 에디터";
                    state = settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.LevelName)
                        ? Truncate(snap.LevelName, 128)
                        : "레벨 제작 중";
                    break;
                case GameMode.LevelSelect:
                    details = "레벨 선택 중";
                    state = "";
                    break;
                default:
                    details = "메인 메뉴";
                    state = "";
                    break;
            }

            RichPresence presence = new RichPresence {
                Details = string.IsNullOrEmpty(details) ? null : details,
                State = string.IsNullOrEmpty(state) ? null : state,
                Timestamps = new Timestamps(sessionStart),
            };

            string largeImageKey = ImageKeyFor(snap.Mode, settings);
            if (!string.IsNullOrEmpty(largeImageKey)) {
                presence.Assets = new Assets {
                    LargeImageKey = largeImageKey,
                    LargeImageText = "A Dance of Fire and Ice",
                    SmallImageKey = string.IsNullOrEmpty(settings.SmallImageKeyPlaying) ? null : settings.SmallImageKeyPlaying,
                };
            }

            if (inLevel && settings.ShowProgress && snap.TotalTiles > 0) {
                int percent = (int)Math.Round(Math.Min(1f, Math.Max(0f, snap.Progress)) * 100);
                presence.Party = new Party {
                    ID = "progress",
                    Max = 100,
                    Size = Math.Max(1, percent),
                };
            }

            return presence;
        }

        private string BuildStateLine(GameSnapshot snap, Settings settings) {
            var parts = new System.Collections.Generic.List<string>();

            if (settings.ShowProgress) {
                parts.Add((snap.Progress * 100f).ToString("0.0") + "%");
            }
            if (settings.ShowRemainingTiles && snap.TotalTiles > 0) {
                parts.Add("남은 " + snap.RemainingTiles + "/" + snap.TotalTiles + " 타일");
            }
            if (settings.ShowDifficulty && snap.Difficulty > 0) {
                parts.Add("난이도 " + snap.Difficulty + "/10");
            }
            if (settings.ShowBpm && snap.Bpm > 0) {
                parts.Add(Math.Round(snap.Bpm) + " BPM");
            }
            if (settings.ShowElapsedTime && snap.TotalSeconds > 0) {
                parts.Add(FormatTime(snap.ElapsedSeconds) + " / " + FormatTime(snap.TotalSeconds));
            }
            if (settings.ShowModeState && snap.Mode == GameMode.Paused) {
                parts.Add("일시정지");
            }

            return Truncate(string.Join("  |  ", parts.ToArray()), 128);
        }

        private static string ImageKeyFor(GameMode mode, Settings settings) {
            switch (mode) {
                case GameMode.Paused:
                    return !string.IsNullOrEmpty(settings.LargeImageKeyPaused) ? settings.LargeImageKeyPaused : settings.LargeImageKeyDefault;
                case GameMode.MainMenu:
                case GameMode.LevelSelect:
                    return !string.IsNullOrEmpty(settings.LargeImageKeyMenu) ? settings.LargeImageKeyMenu : settings.LargeImageKeyDefault;
                case GameMode.Editor:
                    return !string.IsNullOrEmpty(settings.LargeImageKeyEditor) ? settings.LargeImageKeyEditor : settings.LargeImageKeyDefault;
                default:
                    return settings.LargeImageKeyDefault;
            }
        }

        private static string FormatTime(float seconds) {
            if (seconds < 0f) {
                seconds = 0f;
            }
            int total = (int)seconds;
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        private static string Truncate(string s, int max) {
            if (string.IsNullOrEmpty(s)) {
                return s;
            }
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        public void Dispose() {
            client?.Dispose();
            client = null;
        }
    }
}
