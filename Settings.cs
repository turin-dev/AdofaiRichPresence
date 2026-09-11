using System;
using AdofaiRichPresence.Core;
using UnityEngine;
using UnityModManagerNet;

namespace AdofaiRichPresence {
    public class Settings : UnityModManager.ModSettings {
        public bool ShowLevelAndArtist = true;
        public bool ShowProgress = true;
        public bool ShowAccuracy = true;
        public bool ShowXAccuracy = false;
        public bool ShowRemainingTiles = true;
        public bool ShowDifficulty = true;
        public bool ShowBpm = true;
        public bool ShowElapsedTime = true;
        public bool ShowModeState = true;

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

        private static readonly string[] TabNames = { "표시 정보", "동작 방식", "Discord 연결", "이미지", "CDN", "디버그" };

        public override void Save(UnityModManager.ModEntry modEntry) {
            NormalizeSettings();
            Save(this, modEntry);
        }

        internal void Draw(UnityModManager.ModEntry modEntry, PresenceManager presenceManager) {
            GUILayout.Label("ADOFAI Rich Presence");
            GUILayout.Label("설정을 바꾼 뒤 Unity Mod Manager의 저장 버튼을 눌러 변경사항을 보존하세요.");
            GUILayout.Space(8);

            selectedTab = Mathf.Clamp(selectedTab, 0, TabNames.Length - 1);
            selectedTab = GUILayout.Toolbar(selectedTab, TabNames, GUILayout.Width(600));
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
        }

        private void DrawDisplayTab() {
            GUILayout.Label("Discord 상태 메시지에 표시할 정보를 고르세요.");
            GUILayout.Label("상태 줄은 Discord 글자 수 제한에 맞춰 자동으로 줄어들 수 있습니다.");
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("추천 설정", GUILayout.Width(110))) {
                ShowLevelAndArtist = ShowProgress = ShowAccuracy = ShowRemainingTiles =
                    ShowDifficulty = ShowBpm = ShowElapsedTime = ShowModeState = true;
                ShowDetailedResult = true;
            }
            if (GUILayout.Button("간단히 보기", GUILayout.Width(110))) {
                ShowLevelAndArtist = true;
                ShowProgress = true;
                ShowAccuracy = ShowXAccuracy = ShowRemainingTiles =
                    ShowDifficulty = ShowBpm = ShowElapsedTime = false;
                ShowModeState = true;
                ShowDetailedResult = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.Label("핵심 정보");
            ShowLevelAndArtist = GUILayout.Toggle(ShowLevelAndArtist, " 레벨/곡 이름 & 아티스트 & 제작자");
            ShowProgress = GUILayout.Toggle(ShowProgress, " 진행률 (%)");
            ShowModeState = GUILayout.Toggle(ShowModeState, " 메뉴/일시정지/에디터 등 상태");

            GUILayout.Space(5);
            GUILayout.Label("세부 정보");
            ShowAccuracy = GUILayout.Toggle(ShowAccuracy, " 정확도 (%)");
            ShowXAccuracy = GUILayout.Toggle(ShowXAccuracy, " X-정확도 (%, 더 엄격한 기준)");
            ShowRemainingTiles = GUILayout.Toggle(ShowRemainingTiles, " 남은 타일 수");
            ShowDifficulty = GUILayout.Toggle(ShowDifficulty, " 난이도");
            ShowBpm = GUILayout.Toggle(ShowBpm, " BPM");
            ShowElapsedTime = GUILayout.Toggle(ShowElapsedTime, " 경과 시간 / 곡 길이");

            GUILayout.Space(12);
            DrawPresencePreview();
        }

