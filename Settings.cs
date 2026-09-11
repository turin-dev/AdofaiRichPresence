using UnityEngine;
using UnityModManagerNet;

namespace AdofaiRichPresence {
    public class Settings : UnityModManager.ModSettings {
        public bool ShowLevelAndArtist = true;
        public bool ShowProgress = true;
        public bool ShowRemainingTiles = true;
        public bool ShowDifficulty = true;
        public bool ShowBpm = true;
        public bool ShowElapsedTime = true;
        public bool ShowModeState = true;

        public bool ShowAsListening = false;
        public bool ShowMapCoverImage = true;

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
            Save(this, modEntry);
        }

        public void Draw(UnityModManager.ModEntry modEntry) {
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
                    DrawConnectionTab();
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
            GUILayout.Space(5);
            ShowLevelAndArtist = GUILayout.Toggle(ShowLevelAndArtist, " 레벨/곡 이름 & 아티스트 & 제작자");
            ShowProgress = GUILayout.Toggle(ShowProgress, " 진행률 (%)");
            ShowRemainingTiles = GUILayout.Toggle(ShowRemainingTiles, " 남은 타일 수");
            ShowDifficulty = GUILayout.Toggle(ShowDifficulty, " 난이도");
            ShowBpm = GUILayout.Toggle(ShowBpm, " BPM");
            ShowElapsedTime = GUILayout.Toggle(ShowElapsedTime, " 경과 시간 / 곡 길이");
            ShowModeState = GUILayout.Toggle(ShowModeState, " 메뉴/일시정지/에디터 등 상태");
        }

        private void DrawBehaviorTab() {
            GUILayout.Label("표시 방식");
            GUILayout.Space(5);
            ShowAsListening = GUILayout.Toggle(ShowAsListening, " \"플레이 중\" 대신 \"듣는 중\"으로 표시");
            ShowMapCoverImage = GUILayout.Toggle(ShowMapCoverImage, " 맵 커버 이미지를 로고로 자동 사용 (워크샵 레벨만 지원)");
        }

        private void DrawConnectionTab() {
            GUILayout.Label("Discord Application ID (discord.com/developers/applications)");
            DiscordApplicationId = GUILayout.TextField(DiscordApplicationId, GUILayout.Width(300));
            GUILayout.Space(10);

            MuteBuiltInPresence = GUILayout.Toggle(MuteBuiltInPresence, " 게임 기본 Discord 표시 끄기 (권장, 충돌 방지)");
            GUILayout.Space(10);

            GUILayout.Label("업데이트 주기 (초): " + UpdateIntervalSeconds.ToString("0.0"));
            UpdateIntervalSeconds = GUILayout.HorizontalSlider(UpdateIntervalSeconds, 1f, 15f, GUILayout.Width(300));
        }

        private void DrawImagesTab() {
            GUILayout.Label("큰 이미지 (로고, Discord 개발자 포털 Art Assets에 업로드한 이름)");
            DrawKeyField("기본:", ref LargeImageKeyDefault);
            DrawKeyField("일시정지:", ref LargeImageKeyPaused);
            DrawKeyField("메뉴:", ref LargeImageKeyMenu);
            DrawKeyField("에디터:", ref LargeImageKeyEditor);
            GUILayout.Space(10);

            GUILayout.Label("작은 아이콘 (배지, 선택사항)");
            DrawKeyField("플레이 중:", ref SmallImageKeyPlaying);
            DrawKeyField("일시정지:", ref SmallImageKeyPaused);
            DrawKeyField("죽음:", ref SmallImageKeyDead);
        }

        private void DrawCdnTab() {
            GUILayout.Label("맵 커버 이미지 CDN (동작 방식 탭의 자동 사용 옵션에서 씀)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("업로드 URL:", GUILayout.Width(80));
            CdnUploadUrl = GUILayout.TextField(CdnUploadUrl, GUILayout.Width(300));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("비밀키:", GUILayout.Width(80));
            CdnUploadSecret = GUILayout.PasswordField(CdnUploadSecret, '*', GUILayout.Width(300));
            GUILayout.EndHorizontal();
        }

        private void DrawDebugTab() {
            DebugLogging = GUILayout.Toggle(DebugLogging, " 디버그 로그 (문제 생겼을 때만 켜세요)");
        }

        private static void DrawKeyField(string label, ref string value) {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70));
            value = GUILayout.TextField(value, GUILayout.Width(150));
            GUILayout.EndHorizontal();
        }
    }
}
