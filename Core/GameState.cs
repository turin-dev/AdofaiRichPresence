using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AdofaiRichPresence.Core {
    internal enum GameMode {
        MainMenu,
        LevelSelect,
        Playing,
        Paused,
        Dead,
        Cleared,
        Editor,
    }

    internal struct GameSnapshot {
        public GameMode Mode;
        public string LevelName;
        public string Artist;
        public string Author;
        public int Difficulty;
        public float Bpm;
        public float Progress;
        public int RemainingTiles;
        public int TotalTiles;
        public float ElapsedSeconds;
        public float TotalSeconds;
        public string PreviewImagePath;
        public string WorkshopId;
        public float Accuracy;
        public float XAccuracy;
        public bool HasAccuracy;
        public bool HasXAccuracy;
        public int CheckpointsUsed;
        public bool HasCheckpointUsage;
        public int PerfectCount;
        public int EarlyCount;
        public int LateCount;
    }

    internal static class GameState {
        private const float MaxPresenceDurationSeconds = 7f * 24f * 60f * 60f;

        internal static GameSnapshot Capture() {
            GameSnapshot snap = new GameSnapshot();
            snap.Mode = CaptureMode();

            scnGame game = SafeGet(() => scnGame.instance) ?? SafeGet(() => ADOBase.customLevel);
            var data = game != null ? SafeGet(() => game.levelData) : null;
            if (data == null) {
                // Pure editing (not test-playing) never loads scnGame; the editor
                // scene carries its own levelData instead.
                scnEditor editor = SafeGet(() => scnEditor.instance);
                if (editor != null) {
                    data = SafeGet(() => editor.levelData);
                }
            }
            if (data != null) {
                snap.LevelName = StripTags(SafeGet(() => data.song));
                snap.Artist = StripTags(SafeGet(() => data.artist));
                snap.Author = StripTags(SafeGet(() => data.author));
                snap.Difficulty = SafeGet(() => data.difficulty);

                string levelPathForId = game != null ? SafeGet(() => game.levelPath) : null;
                if (string.IsNullOrEmpty(levelPathForId)) {
                    levelPathForId = SafeGet(() => ADOBase.levelPath);
                }
                if (!string.IsNullOrEmpty(levelPathForId)) {
                    Match m = WorkshopIdPattern.Match(levelPathForId);
                    if (m.Success) {
                        snap.WorkshopId = m.Groups[1].Value;
                    }
                }

                string imageFile = SafeGet(() => data.previewImage);
                if (string.IsNullOrEmpty(imageFile)) {
                    imageFile = SafeGet(() => data.previewIcon);
                }
                if (!string.IsNullOrEmpty(imageFile)) {
                    string folder = levelPathForId;
                    if (!string.IsNullOrEmpty(folder)) {
                        try {
                            if (File.Exists(folder)) {
                                folder = Path.GetDirectoryName(Path.GetFullPath(folder));
                            } else if (Directory.Exists(folder)) {
                                folder = Path.GetFullPath(folder);
                            } else {
                                // levelPath normally points at the .adofai file. Use its
                                // parent even when the file is temporarily unavailable.
                                folder = Path.GetDirectoryName(Path.GetFullPath(folder));
                            }
                            string baseFolder = EnsureTrailingSeparator(folder);
                            string fullPath = Path.GetFullPath(Path.Combine(folder ?? "", imageFile));
                            bool insideLevelFolder = !string.IsNullOrEmpty(baseFolder)
                                && fullPath.StartsWith(baseFolder, StringComparison.OrdinalIgnoreCase);
                            if (insideLevelFolder && IsSupportedImagePath(fullPath) && File.Exists(fullPath)) {
                                snap.PreviewImagePath = fullPath;
                            }
                        } catch {
                            // Bad path segment (custom level oddities); just skip the image.
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(snap.LevelName)) {
                // Boss encounters don't populate scnGame/levelData during their intro,
                // but scrController still carries the display title.
                scrController controllerForTitle = SafeGet(() => scrController.instance);
                if (controllerForTitle != null) {
                    string fallbackName = SafeGet(() => controllerForTitle.levelName);
                    if (string.IsNullOrEmpty(fallbackName)) {
                        fallbackName = SafeGet(() => controllerForTitle.originalLevelName);
                    }
                    snap.LevelName = StripTags(fallbackName);
                }
            }

            scrConductor conductor = SafeGet(() => scrConductor.instance);
            if (conductor != null) {
                snap.Bpm = SafeGet(() => conductor.bpm);
                var song = SafeGet(() => conductor.song);
                if (song != null) {
                    snap.ElapsedSeconds = RunFreezeState.IsFrozen ? RunFreezeState.FrozenElapsedSeconds : SafeGet(() => song.time);
                }
            }
            if (!IsFinitePositive(snap.Bpm) && data != null) {
                snap.Bpm = SafeGet(() => data.bpm);
            }

            scrController controller = SafeGet(() => scrController.instance);
            if (controller != null) {
                snap.CheckpointsUsed = Math.Max(0, SafeGet(() => scrController.checkpointsUsed));
                snap.HasCheckpointUsage = true;
                snap.Progress = NormalizeRatio(SafeGet(() => controller.percentComplete), 0f);
                if (RunFreezeState.IsCleared) {
                    snap.Accuracy = NormalizeRatio(RunFreezeState.FrozenAccuracy, 1f);
                    snap.XAccuracy = NormalizeRatio(RunFreezeState.FrozenXAccuracy, 1f);
                    snap.HasAccuracy = true;
                    snap.HasXAccuracy = true;
                    snap.PerfectCount = RunFreezeState.FrozenPerfectCount;
                    snap.EarlyCount = RunFreezeState.FrozenEarlyCount;
                    snap.LateCount = RunFreezeState.FrozenLateCount;
                } else {
                    var mistakes = SafeGet(() => controller.mistakesManager);
                    if (mistakes != null) {
                        snap.Accuracy = NormalizeRatio(SafeGet(() => mistakes.percentAcc), 1f);
                        snap.XAccuracy = NormalizeRatio(SafeGet(() => mistakes.percentXAcc), 1f);
                        snap.HasAccuracy = true;
                        snap.HasXAccuracy = true;
                    }
                }

                // ADOFAI levels routinely change tempo mid-level; the current tile's
                // actual BPM comes from consecutive floors' entry times, not the
                // level's starting BPM.
                scrFloor currFloor = SafeGet(() => controller.currFloor) ?? SafeGet(() => controller.firstFloor);
                scrFloor nextFloor = currFloor != null ? SafeGet(() => currFloor.nextfloor) : null;
                if (currFloor != null && nextFloor != null) {
                    double dt = SafeGet(() => nextFloor.entryTime) - SafeGet(() => currFloor.entryTime);
                    if (dt > 1e-9) {
                        float pitch = conductor != null ? SafeGet(() => conductor.song)?.pitch ?? 1f : 1f;
                        float liveBpm = (float)(60.0 / dt * pitch);
                        if (IsFinitePositive(liveBpm)) {
                            snap.Bpm = liveBpm;
                        }
                    }
                }
            }

            scrLevelMaker lm = SafeGet(() => scrLevelMaker.instance);
            if (lm != null) {
                var floors = SafeGet(() => lm.listFloors);
                if (floors != null && floors.Count > 0) {
                    snap.TotalTiles = floors.Count;
                    int current = controller != null ? SafeGet(() => controller.currentSeqID) : 0;
                    snap.RemainingTiles = Math.Max(0, snap.TotalTiles - current);
                    var last = floors[floors.Count - 1];
                    if (last != null) {
                        snap.TotalSeconds = (float)SafeGet(() => last.entryTime);
                    }
                }
            }

            // Unity/audio values can briefly become NaN or Infinity during scene
            // transitions. Keep invalid values out of Discord text and timestamps.
            snap.Bpm = NormalizePositive(snap.Bpm, 0f);
            snap.ElapsedSeconds = NormalizeNonNegative(snap.ElapsedSeconds, 0f);
            snap.TotalSeconds = NormalizePositiveDuration(snap.TotalSeconds, 0f);

            return snap;
        }

        private static GameMode CaptureMode() {
            try {
                if (ADOBase.isLevelEditor) {
                    return GameMode.Editor;
                }
                if (RunFreezeState.IsCleared) {
                    return GameMode.Cleared;
                }
                scrController controller = scrController.instance;
                if (controller != null && controller.gameworld) {
                    if (controller.paused || RunFreezeState.PauseMenuOpen) {
                        return GameMode.Paused;
                    }
                    if (RunFreezeState.IsFrozen) {
                        return GameMode.Dead;
                    }
                    return GameMode.Playing;
                }
                if (ADOBase.isLevelSelect) {
                    return GameMode.LevelSelect;
                }
            } catch {
                // Fall through to MainMenu below on any read failure (scene transition, etc).
            }
            return GameMode.MainMenu;
        }

        internal static string DebugState() {
            try {
                scrController controller = scrController.instance;
                if (controller == null) {
                    return "controller=null";
                }
                var conductor = SafeGet(() => scrConductor.instance);
                var song = conductor != null ? SafeGet(() => conductor.song) : null;
                var game = SafeGet(() => scnGame.instance) ?? SafeGet(() => ADOBase.customLevel);
                var data = game != null ? SafeGet(() => game.levelData) : null;
                string rawSong = data != null ? SafeGet(() => data.song) : null;
                string previewImage = data != null ? SafeGet(() => data.previewImage) : null;
                string previewIcon = data != null ? SafeGet(() => data.previewIcon) : null;
                string levelFolder = game != null ? SafeGet(() => game.levelPath) : null;
                if (string.IsNullOrEmpty(levelFolder)) {
                    levelFolder = SafeGet(() => ADOBase.levelPath);
                }
                return "gameNull=" + (game == null)
                    + " levelDataNull=" + (data == null)
                    + " previewImage='" + (previewImage ?? "<null>") + "'"
                    + " previewIcon='" + (previewIcon ?? "<null>") + "'"
                    + " levelFolder='" + (levelFolder ?? "<null>") + "'"
                    + " rawSong='" + (rawSong ?? "<null>") + "'"
                    + " strippedSong='" + StripTags(rawSong) + "'"
                    + " gameworld=" + controller.gameworld
                    + " paused=" + controller.paused
                    + " timeScale=" + Time.timeScale
                    + " audioListenerPause=" + AudioListener.pause
                    + " songIsPlaying=" + (song != null ? SafeGet(() => song.isPlaying).ToString() : "null")
                    + " pauseMenuNull=" + (SafeGet(() => controller.pauseMenu) == null)
                    + " pauseMenuOpen=" + RunFreezeState.PauseMenuOpen
                    + " frozen=" + RunFreezeState.IsFrozen
                    + " currentState=" + SafeGet(() => controller.currentState)
                    + " isLevelEditor=" + SafeGet(() => ADOBase.isLevelEditor)
                    + " isLevelSelect=" + SafeGet(() => ADOBase.isLevelSelect);
            } catch (Exception e) {
                return "debug 실패: " + e.Message;
            }
        }

        private static float NormalizeRatio(float value, float fallback) {
            if (float.IsNaN(value) || float.IsInfinity(value)) {
                return fallback;
            }
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static bool IsFinitePositive(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static float NormalizePositive(float value, float fallback) {
            return IsFinitePositive(value) ? value : fallback;
        }

        private static float NormalizePositiveDuration(float value, float fallback) {
            return IsFinitePositive(value) ? Math.Min(value, MaxPresenceDurationSeconds) : fallback;
        }

        private static float NormalizeNonNegative(float value, float fallback) {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) {
                return fallback;
            }
            return Math.Min(value, MaxPresenceDurationSeconds);
        }

        private static readonly Regex WorkshopIdPattern = new Regex(@"workshop[\\/]content[\\/]977950[\\/](\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TagPattern = new Regex("<.*?>", RegexOptions.Compiled);

        private static string StripTags(string s) {
            if (string.IsNullOrEmpty(s)) {
                return "";
            }
            return TagPattern.Replace(s, "").Trim();
        }

        private static bool IsSupportedImagePath(string path) {
            string lower = path.ToLowerInvariant();
            return lower.EndsWith(".png") || lower.EndsWith(".jpg")
                || lower.EndsWith(".jpeg") || lower.EndsWith(".webp");
        }

        private static string EnsureTrailingSeparator(string path) {
            if (string.IsNullOrEmpty(path)) {
                return "";
            }
            string separator = Path.DirectorySeparatorChar.ToString();
            return path.EndsWith(separator, StringComparison.Ordinal)
                ? path
                : path + separator;
        }

        private static T SafeGet<T>(Func<T> getter) {
            try {
                return getter();
            } catch {
                return default(T);
            }
        }
    }
}
