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
        private string failedApplicationId;
        private DateTime nextClientAttemptUtc;
        private bool connectionReady;
        private bool connectionFailed;
        private string lastConnectionError;

        internal PresenceManager(UnityModManager.ModEntry.ModLogger logger) {
            this.logger = logger;
            sessionStart = DateTime.UtcNow;
            imageUploader = new LevelImageUploader(logger);
        }

        internal string GetConnectionStatus(Settings settings) {
            if (settings == null || !settings.EnableDiscord) {
                return "사용 안 함";
            }
            if (connectionReady) {
                return "연결됨";
            }
            if (!string.IsNullOrEmpty(lastConnectionError)) {
                return "오류: " + Truncate(lastConnectionError, 90);
            }
            return client == null ? "연결 대기 중" : "연결 시도 중";
        }

        internal void RequestReconnect() {
            failedApplicationId = null;
            nextClientAttemptUtc = DateTime.MinValue;
            connectionFailed = false;
            lastConnectionError = null;
            DisposeClient();
        }

        internal void Tick(Settings settings, float deltaTime) {
            if (settings == null || !settings.EnableDiscord) {
                Stop();
                return;
            }

            debugLoggingEnabled = settings.DebugLogging;
            imageUploader.Tick();
            bool imageJustReady = imageUploader.ConsumeJustCompleted();

            EnsureClient(settings.DiscordApplicationId);
            if (client == null) {
                return;
            }
            try {
                client.Invoke();
            } catch (Exception e) {
                HandleClientFailure("Discord RPC 호출 실패: " + e.Message);
                return;
            }
            if (connectionFailed) {
                HandleClientFailure(lastConnectionError ?? "Discord RPC 연결이 끊겼습니다.");
                return;
            }

            if (!float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime) && deltaTime > 0f) {
                timeSinceLastUpdate += deltaTime;
            }
            GameSnapshot snap = GameState.Capture();
            bool modeChanged = snap.Mode != lastSentMode;

            // Discord animates the progress bar client-side between our updates, so while
            // frozen (paused/dead) we resend more often to keep the drift small instead of
            // waiting out the normal interval.
            bool frozenMode = snap.Mode == GameMode.Paused || snap.Mode == GameMode.Dead;
            float updateInterval = settings.UpdateIntervalSeconds;
            if (float.IsNaN(updateInterval) || float.IsInfinity(updateInterval)) {
                updateInterval = 3f;
            }
            updateInterval = Math.Max(1f, Math.Min(15f, updateInterval));
            float effectiveInterval = frozenMode ? Math.Min(2f, updateInterval) : updateInterval;

            if (!modeChanged && !imageJustReady && timeSinceLastUpdate < effectiveInterval) {
                return;
            }
            timeSinceLastUpdate = 0f;
            lastSentMode = snap.Mode;

            if (settings.DebugLogging) {
                logger?.Log("[디버그] mode=" + snap.Mode + " | " + GameState.DebugState());
            }
            try {
                client.SetPresence(BuildPresence(snap, settings));
            } catch (Exception e) {
                HandleClientFailure("Discord 상태 전송 실패: " + e.Message);
            }
        }

        private void EnsureClient(string applicationId) {
            applicationId = applicationId == null ? "" : applicationId.Trim();
            if (string.IsNullOrEmpty(applicationId)) {
                DisposeClient();
                failedApplicationId = null;
                lastConnectionError = null;
                return;
            }
            if (!ulong.TryParse(applicationId, out _)) {
                lastConnectionError = "Application ID는 숫자만 입력해야 합니다.";
                if (failedApplicationId != applicationId || DateTime.UtcNow >= nextClientAttemptUtc) {
                    logger?.Warning("Discord Application ID가 숫자가 아닙니다.");
                    failedApplicationId = applicationId;
                    nextClientAttemptUtc = DateTime.UtcNow.AddSeconds(30);
                }
                DisposeClient();
                return;
            }
            if (client != null && connectedApplicationId == applicationId) {
                return;
            }
            if (failedApplicationId == applicationId && DateTime.UtcNow < nextClientAttemptUtc) {
                return;
            }
            DisposeClient();
            try {
                client = new DiscordRpcClient(applicationId);
                client.Logger = new DiscordRPC.Logging.ConsoleLogger(DiscordRPC.Logging.LogLevel.Warning);
                client.OnReady += (sender, e) => {
                    connectionReady = true;
                    connectionFailed = false;
                    lastConnectionError = null;
                    logger?.Log("Discord RPC 연결됨 (사용자: " + e.User.Username + ")");
                };
                client.OnConnectionFailed += (sender, e) => {
                    connectionReady = false;
                    connectionFailed = true;
                    lastConnectionError = "Discord RPC 파이프 연결 실패 (파이프 " + e.FailedPipe + ")";
                    logger?.Warning(lastConnectionError);
                };
                client.OnPresenceUpdate += (sender, e) => {
                    if (debugLoggingEnabled) {
                        logger?.Log("Discord Presence 전송됨: " + e.Presence?.Details + " / " + e.Presence?.State);
                    }
                };
                client.OnError += (sender, e) => {
                    lastConnectionError = "Discord RPC 오류 (" + e.Code + "): " + e.Message;
                    logger?.Error(lastConnectionError);
                };
                client.Initialize();
                connectedApplicationId = applicationId;
                failedApplicationId = null;
                connectionReady = false;
                connectionFailed = false;
                lastConnectionError = null;
                sessionStart = DateTime.UtcNow;
                if (debugLoggingEnabled) {
                    logger?.Log("Discord RPC 초기화 시도 (Application ID: " + applicationId + ")");
                }
            } catch (Exception e) {
                logger?.Error("Discord RPC 초기화 실패: " + e.Message);
                DisposeClient();
                failedApplicationId = applicationId;
                nextClientAttemptUtc = DateTime.UtcNow.AddSeconds(30);
                lastConnectionError = e.Message;
            }
        }

        private void HandleClientFailure(string message) {
            string applicationId = connectedApplicationId;
            lastConnectionError = message;
            connectionReady = false;
            connectionFailed = false;
            DisposeClient();
            if (!string.IsNullOrEmpty(applicationId)) {
                failedApplicationId = applicationId;
                nextClientAttemptUtc = DateTime.UtcNow.AddSeconds(30);
            }
            logger?.Warning(message);
        }

        private void DisposeClient() {
            if (client == null) {
                connectedApplicationId = null;
                connectionReady = false;
                connectionFailed = false;
                return;
            }
            try {
                client.ClearPresence();
            } catch (Exception e) {
                if (debugLoggingEnabled) {
                    logger?.Warning("Discord 상태 정리 실패: " + e.Message);
                }
            }
            client.Dispose();
            client = null;
            connectedApplicationId = null;
            connectionReady = false;
            connectionFailed = false;
            lastSentMode = (GameMode)(-1);
            timeSinceLastUpdate = 0f;
        }

        private RichPresence BuildPresence(GameSnapshot snap, Settings settings) {
            string details;
            string state;
            bool inLevel = snap.Mode == GameMode.Playing || snap.Mode == GameMode.Paused || snap.Mode == GameMode.Dead;
            bool hasLevelContext = inLevel || snap.Mode == GameMode.Editor || snap.Mode == GameMode.Cleared;

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
                    details = "레벨 에디터: " + (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.LevelName)
                        ? Truncate(snap.LevelName + (string.IsNullOrEmpty(snap.Artist) ? "" : " - " + snap.Artist), 110)
                        : "제작 중");
                    state = BuildEditorStateLine(snap, settings);
                    break;
                case GameMode.Cleared:
                    details = "클리어: " + (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.LevelName)
                        ? Truncate(snap.LevelName + (string.IsNullOrEmpty(snap.Artist) ? "" : " - " + snap.Artist), 110)
                        : "완료!");
                    state = settings.ShowDetailedResult ? BuildResultStateLine(snap, settings) : "";
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
                Timestamps = BuildTimestamps(snap, snap.Mode == GameMode.Playing),
                Type = settings.ShowAsListening ? ActivityType.Listening : ActivityType.Playing,
            };

            string largeImageKey = ImageKeyFor(snap.Mode, settings);
            if (hasLevelContext && settings.ShowMapCoverImage) {
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

            if (hasLevelContext) {
                var buttons = new System.Collections.Generic.List<Button>();
                if (!string.IsNullOrEmpty(snap.WorkshopId)) {
                    buttons.Add(new Button {
                        Label = "워크샵에서 보기",
                        Url = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + snap.WorkshopId,
                    });
                }
                if (settings.ShowModDownloadButton) {
                    buttons.Add(new Button {
                        Label = "이 모드 받기",
                        Url = "https://github.com/turin-dev/AdofaiRichPresence",
                    });
                }
                if (buttons.Count > 0) {
                    presence.Buttons = buttons.ToArray();
                }
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

        private string BuildResultStateLine(GameSnapshot snap, Settings settings) {
            var parts = new System.Collections.Generic.List<string>();

            if (settings.ShowAccuracy) {
                parts.Add("정확도 " + (snap.Accuracy * 100f).ToString("0.00") + "%");
            }
            if (settings.ShowXAccuracy) {
                parts.Add("X-정확도 " + (snap.XAccuracy * 100f).ToString("0.00") + "%");
            }
            parts.Add("정확 " + snap.PerfectCount + "  빠름 " + snap.EarlyCount + "  느림 " + snap.LateCount);
            if (settings.ShowDifficulty && snap.Difficulty > 0) {
                parts.Add("난이도 " + snap.Difficulty + "/10");
            }
            if (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.Author)) {
                parts.Add("제작: " + snap.Author);
            }

            return Truncate(string.Join("  |  ", parts.ToArray()), 128);
        }

        private string BuildEditorStateLine(GameSnapshot snap, Settings settings) {
            var parts = new System.Collections.Generic.List<string>();

            if (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.Author)) {
                parts.Add("제작: " + snap.Author);
            }
            if (settings.ShowRemainingTiles && snap.TotalTiles > 0) {
                parts.Add(snap.TotalTiles + " 타일");
            }
            if (settings.ShowDifficulty && snap.Difficulty > 0) {
                parts.Add("난이도 " + snap.Difficulty + "/10");
            }
            if (settings.ShowBpm && snap.Bpm > 0) {
                parts.Add(Math.Round(snap.Bpm) + " BPM");
            }

            return Truncate(string.Join("  |  ", parts.ToArray()), 128);
        }

        private string BuildStateLine(GameSnapshot snap, Settings settings) {
            var parts = new System.Collections.Generic.List<string>();

            // Ordered so that if the line has to be truncated, the most
            // time-sensitive info survives and static/flavor info drops first.
            if (settings.ShowModeState && snap.Mode == GameMode.Dead) {
                parts.Add("죽음");
            } else if (settings.ShowModeState && snap.Mode == GameMode.Paused) {
                parts.Add("일시정지");
            }
            if (settings.ShowProgress) {
                parts.Add((snap.Progress * 100f).ToString("0.0") + "%");
            }
            if (settings.ShowRemainingTiles && snap.TotalTiles > 0) {
                parts.Add("남은 " + snap.RemainingTiles + "/" + snap.TotalTiles + " 타일");
            }
            if (settings.ShowAccuracy) {
                parts.Add("정확도 " + (snap.Accuracy * 100f).ToString("0.0") + "%");
            }
            if (settings.ShowXAccuracy) {
                parts.Add("X-정확도 " + (snap.XAccuracy * 100f).ToString("0.0") + "%");
            }
            if (settings.ShowBpm && snap.Bpm > 0) {
                parts.Add(Math.Round(snap.Bpm) + " BPM");
            }
            if (settings.ShowElapsedTime && snap.TotalSeconds > 0 && !settings.ShowAsListening) {
                parts.Add(FormatTime(snap.ElapsedSeconds) + " / " + FormatTime(snap.TotalSeconds));
            }
            if (settings.ShowDifficulty && snap.Difficulty > 0) {
                parts.Add("난이도 " + snap.Difficulty + "/10");
            }
            if (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.Author)) {
                parts.Add("제작: " + snap.Author);
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

        internal void Stop() {
            DisposeClient();
            imageUploader.Dispose();
        }

        public void Dispose() {
            Stop();
        }
    }
}
