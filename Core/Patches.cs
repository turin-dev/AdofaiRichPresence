using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using UnityModManagerNet;

namespace AdofaiRichPresence.Core {
    [HarmonyPatch(typeof(DiscordController), "UpdatePresence")]
    internal static class MuteBuiltInPresencePatch {
        internal static Settings Settings;

        private static bool Prefix() {
            return !(Settings?.MuteBuiltInPresence ?? false);
        }
    }

    // Polling GameState every few seconds can miss a death entirely when ADOFAI
    // auto-restarts within that window, which made the presence jump straight to
    // 0:00 instead of freezing on death. Watching the state machine transition
    // directly catches every death/restart regardless of poll timing.
    [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
    internal static class TrackRunStatePatch {
        private static void Postfix(Enum newState) {
            if (!(newState is States state)) {
                return;
            }
            if (state == States.Fail || state == States.Fail2) {
                if (!RunFreezeState.IsFrozen) {
                    RunFreezeState.IsFrozen = true;
                    try {
                        var conductor = scrConductor.instance;
                        RunFreezeState.FrozenElapsedSeconds = conductor != null && conductor.song != null ? conductor.song.time : 0f;
                    } catch {
                        RunFreezeState.FrozenElapsedSeconds = 0f;
                    }
                }
            } else if (state == States.Won) {
                if (!RunFreezeState.IsCleared) {
                    RunFreezeState.IsCleared = true;
                    try {
                        var controller = scrController.instance;
                        var mistakes = controller?.mistakesManager;
                        RunFreezeState.FrozenAccuracy = mistakes != null && !float.IsNaN(mistakes.percentAcc) ? mistakes.percentAcc : 1f;
                        RunFreezeState.FrozenXAccuracy = mistakes != null && !float.IsNaN(mistakes.percentXAcc) ? mistakes.percentXAcc : 1f;
                    } catch {
                        RunFreezeState.FrozenAccuracy = 1f;
                        RunFreezeState.FrozenXAccuracy = 1f;
                    }
                }
            } else if (state == States.Start || state == States.Countdown || state == States.PlayerControl) {
                RunFreezeState.IsFrozen = false;
                RunFreezeState.IsCleared = false;
            }
        }
    }

    // scrController.paused and Time.timeScale both stay unchanged while ADOFAI's
    // pause menu is open, so poll-based detection never sees it. Hook the menu's
    // own Show/Hide calls instead.
    [HarmonyPatch(typeof(PauseMenu), "Show")]
    internal static class PauseMenuShowPatch {
        private static void Postfix() {
            RunFreezeState.PauseMenuOpen = true;
            if (RunFreezeState.DebugLogging) {
                RunFreezeState.Logger?.Log("[이벤트] PauseMenu.Show 호출됨");
            }
        }
    }

    [HarmonyPatch(typeof(PauseMenu), "Hide")]
    internal static class PauseMenuHidePatch {
        private static void Postfix() {
            RunFreezeState.PauseMenuOpen = false;
            if (RunFreezeState.DebugLogging) {
                RunFreezeState.Logger?.Log("[이벤트] PauseMenu.Hide 호출됨");
            }
        }
    }

    [HarmonyPatch(typeof(scrController), "TogglePauseGame")]
    internal static class TogglePauseGamePatch {
        private static void Postfix(bool __result) {
            RunFreezeState.PauseMenuOpen = __result;
            if (RunFreezeState.DebugLogging) {
                RunFreezeState.Logger?.Log("[이벤트] TogglePauseGame 호출됨, 결과=" + __result);
            }
        }
    }

    internal static class RunFreezeState {
        internal static bool IsFrozen;
        internal static float FrozenElapsedSeconds;
        internal static bool IsCleared;
        internal static float FrozenAccuracy;
        internal static float FrozenXAccuracy;
        internal static bool PauseMenuOpen;
        internal static UnityModManager.ModEntry.ModLogger Logger;
        internal static bool DebugLogging;
    }
}
