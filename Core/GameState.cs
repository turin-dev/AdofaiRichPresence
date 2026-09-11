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
    }

    internal static class GameState {
        internal static GameSnapshot Capture() {
            GameSnapshot snap = new GameSnapshot();
            snap.Mode = CaptureMode();

            scnGame game = SafeGet(() => scnGame.instance) ?? SafeGet(() => ADOBase.customLevel);
            if (game != null) {
                var data = SafeGet(() => game.levelData);
                if (data != null) {
                    snap.LevelName = StripTags(SafeGet(() => data.song));
                    snap.Artist = StripTags(SafeGet(() => data.artist));
                    snap.Author = StripTags(SafeGet(() => data.author));
                    snap.Difficulty = SafeGet(() => data.difficulty);

                    string levelPathForId = SafeGet(() => game.levelPath);
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
                        string folder = SafeGet(() => game.levelPath);
                        if (string.IsNullOrEmpty(folder)) {
                            folder = SafeGet(() => ADOBase.levelPath);
                        }
                        if (!string.IsNullOrEmpty(folder)) {
                            try {
                                // levelPath points at the .adofai file itself, not its folder.
                                if (File.Exists(folder)) {
                                    folder = Path.GetDirectoryName(folder);
                                }
                                string fullPath = Path.Combine(folder ?? "", imageFile);
                                if (File.Exists(fullPath)) {
                                    snap.PreviewImagePath = fullPath;
                                }
                            } catch {
                                // Bad path segment (custom level oddities); just skip the image.
                            }
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

            scrController controller = SafeGet(() => scrController.instance);
            if (controller != null) {
                snap.Progress = SafeGet(() => controller.percentComplete);
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

            return snap;
        }

        private static GameMode CaptureMode() {
            try {
                if (ADOBase.isLevelEditor) {
                    return GameMode.Editor;
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

        private static readonly Regex WorkshopIdPattern = new Regex(@"workshop[\\/]content[\\/]977950[\\/](\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TagPattern = new Regex("<.*?>", RegexOptions.Compiled);

        private static string StripTags(string s) {
            if (string.IsNullOrEmpty(s)) {
                return "";
            }
            return TagPattern.Replace(s, "").Trim();
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
