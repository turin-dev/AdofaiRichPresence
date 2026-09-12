using System;
using System.IO;
using System.Text;
using UnityEngine.Networking;
using UnityModManagerNet;

namespace AdofaiRichPresence.Core {
    // Uploads the current level's cover image to the configured CDN once per level
    // and caches the returned URL, so we don't re-upload every presence tick.
    // Discord's SET_ACTIVITY accepts a direct https URL for the large image, so no
    // further Discord-side registration is needed once we have that URL.
    internal class LevelImageUploader {
        private readonly UnityModManager.ModEntry.ModLogger logger;
        private string cachedSourcePath;
        private string cachedUrl;
        private string cachedConfigurationKey;
        private long cachedSourceLength = -1;
        private DateTime cachedSourceLastWriteUtc;
        private string pendingSourcePath;
        private string pendingConfigurationKey;
        private long pendingSourceLength = -1;
        private DateTime pendingSourceLastWriteUtc;
        private UnityWebRequestAsyncOperation pendingRequest;
        private string failedConfigurationKey;
        private DateTime retryNotBeforeUtc;
        private bool justCompleted;

        private const long MaxImageBytes = 8 * 1024 * 1024;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);
        private const int RequestTimeoutSeconds = 30;

        internal LevelImageUploader(UnityModManager.ModEntry.ModLogger logger) {
            this.logger = logger;
        }

        internal string GetUrlFor(string previewImagePath, string uploadUrl, string uploadSecret) {
            previewImagePath = previewImagePath == null ? "" : previewImagePath.Trim();
            uploadUrl = uploadUrl == null ? "" : uploadUrl.Trim();
            uploadSecret = uploadSecret == null ? "" : uploadSecret.Trim();
            if (string.IsNullOrEmpty(previewImagePath) || string.IsNullOrEmpty(uploadUrl)) {
                return null;
            }
            string configurationKey = BuildConfigurationKey(previewImagePath, uploadUrl, uploadSecret);
            if (previewImagePath == cachedSourcePath && configurationKey == cachedConfigurationKey
                && IsSourceCurrent(previewImagePath, cachedSourceLength, cachedSourceLastWriteUtc)) {
                return cachedUrl;
            }
            if (pendingRequest != null) {
                if (configurationKey == pendingConfigurationKey
                    && IsSourceCurrent(previewImagePath, pendingSourceLength, pendingSourceLastWriteUtc)) {
                    return null;
                }
                AbortPendingRequest();
            }
            if (configurationKey == failedConfigurationKey && DateTime.UtcNow < retryNotBeforeUtc) {
                return null;
            }
            if (!IsHttpsUrl(uploadUrl)) {
                MarkFailure(configurationKey, "맵 이미지 업로드 URL은 https 주소여야 합니다.");
                return null;
            }
            StartUpload(previewImagePath, uploadUrl, uploadSecret, configurationKey);
            return null;
        }

        internal void Tick() {
            if (pendingRequest == null || !pendingRequest.isDone) {
                return;
            }
            UnityWebRequest req = pendingRequest.webRequest;
            string sourcePath = pendingSourcePath;
            string configurationKey = pendingConfigurationKey;
            long sourceLength = pendingSourceLength;
            DateTime sourceLastWriteUtc = pendingSourceLastWriteUtc;
            pendingRequest = null;
            pendingSourcePath = null;
            pendingConfigurationKey = null;
            pendingSourceLength = -1;
            pendingSourceLastWriteUtc = default(DateTime);

            try {
                if (req == null || req.result != UnityWebRequest.Result.Success) {
                    MarkFailure(configurationKey, "맵 이미지 업로드 실패: " + (req?.error ?? "요청이 없습니다."));
                    return;
                }

                string response = req.downloadHandler != null ? req.downloadHandler.text : null;
                string url = ExtractUrl(response);
                if (!IsHttpsUrl(url)) {
                    MarkFailure(configurationKey, "맵 이미지 업로드 응답에 유효한 https URL이 없습니다.");
                    return;
                }

                cachedSourcePath = sourcePath;
                cachedConfigurationKey = configurationKey;
                cachedUrl = url;
                cachedSourceLength = sourceLength;
                cachedSourceLastWriteUtc = sourceLastWriteUtc;
                failedConfigurationKey = null;
                justCompleted = true;
                if (RunFreezeState.DebugLogging) {
                    logger?.Log("맵 이미지 업로드 완료: " + url);
                }
            } catch (Exception e) {
                MarkFailure(configurationKey, "맵 이미지 업로드 처리 실패: " + e.Message);
            } finally {
                req?.Dispose();
            }
        }

        internal bool ConsumeJustCompleted() {
            bool result = justCompleted;
            justCompleted = false;
            return result;
        }

