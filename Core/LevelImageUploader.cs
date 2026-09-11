using System;
using System.IO;
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
        private string pendingSourcePath;
        private UnityWebRequestAsyncOperation pendingRequest;
        private bool justCompleted;

        internal LevelImageUploader(UnityModManager.ModEntry.ModLogger logger) {
            this.logger = logger;
        }

        internal string GetUrlFor(string previewImagePath, string uploadUrl, string uploadSecret) {
            if (string.IsNullOrEmpty(previewImagePath) || string.IsNullOrEmpty(uploadUrl)) {
                return null;
            }
            if (previewImagePath == cachedSourcePath) {
                return cachedUrl;
            }
            if (previewImagePath != pendingSourcePath) {
                StartUpload(previewImagePath, uploadUrl, uploadSecret);
            }
            return null;
        }

        internal void Tick() {
            if (pendingRequest == null || !pendingRequest.isDone) {
                return;
            }
            UnityWebRequest req = pendingRequest.webRequest;
            string sourcePath = pendingSourcePath;
            pendingRequest = null;
            pendingSourcePath = null;

            if (req.result != UnityWebRequest.Result.Success) {
                logger?.Warning("맵 이미지 업로드 실패: " + req.error);
                return;
            }

            string url = ExtractUrl(req.downloadHandler.text);
            if (string.IsNullOrEmpty(url)) {
                logger?.Warning("맵 이미지 업로드 응답 파싱 실패: " + req.downloadHandler.text);
                return;
            }

            cachedSourcePath = sourcePath;
            cachedUrl = url;
            justCompleted = true;
            if (RunFreezeState.DebugLogging) {
                logger?.Log("맵 이미지 업로드 완료: " + url);
            }
        }

        internal bool ConsumeJustCompleted() {
            bool result = justCompleted;
            justCompleted = false;
            return result;
        }

        private void StartUpload(string previewImagePath, string uploadUrl, string uploadSecret) {
            byte[] bytes;
            try {
                bytes = File.ReadAllBytes(previewImagePath);
            } catch (Exception e) {
                logger?.Warning("맵 이미지 읽기 실패: " + e.Message);
                return;
            }

            string mime = GuessMimeType(previewImagePath);
            var req = new UnityWebRequest(uploadUrl, "POST") {
                uploadHandler = new UploadHandlerRaw(bytes) { contentType = mime },
                downloadHandler = new DownloadHandlerBuffer(),
            };
            if (!string.IsNullOrEmpty(uploadSecret)) {
                req.SetRequestHeader("Authorization", "Bearer " + uploadSecret);
            }
            req.SetRequestHeader("Content-Type", mime);

            pendingSourcePath = previewImagePath;
            pendingRequest = req.SendWebRequest();
        }

        private static string GuessMimeType(string path) {
            string lower = path.ToLowerInvariant();
            if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg")) {
                return "image/jpeg";
            }
            if (lower.EndsWith(".webp")) {
                return "image/webp";
            }
            return "image/png";
        }

        private static string ExtractUrl(string json) {
            if (string.IsNullOrEmpty(json)) {
                return null;
            }
            const string marker = "\"url\":\"";
            int start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) {
                return null;
            }
            start += marker.Length;
            int end = json.IndexOf('"', start);
            return end < 0 ? null : json.Substring(start, end - start);
        }
    }
}