        private void DrawPresencePreview() {
            GUILayout.Label("미리보기 (예시 맵)");
            GUILayout.Label("현재 선택한 표시 옵션이 Discord의 두 줄에 어떻게 보이는지 보여줍니다.");
            GUILayout.BeginVertical("box");

            string details = ShowLevelAndArtist ? "예시 맵 - Example Artist" : "플레이 중";
            var stateParts = new System.Collections.Generic.List<string>();
            if (ShowModeState) {
                stateParts.Add(ShowAsListening ? "듣는 중" : "플레이 중");
            }
            if (ShowProgress) {
                stateParts.Add("42.5%");
            }
            if (ShowRemainingTiles) {
                stateParts.Add("남은 580/1000 타일");
            }
            if (ShowAccuracy) {
                stateParts.Add("정확도 98.42%");
            }
            if (ShowXAccuracy) {
                stateParts.Add("X-정확도 97.80%");
            }
            if (ShowBpm) {
                stateParts.Add("180 BPM");
            }
            if (ShowElapsedTime && !ShowAsListening) {
                stateParts.Add("1:23 / 3:14");
            }
            if (ShowDifficulty) {
                stateParts.Add("난이도 8/10");
            }
            if (ShowLevelAndArtist) {
                stateParts.Add("제작: Example Creator");
            }

            string state = stateParts.Count == 0
                ? "(표시할 상태 정보 없음)"
                : string.Join("  |  ", stateParts.ToArray());
            string clippedDetails = PreviewTruncate(details, 128);
            string clippedState = PreviewTruncate(state, 128);

            GUILayout.Label("상세: " + clippedDetails);
            GUILayout.Label("상태: " + clippedState);
            GUILayout.Label("글자 수: 상세 " + details.Length + "/128, 상태 " + state.Length + "/128");
            if (details.Length > 128 || state.Length > 128) {
                GUILayout.Label("상태 줄이 길어 Discord에서 끝부분이 잘립니다. 표시 항목을 줄여 보세요.");
            }
            if (ShowDetailedResult) {
                GUILayout.Label("클리어 시 상세 결과가 별도 상태 줄에 표시됩니다.");
            }
            GUILayout.EndVertical();
        }

        private static string PreviewTruncate(string value, int max) {
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }

        private void DrawBehaviorTab() {
            GUILayout.Label("표시 방식");
            GUILayout.Label("플레이 중인 정보와 완료 결과를 Discord에 어떻게 보여줄지 정합니다.");
            GUILayout.Space(5);

            GUILayout.Label("활동 유형");
            ShowAsListening = GUILayout.Toggle(ShowAsListening, " \"플레이 중\" 대신 \"듣는 중\"으로 표시");

            GUILayout.Space(5);
            GUILayout.Label("이미지와 링크");
            ShowMapCoverImage = GUILayout.Toggle(ShowMapCoverImage, " 맵 커버 이미지를 로고로 자동 사용 (워크샵 레벨만 지원)");
            if (ShowMapCoverImage) {
                GUILayout.Label("  커버 이미지는 CDN 탭의 업로드 주소로 한 번 업로드됩니다.");
            }
            ShowModDownloadButton = GUILayout.Toggle(ShowModDownloadButton, " \"이 모드 받기\" 버튼 표시");
            GUILayout.Label("  버튼은 기본적으로 숨겨져 있으며, 켜면 Discord 상태에 GitHub 링크가 추가됩니다.");
            ShowDetailedResult = GUILayout.Toggle(ShowDetailedResult, " 레벨 완료 시 상세 결과(정확도 등) 표시");
        }

        private void DrawConnectionTab(PresenceManager presenceManager) {
            GUILayout.Label("Discord 연결");
            GUILayout.Label("기본 Application ID가 포함되어 있어 보통은 입력하지 않아도 됩니다.");
            GUILayout.Label("자신의 Discord 앱과 Art Assets를 사용하려는 경우에만 바꾸세요.");
            GUILayout.Space(5);

            GUILayout.Label("연결 상태: " + (presenceManager == null ? "초기화 중" : presenceManager.GetConnectionStatus(this)));
            if (GUILayout.Button("Discord 다시 연결", GUILayout.Width(150))) {
                presenceManager?.RequestReconnect();
            }
            GUILayout.Space(5);

            EnableDiscord = GUILayout.Toggle(EnableDiscord, " Discord 상태 표시 사용");
            if (!EnableDiscord) {
                GUILayout.Label("  꺼져 있으면 Discord 연결과 상태 업데이트를 중지합니다.");
            }

            GUILayout.Label("Application ID (숫자만)");
            DiscordApplicationId = GUILayout.TextField(DiscordApplicationId ?? "", GUILayout.Width(360));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("기본 ID로 되돌리기", GUILayout.Width(150))) {
                DiscordApplicationId = DiscordConfig.DefaultApplicationId;
            }
            GUILayout.Label("Discord 개발자 포털에서 확인할 수 있습니다.");
            GUILayout.EndHorizontal();

