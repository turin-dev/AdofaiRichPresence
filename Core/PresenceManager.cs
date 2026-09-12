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
        // Keep the message template so changing language also translates an existing error.
        private Func<string, string> lastConnectionError;

        private static Func<string, string> ConnectionError(string template, params object[] args) {
            return language => Localization.Format(language, template, args);
        }

        internal PresenceManager(UnityModManager.ModEntry.ModLogger logger) {
            this.logger = logger;
            sessionStart = DateTime.UtcNow;
            imageUploader = new LevelImageUploader(logger);
        }

        internal string GetConnectionStatus(Settings settings) {
            if (settings == null || !settings.EnableDiscord) {
                return Localization.Text(settings?.Language, "사용 안 함");
            }
            if (connectionReady) {
                return settings.Text("연결됨");
            }
            if (lastConnectionError != null) {
                return settings.Text("오류: ") + Truncate(lastConnectionError(settings.Language), 90);
            }
            return settings.Text(client == null ? "연결 대기 중" : "연결 시도 중");
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
                HandleClientFailure(ConnectionError("Discord RPC 호출 실패: {0}", e.Message));
                return;
            }
            if (connectionFailed) {
                HandleClientFailure(lastConnectionError ?? ConnectionError("Discord RPC 연결이 끊겼습니다."));
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
                logger?.Log("[Debug] mode=" + snap.Mode + " | " + GameState.DebugState());
            }
            try {
                client.SetPresence(BuildPresence(snap, settings));
            } catch (Exception e) {
                HandleClientFailure(ConnectionError("Discord 상태 전송 실패: {0}", e.Message));
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
                lastConnectionError = ConnectionError("Application ID는 숫자만 입력해야 합니다.");
                if (failedApplicationId != applicationId || DateTime.UtcNow >= nextClientAttemptUtc) {
                    logger?.Warning("Discord Application ID must contain digits only.");
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
                    logger?.Log("Discord RPC connected (user: " + e.User.Username + ")");
                };
                client.OnConnectionFailed += (sender, e) => {
                    connectionReady = false;
                    connectionFailed = true;
                    lastConnectionError = ConnectionError("Discord RPC 파이프 연결 실패 (파이프 {0})", e.FailedPipe);
                    logger?.Warning(lastConnectionError("en"));
                };
                client.OnPresenceUpdate += (sender, e) => {
                    if (debugLoggingEnabled) {
                        logger?.Log("Discord presence sent: " + e.Presence?.Details + " / " + e.Presence?.State);
                    }
                };
                client.OnError += (sender, e) => {
                    lastConnectionError = ConnectionError("Discord RPC 오류 ({0}): {1}", e.Code, e.Message);
                    logger?.Error(lastConnectionError("en"));
                };
                client.Initialize();
                connectedApplicationId = applicationId;
                failedApplicationId = null;
                connectionReady = false;
                connectionFailed = false;
                lastConnectionError = null;
                sessionStart = DateTime.UtcNow;
                if (debugLoggingEnabled) {
                    logger?.Log("Initializing Discord RPC (Application ID: " + applicationId + ")");
                }
            } catch (Exception e) {
                logger?.Error("Discord RPC initialization failed: " + e.Message);
                DisposeClient();
                failedApplicationId = applicationId;
                nextClientAttemptUtc = DateTime.UtcNow.AddSeconds(30);
                lastConnectionError = ConnectionError("{0}", e.Message);
            }
        }

        private void HandleClientFailure(Func<string, string> message) {
            string applicationId = connectedApplicationId;
            lastConnectionError = message;
            connectionReady = false;
            connectionFailed = false;
            DisposeClient();
            if (!string.IsNullOrEmpty(applicationId)) {
                failedApplicationId = applicationId;
                nextClientAttemptUtc = DateTime.UtcNow.AddSeconds(30);
            }
            logger?.Warning(message("en"));
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
                    logger?.Warning("Failed to clear Discord activity: " + e.Message);
                }
            }
            try {
                client.Dispose();
            } catch (Exception e) {
                if (debugLoggingEnabled) {
                    logger?.Warning("Failed to dispose Discord client: " + e.Message);
                }
            } finally {
                // A broken client must never remain reachable after cleanup. This
                // path is used by both manual reconnect and the mod disable toggle.
                client = null;
                connectedApplicationId = null;
                connectionReady = false;
                connectionFailed = false;
                lastSentMode = (GameMode)(-1);
                timeSinceLastUpdate = 0f;
            }
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
                        : (snap.Mode == GameMode.Dead ? settings.Text("죽음") : snap.Mode == GameMode.Paused ? settings.Text("일시정지") : settings.Text("플레이 중"));
                    state = BuildStateLine(snap, settings);
                    break;
                case GameMode.Editor:
                    details = settings.Text("레벨 에디터: ") + (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.LevelName)
                        ? Truncate(snap.LevelName + (string.IsNullOrEmpty(snap.Artist) ? "" : " - " + snap.Artist), 110)
                        : settings.Text("제작 중"));
                    state = BuildEditorStateLine(snap, settings);
                    break;
                case GameMode.Cleared:
                    details = settings.Text("클리어: ") + (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.LevelName)
                        ? Truncate(snap.LevelName + (string.IsNullOrEmpty(snap.Artist) ? "" : " - " + snap.Artist), 110)
                        : settings.Text("완료!"));
                    state = settings.ShowDetailedResult ? BuildResultStateLine(snap, settings) : "";
                    break;
                case GameMode.LevelSelect:
                    details = settings.Text("레벨 선택 중");
                    state = "";
                    break;
                default:
                    details = settings.Text("메인 메뉴");
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
                        Label = settings.Text("워크샵에서 보기"),
                        Url = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + snap.WorkshopId,
                    });
                }
                if (settings.ShowModDownloadButton) {
                    buttons.Add(new Button {
                        Label = settings.Text("이 모드 받기"),
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

            if (settings.ShowAccuracy && snap.HasAccuracy) {
                parts.Add(settings.Text("정확도 ") + (snap.Accuracy * 100f).ToString("0.00") + "%");
            }
            if (settings.ShowXAccuracy && snap.HasXAccuracy) {
                parts.Add(settings.Text("X-정확도 ") + (snap.XAccuracy * 100f).ToString("0.00") + "%");
            }
            parts.Add(settings.Text("정확 {0}  빠름 {1}  느림 {2}", snap.PerfectCount, snap.EarlyCount, snap.LateCount));
            if (settings.ShowCheckpointUsage && snap.HasCheckpointUsage && snap.CheckpointsUsed > 0) {
                parts.Add(settings.Text("체크포인트 {0}회", snap.CheckpointsUsed));
            }
            if (settings.ShowDifficulty && snap.Difficulty > 0) {
                parts.Add(settings.Text("난이도 ") + snap.Difficulty + "/10");
            }
            if (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.Author)) {
                parts.Add(settings.Text("제작: ") + snap.Author);
            }

            return Truncate(string.Join("  |  ", parts.ToArray()), 128);
        }

        private string BuildEditorStateLine(GameSnapshot snap, Settings settings) {
            var parts = new System.Collections.Generic.List<string>();

            if (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.Author)) {
                parts.Add(settings.Text("제작: ") + snap.Author);
            }
            if (settings.ShowRemainingTiles && snap.TotalTiles > 0) {
                parts.Add(snap.TotalTiles + settings.Text(" 타일"));
            }
            if (settings.ShowDifficulty && snap.Difficulty > 0) {
                parts.Add(settings.Text("난이도 ") + snap.Difficulty + "/10");
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
            if (settings.ShowModeState) {
                if (snap.Mode == GameMode.Dead) {
                    parts.Add(settings.Text("죽음"));
                } else if (snap.Mode == GameMode.Paused) {
                    parts.Add(settings.Text("일시정지"));
                } else if (snap.Mode == GameMode.Playing) {
                    parts.Add(settings.ShowAsListening ? settings.Text("듣는 중") : settings.Text("플레이 중"));
                }
            }
            if (settings.ShowProgress) {
                parts.Add((snap.Progress * 100f).ToString("0.0") + "%");
            }
            if (settings.ShowRemainingTiles && snap.TotalTiles > 0) {
                parts.Add(settings.Text("남은 {0}/{1} 타일", snap.RemainingTiles, snap.TotalTiles));
            }
            if (settings.ShowCheckpointUsage && snap.HasCheckpointUsage && snap.CheckpointsUsed > 0) {
                parts.Add(settings.Text("체크포인트 {0}회", snap.CheckpointsUsed));
            }
            if (settings.ShowAccuracy && snap.HasAccuracy) {
                parts.Add(settings.Text("정확도 ") + (snap.Accuracy * 100f).ToString("0.0") + "%");
            }
            if (settings.ShowXAccuracy && snap.HasXAccuracy) {
                parts.Add(settings.Text("X-정확도 ") + (snap.XAccuracy * 100f).ToString("0.0") + "%");
            }
            if (settings.ShowBpm && snap.Bpm > 0) {
                parts.Add(Math.Round(snap.Bpm) + " BPM");
            }
            if (settings.ShowElapsedTime && snap.TotalSeconds > 0 && !settings.ShowAsListening) {
                parts.Add(FormatTime(snap.ElapsedSeconds) + " / " + FormatTime(snap.TotalSeconds));
            }
            if (settings.ShowDifficulty && snap.Difficulty > 0) {
                parts.Add(settings.Text("난이도 ") + snap.Difficulty + "/10");
            }
            if (settings.ShowLevelAndArtist && !string.IsNullOrEmpty(snap.Author)) {
                parts.Add(settings.Text("제작: ") + snap.Author);
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