        private void StartUpload(string previewImagePath, string uploadUrl, string uploadSecret, string configurationKey) {
            byte[] bytes;
            try {
                FileInfo file = new FileInfo(previewImagePath);
                if (!file.Exists) {
                    MarkFailure(configurationKey, "맵 커버 이미지를 찾을 수 없습니다.");
                    return;
                }
                if (file.Length <= 0 || file.Length > MaxImageBytes) {
                    MarkFailure(configurationKey, "맵 커버 이미지는 8MB 이하이어야 합니다.");
                    return;
                }
                long sourceLength = file.Length;
                DateTime sourceLastWriteUtc = file.LastWriteTimeUtc;
                bytes = File.ReadAllBytes(previewImagePath);

                pendingSourceLength = sourceLength;
                pendingSourceLastWriteUtc = sourceLastWriteUtc;
            } catch (Exception e) {
                MarkFailure(configurationKey, "맵 이미지 읽기 실패: " + e.Message);
                return;
            }

            string mime = GuessMimeType(previewImagePath);
            if (string.IsNullOrEmpty(mime)) {
                MarkFailure(configurationKey, "지원하지 않는 맵 이미지 형식입니다.");
                return;
            }
            UnityWebRequest req = null;
            try {
                req = new UnityWebRequest(uploadUrl, "POST") {
                    uploadHandler = new UploadHandlerRaw(bytes) { contentType = mime },
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = RequestTimeoutSeconds,
                };
                if (!string.IsNullOrEmpty(uploadSecret)) {
                    req.SetRequestHeader("Authorization", "Bearer " + uploadSecret);
                }
                req.SetRequestHeader("Content-Type", mime);

                pendingSourcePath = previewImagePath;
                pendingConfigurationKey = configurationKey;
                pendingRequest = req.SendWebRequest();
            } catch (Exception e) {
                req?.Dispose();
                MarkFailure(configurationKey, "맵 이미지 업로드 요청 생성 실패: " + e.Message);
            }
        }

        private static string GuessMimeType(string path) {
            string lower = path.ToLowerInvariant();
            if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg")) {
                return "image/jpeg";
            }
            if (lower.EndsWith(".webp")) {
                return "image/webp";
            }
            if (lower.EndsWith(".png")) {
                return "image/png";
            }
            return null;
        }

        private static string ExtractUrl(string json) {
            if (string.IsNullOrEmpty(json)) {
                return null;
            }
            int key = json.IndexOf("\"url\"", StringComparison.OrdinalIgnoreCase);
            if (key < 0) {
                return null;
            }
            int colon = json.IndexOf(':', key + 5);
            if (colon < 0) {
                return null;
            }
            int start = colon + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start])) {
                start++;
            }
            if (start >= json.Length || json[start] != '"') {
                return null;
            }
            start++;

            var value = new StringBuilder();
            for (int i = start; i < json.Length; i++) {
                char c = json[i];
                if (c == '"') {
                    return value.ToString();
                }
                if (c != '\\' || i + 1 >= json.Length) {
                    value.Append(c);
                    continue;
                }
                char escaped = json[++i];
                switch (escaped) {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    default: return null;
                }
            }
            return null;
        }

        private static bool IsHttpsUrl(string url) {
            Uri uri;
            return !string.IsNullOrEmpty(url)
                && Uri.TryCreate(url, UriKind.Absolute, out uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && !string.IsNullOrEmpty(uri.Host);
        }

        private static bool IsSourceCurrent(string path, long expectedLength, DateTime expectedLastWriteUtc) {
            if (string.IsNullOrEmpty(path) || expectedLength < 0) {
                return false;
            }
            try {
                FileInfo file = new FileInfo(path);
                return file.Exists
                    && file.Length == expectedLength
                    && file.LastWriteTimeUtc == expectedLastWriteUtc;
            } catch {
                return false;
            }
        }

        private void MarkFailure(string configurationKey, string message) {
            if (configurationKey == failedConfigurationKey && DateTime.UtcNow < retryNotBeforeUtc) {
                return;
            }
            failedConfigurationKey = configurationKey;
            retryNotBeforeUtc = DateTime.UtcNow.Add(RetryDelay);
            logger?.Warning(message);
        }

        private static string BuildConfigurationKey(string sourcePath, string uploadUrl, string uploadSecret) {
            return sourcePath + "\n" + uploadUrl + "\n" + uploadSecret;
        }

        private void AbortPendingRequest() {
            UnityWebRequest req = pendingRequest?.webRequest;
            pendingRequest = null;
            pendingSourcePath = null;
            pendingConfigurationKey = null;
            pendingSourceLength = -1;
            pendingSourceLastWriteUtc = default(DateTime);
            try {
                req?.Abort();
            } catch {
                // The request may already have completed while the level changed.
            }
            req?.Dispose();
        }

        internal void Dispose() {
            AbortPendingRequest();
            cachedSourcePath = null;
            cachedConfigurationKey = null;
            cachedUrl = null;
            cachedSourceLength = -1;
            cachedSourceLastWriteUtc = default(DateTime);
            failedConfigurationKey = null;
            justCompleted = false;
        }
    }
}