            if (string.IsNullOrWhiteSpace(DiscordApplicationId)) {
                GUILayout.Label("Application ID가 비어 있습니다. 상태 표시를 사용하려면 ID를 입력하세요.");
            } else if (!ulong.TryParse(DiscordApplicationId.Trim(), out _)) {
                GUILayout.Label("Application ID는 숫자만 입력해야 합니다.");
            }
            GUILayout.Space(10);

            MuteBuiltInPresence = GUILayout.Toggle(MuteBuiltInPresence, " 게임 기본 Discord 표시 끄기 (권장, 충돌 방지)");
            GUILayout.Label("  이 모드의 표시와 게임 기본 표시가 서로 덮어쓰는 문제를 줄입니다.");
            GUILayout.Space(10);

            GUILayout.Label("업데이트 주기: " + UpdateIntervalSeconds.ToString("0.0") + "초");
            GUILayout.Label("  짧게 하면 더 빠르게 갱신되지만 게임과 Discord의 작업량이 늘어납니다.");
            UpdateIntervalSeconds = GUILayout.HorizontalSlider(UpdateIntervalSeconds, 1f, 15f, GUILayout.Width(300));
        }

        private void DrawImagesTab() {
            GUILayout.Label("큰 이미지");
            GUILayout.Label("Discord 개발자 포털의 Rich Presence → Art Assets에 등록한 키를 입력하세요.");
            GUILayout.Label("비워 두면 해당 상태는 기본 이미지로 대체됩니다.");
            DrawKeyField("기본:", ref LargeImageKeyDefault);
            DrawKeyField("일시정지:", ref LargeImageKeyPaused);
            DrawKeyField("메뉴:", ref LargeImageKeyMenu);
            DrawKeyField("에디터:", ref LargeImageKeyEditor);
            GUILayout.Space(10);

            GUILayout.Label("작은 아이콘 (선택사항)");
            GUILayout.Label("키가 없거나 등록되지 않은 이미지는 Discord에서 표시되지 않을 수 있습니다.");
            DrawKeyField("플레이 중:", ref SmallImageKeyPlaying);
            DrawKeyField("일시정지:", ref SmallImageKeyPaused);
            DrawKeyField("죽음:", ref SmallImageKeyDead);
        }

        private void DrawCdnTab() {
            GUILayout.Label("맵 커버 이미지 CDN");
            GUILayout.Label("동작 방식 탭에서 커버 이미지 자동 사용을 켰을 때만 사용됩니다.");
            GUILayout.Label("커버 이미지는 입력한 서버로 업로드되므로, 신뢰할 수 있는 주소만 사용하세요.");
            if (ShowMapCoverImage && string.IsNullOrWhiteSpace(CdnUploadUrl)) {
                GUILayout.Label("커버 이미지 자동 사용이 켜져 있지만 업로드 URL이 비어 있습니다.");
            }
            GUILayout.Space(5);
            GUILayout.Label("업로드 설정");
            GUILayout.BeginHorizontal();
            GUILayout.Label("업로드 URL:", GUILayout.Width(80));
            CdnUploadUrl = GUILayout.TextField(CdnUploadUrl ?? "", GUILayout.Width(360));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("비밀키:", GUILayout.Width(80));
            CdnUploadSecret = GUILayout.PasswordField(CdnUploadSecret ?? "", '*', GUILayout.Width(360));
            GUILayout.EndHorizontal();
            GUILayout.Label("비밀키는 서버가 요구할 때만 입력하세요. 기본 서버는 비밀키 없이 사용할 수 있습니다.");
        }

        private void DrawDebugTab() {
            GUILayout.Label("문제 해결");
            DebugLogging = GUILayout.Toggle(DebugLogging, " 디버그 로그 (문제 생겼을 때만 켜세요)");
            GUILayout.Label("  UMM 로그에 게임 상태와 Discord 연결 정보를 추가합니다. 평소에는 꺼 두는 것을 권장합니다.");
        }

        private static void DrawKeyField(string label, ref string value) {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70));
            value = GUILayout.TextField(value ?? "", GUILayout.Width(180));
            GUILayout.EndHorizontal();
        }

        private void NormalizeSettings() {
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
            UpdateIntervalSeconds = Mathf.Clamp(UpdateIntervalSeconds, 1f, 15f);
        }

        private static string TrimOrEmpty(string value) {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }
    }
}
