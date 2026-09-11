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

        public bool ShowAdvanced = false;

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

        public override void Save(UnityModManager.ModEntry modEntry) {
            Save(this, modEntry);
        }

        public void Draw(UnityModManager.ModEntry modEntry) {
            GUILayout.Label("표시할 정보");
            ShowLevelAndArtist = GUILayout.Toggle(ShowLevelAndArtist, " 레벨/곡 이름 & 아티스트");
            ShowProgress = GUILayout.Toggle(ShowProgress, " 진행률 (%)");
            ShowRemainingTiles = GUILayout.Toggle(ShowRemainingTiles, " 남은 타일 수");
            ShowDifficulty = GUILayout.Toggle(ShowDifficulty, " 난이도");
            ShowBpm = GUILayout.Toggle(ShowBpm, " BPM");
            ShowElapsedTime = GUILayout.Toggle(ShowElapsedTime, " 경과 시간 / 곡 길이");
            ShowModeState = GUILayout.Toggle(ShowModeState, " 메뉴/일시정지/에디터 등 상태");
            GUILayout.Space(10);

            ShowAsListening = GUILayout.Toggle(ShowAsListening, " \"플레이 중\" 대신 \"듣는 중\"으로 표시");
            ShowMapCoverImage = GUILayout.Toggle(ShowMapCoverImage, " 맵 커버 이미지를 로고로 자동 사용");
            GUILayout.Space(10);

            ShowAdvanced = GUILayout.Toggle(ShowAdvanced, (ShowAdvanced ? "▼" : "▶") + " 고급 설정");
            if (!ShowAdvanced) {
                return;
            }
            GUILayout.Space(5);

            GUILayout.Label("Discord Application ID (discord.com/developers/applications)");
            DiscordApplicationId = GUILayout.TextField(DiscordApplicationId, GUILayout.Width(300));
            GUILayout.Space(10);

            MuteBuiltInPresence = GUILayout.Toggle(MuteBuiltInPresence, " 게임 기본 Discord 표시 끄기 (권장, 충돌 방지)");
            GUILayout.Space(10);

            GUILayout.Label("업데이트 주기 (초): " + UpdateIntervalSeconds.ToString("0.0"));
            UpdateIntervalSeconds = GUILayout.HorizontalSlider(UpdateIntervalSeconds, 1f, 15f, GUILayout.Width(300));
            GUILayout.Space(10);

            GUILayout.Label("Rich Presence 이미지 에셋 키 (Discord 개발자 포털의 Art Assets에 업로드한 이름)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("기본:", GUILayout.Width(50));
            LargeImageKeyDefault = GUILayout.TextField(LargeImageKeyDefault, GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("일시정지:", GUILayout.Width(50));
            LargeImageKeyPaused = GUILayout.TextField(LargeImageKeyPaused, GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("메뉴:", GUILayout.Width(50));
            LargeImageKeyMenu = GUILayout.TextField(LargeImageKeyMenu, GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("에디터:", GUILayout.Width(50));
            LargeImageKeyEditor = GUILayout.TextField(LargeImageKeyEditor, GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.Label("맵 커버 이미지 CDN");
            GUILayout.BeginHorizontal();
            GUILayout.Label("업로드 URL:", GUILayout.Width(80));
            CdnUploadUrl = GUILayout.TextField(CdnUploadUrl, GUILayout.Width(300));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("비밀키:", GUILayout.Width(80));
            CdnUploadSecret = GUILayout.PasswordField(CdnUploadSecret, '*', GUILayout.Width(300));
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.Label("스몰 아이콘 (상태 배지, 선택사항)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("플레이 중:", GUILayout.Width(80));
            SmallImageKeyPlaying = GUILayout.TextField(SmallImageKeyPlaying, GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("일시정지:", GUILayout.Width(80));
            SmallImageKeyPaused = GUILayout.TextField(SmallImageKeyPaused, GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("죽음:", GUILayout.Width(80));
            SmallImageKeyDead = GUILayout.TextField(SmallImageKeyDead, GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            DebugLogging = GUILayout.Toggle(DebugLogging, " 디버그 로그 (문제 생겼을 때만 켜세요)");
        }
    }
}
