using System;
using AdofaiRichPresence.Core;
using UnityEngine;
using UnityModManagerNet;

namespace AdofaiRichPresence {
    public class Settings : UnityModManager.ModSettings {
        public string Language = "ko";

        internal string Text(string korean) => Localization.Text(Language, korean);
        internal string Text(string korean, params object[] args) => Localization.Format(Language, korean, args);
        public bool ShowLevelAndArtist = true;
        public bool ShowProgress = true;
        public bool ShowAccuracy = true;
        public bool ShowXAccuracy = false;
        public bool ShowRemainingTiles = true;
        public bool ShowDifficulty = true;
        public bool ShowBpm = true;
        public bool ShowElapsedTime = true;
        public bool ShowModeState = true;
        public bool ShowCheckpointUsage = false;

        public bool ShowAsListening = false;
        public bool ShowMapCoverImage = true;
        public bool ShowModDownloadButton = false;
        public bool ShowDetailedResult = true;
        public bool EnableDiscord = true;

        public string DiscordApplicationId = DiscordConfig.DefaultApplicationId;
        public bool MuteBuiltInPresence = true;
        public float UpdateIntervalSeconds = 3f;

        public string LargeImageKeyDefault = DiscordConfig.DefaultLargeImageKey;
        public string LargeImageKeyPaused = DiscordConfig.DefaultLargeImageKeyPaused;
        public string LargeImageKeyMenu = DiscordConfig.DefaultLargeImageKeyMenu;
        public string LargeImageKeyEditor = DiscordConfig.DefaultLargeImageKeyEditor;
        public string SmallImageKeyPlaying = DiscordConfig.DefaultSmallImageKeyPlaying;
        public string SmallImageKeyPaused = "";
        public string SmallImageKeyDead = "";

        public string CdnUploadUrl = DiscordConfig.DefaultCdnUploadUrl;
        public string CdnUploadSecret = DiscordConfig.DefaultCdnUploadSecret;

        public bool DebugLogging = false;

        // UI-only navigation state; not meaningful to persist as user-facing config,
        // but harmless if UMM's serializer picks it up.
        private int selectedTab;
        private bool resetConfirmationPending;
        private Settings settingsBeforeReset;
        private int tabBeforeReset;
        private GUIStyle wrappedLabelStyle;

        private void Label(string text, params GUILayoutOption[] options) {
            if (wrappedLabelStyle == null) {
                wrappedLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }
            GUILayout.Label(text, wrappedLabelStyle, options);
        }

        private static readonly string[] KoreanTabs = { "표시 정보", "동작 방식", "Discord 연결", "이미지", "CDN", "디버그" };
        private static readonly string[] EnglishTabs = { "Display", "Behavior", "Connection", "Images", "CDN", "Debug" };
        private static readonly string[] LanguageNames = { "한국어", "English" };

        public override void Save(UnityModManager.ModEntry modEntry) {
            NormalizeSettings();
            Save(this, modEntry);
        }

