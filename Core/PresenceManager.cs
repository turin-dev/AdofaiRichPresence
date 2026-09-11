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
        private readonly LevelImageUploader imageUploader;
        private GameMode lastSentMode = (GameMode)(-1);
        private bool debugLoggingEnabled;

        internal PresenceManager(UnityModManager.ModEntry.ModLogger logger) {
            this.logger = logger;
            sessionStart = DateTime.UtcNow;
            imageUploader = new LevelImageUploader(logger);
        }

        internal void Tick(Settings settings, float deltaTime) {
            debugLoggingEnabled = settings.DebugLogging;
            imageUploader.Tick();
            bool imageJustReady = imageUploader.ConsumeJustCompleted();

            EnsureClient(settings.DiscordApplicationId);
            if (client == null) {
                return;
            }
            client.Invoke();

            timeSinceLastUpdate += deltaTime;
            GameSnapshot snap = GameState.Capture();
            bool modeChanged = snap.Mode != lastSentMode;

            // Discord animates the progress bar client-side between our updates, so while
            // frozen (paused/dead) we resend more often to keep the drift small instead of
            // waiting out the normal interval.
            bool frozenMode = snap.Mode == GameMode.Paused || snap.Mode == GameMode.Dead;
            float effectiveInterval = frozenMode ? Math.Min(2f, settings.UpdateIntervalSeconds) : settings.UpdateIntervalSeconds;

            if (!modeChanged && !imageJustReady && timeSinceLastUpdate < effectiveInterval) {
                return;
            }
            timeSinceLastUpdate = 0f;
            lastSentMode = snap.Mode;

            if (settings.DebugLogging) {
                logger?.Log("[디버그] mode=" + snap.Mode + " | " + GameState.DebugState());
            }
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
                client.Logger = new DiscordRPC.Logging.ConsoleLogger(DiscordRPC.Logging.LogLevel.Warning);
                client.OnReady += (sender, e) => logger?.Log("Discord RPC 연결됨 (사용자: " + e.User.Username + ")");
                client.OnConnectionFailed += (sender, e) => logger?.Warning("Discord RPC 파이프 연결 실패 (Discord 클라이언트가 실행 중인지 확인하세요): " + e.FailedPipe);
                client.OnPresenceUpdate += (sender, e) => {
                    if (debugLoggingEnabled) {
                        logger?.Log("Discord Presence 전송됨: " + e.Presence?.Details + " / " + e.Presence?.State);
                    }
                };
                client.OnError += (sender, e) => logger?.Error("Discord RPC 오류 (" + e.Code + "): " + e.Message);
                client.Initialize();
                connectedApplicationId = applicationId;
                sessionStart = DateTime.UtcNow;
                if (debugLoggingEnabled) {
                    logger?.Log("Discord RPC 초기화 시도 (Application ID: " + applicationId + ")");
                }
            } catch (Exception e) {
                logger?.Error("Discord RPC 초기화 실패: " + e.Message);
                client = null;
                connectedApplicationId = null;
            }
        }

        private RichPresence BuildPresence(GameSnapshot snap, Settings settings) {
            string details;
            string state;
            bool inLevel = snap.Mode == GameMode.Playing || snap.Mode == GameMode.Paused || snap.Mode == GameMode.Dead;

            switch (snap.Mode) {
                case GameMode.Playing:
                case GameMode.Paused:
                case GameMode.Dead:
                    details = settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.LevelName)
                        ? Truncate(snap.LevelName + (string.IsNullOrEmpty(snap.Artist) ? "" : " - " + snap.Artist), 128)
                        : (snap.Mode == GameMode.Dead ? "죽음" : snap.Mode == GameMode.Paused ? "일시정지" : "플레이 중");
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
                Timestamps = BuildTimestamps(snap, inLevel),
                Type = settings.ShowAsListening ? ActivityType.Listening : ActivityType.Playing,
            };

            string largeImageKey = ImageKeyFor(snap.Mode, settings);
            if (inLevel && settings.ShowMapCoverImage) {
                string coverUrl = imageUploader.GetUrlFor(snap.PreviewImagePath, settings.CdnUploadUrl, settings.CdnUploadSecret);
                if (!string.IsNullOrEmpty(coverUrl)) {
                    largeImageKey = coverUrl;
                }
            }
            if (!string.IsNullOrEmpty(largeImageKey)) {
                string smallImageKey = SmallImageKeyFor(snap.Mode, settings);
                presence.Assets = new Assets {
                    LargeImageKey = largeImageKey,
                    LargeImageText = "A Dance of Fire and Ice",
                    SmallImageKey = string.IsNullOrEmpty(smallImageKey) ? null : smallImageKey,
                    SmallImageText = string.IsNullOrEmpty(smallImageKey) ? null : snap.Mode.ToString(),
                };
            }

            if (inLevel && !string.IsNullOrEmpty(snap.WorkshopId)) {
                presence.Buttons = new[] {
                    new Button {
                        Label = "워크샵에서 보기",
                        Url = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + snap.WorkshopId,
                    },
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

        private Timestamps BuildTimestamps(GameSnapshot snap, bool inLevel) {
            if (inLevel && snap.TotalSeconds > 0f) {
                DateTime start = DateTime.UtcNow.AddSeconds(-Math.Max(0f, snap.ElapsedSeconds));
                DateTime end = start.AddSeconds(snap.TotalSeconds);
                return new Timestamps(start, end);
            }
            return new Timestamps(sessionStart);
        }

        private string BuildStateLine(GameSnapshot snap, Settings settings) {
            var parts = new System.Collections.Generic.List<string>();

            if (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.Author)) {
                parts.Add("제작: " + snap.Author);
            }
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
            if (settings.ShowElapsedTime && snap.TotalSeconds > 0 && !settings.ShowAsListening) {
                parts.Add(FormatTime(snap.ElapsedSeconds) + " / " + FormatTime(snap.TotalSeconds));
            }
            if (settings.ShowModeState && snap.Mode == GameMode.Dead) {
                parts.Add("죽음");
            } else if (settings.ShowModeState && snap.Mode == GameMode.Paused) {
                parts.Add("일시정지");
            }

            return Truncate(string.Join("  |  ", parts.ToArray()), 128);
        }

        private static string ImageKeyFor(GameMode mode, Settings settings) {
            switch (mode) {
                case GameMode.Paused:
                case GameMode.Dead:
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

        private static string SmallImageKeyFor(GameMode mode, Settings settings) {
            switch (mode) {
                case GameMode.Dead:
                    return settings.SmallImageKeyDead;
                case GameMode.Paused:
                    return settings.SmallImageKeyPaused;
                case GameMode.Playing:
                    return settings.SmallImageKeyPlaying;
                default:
                    return "";
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
