using System;

namespace AdofaiRichPresence.Core {
    internal enum GameMode {
        MainMenu,
        LevelSelect,
        Playing,
        Paused,
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
    }

    internal static class GameState {
        internal static GameSnapshot Capture() {
            GameSnapshot snap = new GameSnapshot();
            snap.Mode = CaptureMode();

            scnGame game = SafeGet(() => scnGame.instance);
            if (game != null) {
                var data = SafeGet(() => game.levelData);
                if (data != null) {
                    snap.LevelName = SafeGet(() => data.song) ?? "";
                    snap.Artist = SafeGet(() => data.artist) ?? "";
                    snap.Author = SafeGet(() => data.author) ?? "";
                    snap.Difficulty = SafeGet(() => data.difficulty);
                }
            }

            scrConductor conductor = SafeGet(() => scrConductor.instance);
            if (conductor != null) {
                snap.Bpm = SafeGet(() => conductor.bpm);
                var song = SafeGet(() => conductor.song);
                if (song != null) {
                    snap.ElapsedSeconds = SafeGet(() => song.time);
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
                    return controller.paused ? GameMode.Paused : GameMode.Playing;
                }
                if (ADOBase.isLevelSelect) {
                    return GameMode.LevelSelect;
                }
            } catch {
                // Fall through to MainMenu below on any read failure (scene transition, etc).
            }
            return GameMode.MainMenu;
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