        internal void Draw(UnityModManager.ModEntry modEntry, PresenceManager presenceManager) {
            GUILayout.BeginVertical(GUILayout.MaxWidth(640));
            Label("ADOFAI Rich Presence");
            Label("언어 / Language");
            int languageIndex = Localization.NormalizeLanguage(Language) == "en" ? 1 : 0;
            Language = GUILayout.Toolbar(languageIndex, LanguageNames, GUILayout.Width(240)) == 1 ? "en" : "ko";
            Label(Text("설정을 바꾼 뒤 Unity Mod Manager의 저장 버튼을 눌러 변경사항을 보존하세요."));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("기본 설정 전체 복원"), GUILayout.MinWidth(160))) {
                resetConfirmationPending = true;
            }
            if (settingsBeforeReset != null && GUILayout.Button(Text("복원 취소"), GUILayout.Width(100))) {
                UndoReset();
            }
            GUILayout.EndHorizontal();
            if (resetConfirmationPending) {
                Label(Text("Discord 연결, CDN 주소와 비밀키를 포함한 모든 설정을 기본값으로 바꿉니다."));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Text("복원 실행"), GUILayout.Width(100))) {
                    ResetToDefaults();
                }
                if (GUILayout.Button(Text("취소"), GUILayout.Width(100))) {
                    resetConfirmationPending = false;
                }
                GUILayout.EndHorizontal();
            }
            if (settingsBeforeReset != null) {
                Label(Text("복원 취소를 누르면 직전 설정으로 돌아갑니다. 변경사항은 즉시 적용되며, 보존하려면 저장하세요."));
            }
            GUILayout.Space(8);

            string[] tabNames = Language == "en" ? EnglishTabs : KoreanTabs;
            selectedTab = Mathf.Clamp(selectedTab, 0, tabNames.Length - 1);
            float tabWidth = Mathf.Clamp(Screen.width - 40f, 360f, 600f);
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Width(tabWidth));
            GUILayout.Space(10);

            switch (selectedTab) {
                case 0:
                    DrawDisplayTab();
                    break;
                case 1:
                    DrawBehaviorTab();
                    break;
                case 2:
                    DrawConnectionTab(presenceManager);
                    break;
                case 3:
                    DrawImagesTab();
                    break;
                case 4:
                    DrawCdnTab();
                    break;
                case 5:
                    DrawDebugTab();
                    break;
            }
            GUILayout.EndVertical();
        }

        private void DrawDisplayTab() {
            Label(Text("Discord 상태 메시지에 표시할 정보를 고르세요."));
            Label(Text("상태 줄은 Discord 글자 수 제한에 맞춰 자동으로 줄어들 수 있습니다."));
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("추천 설정"), GUILayout.MinWidth(125))) {
                ShowLevelAndArtist = ShowProgress = ShowAccuracy = ShowRemainingTiles =
                    ShowDifficulty = ShowBpm = ShowElapsedTime = ShowModeState = true;
                ShowCheckpointUsage = true;
                ShowDetailedResult = true;
            }
            if (GUILayout.Button(Text("간단히 보기"), GUILayout.Width(110))) {
                ShowLevelAndArtist = true;
                ShowProgress = true;
                ShowAccuracy = ShowXAccuracy = ShowRemainingTiles =
                    ShowDifficulty = ShowBpm = ShowElapsedTime = ShowCheckpointUsage = false;
                ShowModeState = true;
                ShowDetailedResult = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            Label(Text("핵심 정보"));
            ShowLevelAndArtist = GUILayout.Toggle(ShowLevelAndArtist, Text(" 레벨/곡 이름 & 아티스트 & 제작자"));
            ShowProgress = GUILayout.Toggle(ShowProgress, Text(" 진행률 (%)"));
            ShowModeState = GUILayout.Toggle(ShowModeState, Text(" 메뉴/일시정지/에디터 등 상태"));

            GUILayout.Space(5);
            Label(Text("세부 정보"));
            ShowAccuracy = GUILayout.Toggle(ShowAccuracy, Text(" 정확도 (%)"));
            ShowXAccuracy = GUILayout.Toggle(ShowXAccuracy, Text(" X-정확도 (%, 더 엄격한 기준)"));
            ShowRemainingTiles = GUILayout.Toggle(ShowRemainingTiles, Text(" 남은 타일 수"));
            ShowDifficulty = GUILayout.Toggle(ShowDifficulty, Text(" 난이도"));
            ShowBpm = GUILayout.Toggle(ShowBpm, " BPM");
            ShowElapsedTime = GUILayout.Toggle(ShowElapsedTime, Text(" 경과 시간 / 곡 길이"));
            ShowCheckpointUsage = GUILayout.Toggle(ShowCheckpointUsage, Text(" 체크포인트 사용 횟수"));

            GUILayout.Space(12);
            DrawPresencePreview();
        }

        private void DrawPresencePreview() {
            Label(Text("미리보기 (예시 맵)"));
            Label(Text("현재 선택한 표시 옵션이 Discord의 두 줄에 어떻게 보이는지 보여줍니다."));
            GUILayout.BeginVertical("box");

            string details = ShowLevelAndArtist ? Text("예시 맵 - Example Artist") : Text("플레이 중");
            var stateParts = new System.Collections.Generic.List<string>();
            if (ShowModeState) {
                stateParts.Add(ShowAsListening ? Text("듣는 중") : Text("플레이 중"));
            }
            if (ShowProgress) {
                stateParts.Add("42.5%");
            }
            if (ShowRemainingTiles) {
                stateParts.Add(Text("남은 580/1000 타일"));
            }
            if (ShowAccuracy) {
                stateParts.Add(Text("정확도 98.42%"));
            }
            if (ShowXAccuracy) {
                stateParts.Add(Text("X-정확도 97.80%"));
            }
            if (ShowBpm) {
                stateParts.Add("180 BPM");
            }
            if (ShowElapsedTime && !ShowAsListening) {
                stateParts.Add("1:23 / 3:14");
            }
            if (ShowCheckpointUsage) {
                stateParts.Add(Text("체크포인트 2회"));
            }
            if (ShowDifficulty) {
                stateParts.Add(Text("난이도 8/10"));
            }
            if (ShowLevelAndArtist) {
                stateParts.Add(Text("제작: Example Creator"));
            }

            string state = stateParts.Count == 0
                ? Text("(표시할 상태 정보 없음)")
                : string.Join("  |  ", stateParts.ToArray());
            string clippedDetails = PreviewTruncate(details, 128);
            string clippedState = PreviewTruncate(state, 128);

            Label(Text("상세: ") + clippedDetails);
            Label(Text("상태: ") + clippedState);
            Label(Text("글자 수: 상세 {0}/128, 상태 {1}/128", details.Length, state.Length));
            if (details.Length > 128 || state.Length > 128) {
                Label(Text("상태 줄이 길어 Discord에서 끝부분이 잘립니다. 표시 항목을 줄여 보세요."));
            }
            if (ShowDetailedResult) {
                Label(Text("클리어 시 상세 결과가 별도 상태 줄에 표시됩니다."));
            }
            GUILayout.EndVertical();
        }

        private static string PreviewTruncate(string value, int max) {
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }

        private void DrawBehaviorTab() {
            Label(Text("표시 방식"));
            Label(Text("플레이 중인 정보와 완료 결과를 Discord에 어떻게 보여줄지 정합니다."));
            GUILayout.Space(5);

            Label(Text("활동 유형"));
            ShowAsListening = GUILayout.Toggle(ShowAsListening, Text(" \"플레이 중\" 대신 \"듣는 중\"으로 표시"));

            GUILayout.Space(5);
            Label(Text("이미지와 링크"));
            ShowMapCoverImage = GUILayout.Toggle(ShowMapCoverImage, Text(" 맵 커버 이미지를 로고로 자동 사용 (워크샵 레벨만 지원)"));
            if (ShowMapCoverImage) {
                Label(Text("  커버 이미지는 CDN 탭의 업로드 주소로 한 번 업로드됩니다."));
            }
            ShowModDownloadButton = GUILayout.Toggle(ShowModDownloadButton, Text(" \"이 모드 받기\" 버튼 표시"));
            Label(Text("  버튼은 기본적으로 숨겨져 있으며, 켜면 Discord 상태에 GitHub 링크가 추가됩니다."));
            ShowDetailedResult = GUILayout.Toggle(ShowDetailedResult, Text(" 레벨 완료 시 상세 결과(정확도 등) 표시"));
        }

        private void DrawConnectionTab(PresenceManager presenceManager) {
            Label(Text("Discord 연결"));
            Label(Text("기본 Application ID가 포함되어 있어 보통은 입력하지 않아도 됩니다."));
            Label(Text("자신의 Discord 앱과 Art Assets를 사용하려는 경우에만 바꾸세요."));
            GUILayout.Space(5);

            Label(Text("연결 상태: ") + (presenceManager == null ? Text("초기화 중") : presenceManager.GetConnectionStatus(this)));
            if (GUILayout.Button(Text("Discord 다시 연결"), GUILayout.Width(150))) {
                presenceManager?.RequestReconnect();
            }
            GUILayout.Space(5);

            EnableDiscord = GUILayout.Toggle(EnableDiscord, Text(" Discord 상태 표시 사용"));
            if (!EnableDiscord) {
                Label(Text("  Discord 연결을 중지하고 게임 기본 Discord 표시를 다시 사용합니다."));
            }

            Label(Text("Application ID (숫자만)"));
            DiscordApplicationId = GUILayout.TextField(DiscordApplicationId ?? "", GUILayout.Width(360));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("기본 ID로 되돌리기"), GUILayout.Width(150))) {
                DiscordApplicationId = DiscordConfig.DefaultApplicationId;
            }
            Label(Text("Discord 개발자 포털에서 확인할 수 있습니다."));
            GUILayout.EndHorizontal();

            if (string.IsNullOrWhiteSpace(DiscordApplicationId)) {
                Label(Text("Application ID가 비어 있습니다. 상태 표시를 사용하려면 ID를 입력하세요."));
            } else if (!ulong.TryParse(DiscordApplicationId.Trim(), out _)) {
                Label(Text("Application ID는 숫자만 입력해야 합니다."));
            }
            GUILayout.Space(10);

            MuteBuiltInPresence = GUILayout.Toggle(MuteBuiltInPresence, Text(" 게임 기본 Discord 표시 끄기 (권장, 충돌 방지)"));
            Label(Text("  이 모드의 표시와 게임 기본 표시가 서로 덮어쓰는 문제를 줄입니다."));
            GUILayout.Space(10);

            if (float.IsNaN(UpdateIntervalSeconds) || float.IsInfinity(UpdateIntervalSeconds)) {
                UpdateIntervalSeconds = 3f;
            }
            UpdateIntervalSeconds = Mathf.Clamp(UpdateIntervalSeconds, 1f, 15f);
            Label(Text("업데이트 주기: {0}초", UpdateIntervalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            Label(Text("  짧게 하면 더 빠르게 갱신되지만 게임과 Discord의 작업량이 늘어납니다."));
            UpdateIntervalSeconds = GUILayout.HorizontalSlider(UpdateIntervalSeconds, 1f, 15f, GUILayout.Width(300));
        }

        private void DrawImagesTab() {
            Label(Text("큰 이미지"));
            Label(Text("Discord 개발자 포털의 Rich Presence → Art Assets에 등록한 키를 입력하세요."));
            Label(Text("비워 두면 해당 상태는 기본 이미지로 대체됩니다."));
            DrawKeyField(Text("기본:"), ref LargeImageKeyDefault);
            DrawKeyField(Text("일시정지:"), ref LargeImageKeyPaused);
            DrawKeyField(Text("메뉴:"), ref LargeImageKeyMenu);
            DrawKeyField(Text("에디터:"), ref LargeImageKeyEditor);
            GUILayout.Space(10);

            Label(Text("작은 아이콘 (선택사항)"));
            Label(Text("키가 없거나 등록되지 않은 이미지는 Discord에서 표시되지 않을 수 있습니다."));
            DrawKeyField(Text("플레이 중:"), ref SmallImageKeyPlaying);
            DrawKeyField(Text("일시정지:"), ref SmallImageKeyPaused);
            DrawKeyField(Text("죽음:"), ref SmallImageKeyDead);
        }

        private void DrawCdnTab() {
            Label(Text("맵 커버 이미지 CDN"));
            Label(Text("동작 방식 탭에서 커버 이미지 자동 사용을 켰을 때만 사용됩니다."));
            Label(Text("커버 이미지는 입력한 서버로 업로드되므로, 신뢰할 수 있는 주소만 사용하세요."));
            if (ShowMapCoverImage && string.IsNullOrWhiteSpace(CdnUploadUrl)) {
                Label(Text("커버 이미지 자동 사용이 켜져 있지만 업로드 URL이 비어 있습니다."));
            }
            GUILayout.Space(5);
            Label(Text("업로드 설정"));
            GUILayout.BeginHorizontal();
            Label(Text("업로드 URL:"), GUILayout.Width(100));
            CdnUploadUrl = GUILayout.TextField(CdnUploadUrl ?? "", GUILayout.Width(360));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Label(Text("비밀키:"), GUILayout.Width(80));
            CdnUploadSecret = GUILayout.PasswordField(CdnUploadSecret ?? "", '*', GUILayout.Width(360));
            GUILayout.EndHorizontal();
            Label(Text("비밀키는 서버가 요구할 때만 입력하세요. 기본 서버는 비밀키 없이 사용할 수 있습니다."));
        }

        private void DrawDebugTab() {
            Label(Text("문제 해결"));
            DebugLogging = GUILayout.Toggle(DebugLogging, Text(" 디버그 로그 (문제 생겼을 때만 켜세요)"));
            Label(Text("  UMM 로그에 게임 상태와 Discord 연결 정보를 추가합니다. 평소에는 꺼 두는 것을 권장합니다."));
        }

        private void DrawKeyField(string label, ref string value) {
            GUILayout.BeginHorizontal();
            Label(label, GUILayout.Width(90));
            value = GUILayout.TextField(value ?? "", GUILayout.Width(180));
            GUILayout.EndHorizontal();
        }

        private void NormalizeSettings() {
            Language = Localization.NormalizeLanguage(Language);
            DiscordApplicationId = TrimOrEmpty(DiscordApplicationId);
            CdnUploadUrl = TrimOrEmpty(CdnUploadUrl);
            CdnUploadSecret = TrimOrEmpty(CdnUploadSecret);
            LargeImageKeyDefault = TrimOrEmpty(LargeImageKeyDefault);
            LargeImageKeyPaused = TrimOrEmpty(LargeImageKeyPaused);
            LargeImageKeyMenu = TrimOrEmpty(LargeImageKeyMenu);
            LargeImageKeyEditor = TrimOrEmpty(LargeImageKeyEditor);
            SmallImageKeyPlaying = TrimOrEmpty(SmallImageKeyPlaying);
            SmallImageKeyPaused = TrimOrEmpty(SmallImageKeyPaused);
            SmallImageKeyDead = TrimOrEmpty(SmallImageKeyDead);
            if (float.IsNaN(UpdateIntervalSeconds) || float.IsInfinity(UpdateIntervalSeconds)) {
                UpdateIntervalSeconds = 3f;
            }
            UpdateIntervalSeconds = Mathf.Clamp(UpdateIntervalSeconds, 1f, 15f);
        }

        private static string TrimOrEmpty(string value) {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        private void ResetToDefaults() {
            // All persisted settings are scalars or immutable strings. Do not retain
            // earlier undo snapshots or UI confirmation state in this snapshot.
            settingsBeforeReset = (Settings)MemberwiseClone();
            settingsBeforeReset.settingsBeforeReset = null;
            settingsBeforeReset.resetConfirmationPending = false;
            tabBeforeReset = selectedTab;
            CopyFrom(new Settings());
            selectedTab = 0;
            resetConfirmationPending = false;
        }

        private void UndoReset() {
            if (settingsBeforeReset == null) {
                return;
            }
            CopyFrom(settingsBeforeReset);
            selectedTab = tabBeforeReset;
            settingsBeforeReset = null;
            resetConfirmationPending = false;
        }

        private void CopyFrom(Settings defaults) {
            Language = defaults.Language;
            ShowLevelAndArtist = defaults.ShowLevelAndArtist;
            ShowProgress = defaults.ShowProgress;
            ShowAccuracy = defaults.ShowAccuracy;
            ShowXAccuracy = defaults.ShowXAccuracy;
            ShowRemainingTiles = defaults.ShowRemainingTiles;
            ShowDifficulty = defaults.ShowDifficulty;
            ShowBpm = defaults.ShowBpm;
            ShowElapsedTime = defaults.ShowElapsedTime;
            ShowModeState = defaults.ShowModeState;
            ShowCheckpointUsage = defaults.ShowCheckpointUsage;
            ShowAsListening = defaults.ShowAsListening;
            ShowMapCoverImage = defaults.ShowMapCoverImage;
            ShowModDownloadButton = defaults.ShowModDownloadButton;
            ShowDetailedResult = defaults.ShowDetailedResult;
            EnableDiscord = defaults.EnableDiscord;
            DiscordApplicationId = defaults.DiscordApplicationId;
            MuteBuiltInPresence = defaults.MuteBuiltInPresence;
            UpdateIntervalSeconds = defaults.UpdateIntervalSeconds;
            LargeImageKeyDefault = defaults.LargeImageKeyDefault;
            LargeImageKeyPaused = defaults.LargeImageKeyPaused;
            LargeImageKeyMenu = defaults.LargeImageKeyMenu;
            LargeImageKeyEditor = defaults.LargeImageKeyEditor;
            SmallImageKeyPlaying = defaults.SmallImageKeyPlaying;
            SmallImageKeyPaused = defaults.SmallImageKeyPaused;
            SmallImageKeyDead = defaults.SmallImageKeyDead;
            CdnUploadUrl = defaults.CdnUploadUrl;
            CdnUploadSecret = defaults.CdnUploadSecret;
            DebugLogging = defaults.DebugLogging;
        }
    }
}
