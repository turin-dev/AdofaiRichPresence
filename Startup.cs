using System;
using System.Diagnostics;
using AdofaiRichPresence.Core;
using HarmonyLib;
using UnityModManagerNet;

namespace AdofaiRichPresence {
    internal static class Startup {
        private static Settings settings;
        private static PresenceManager presenceManager;
        private static Harmony harmony;
        private static DateTime nextCallbackErrorLogUtc;
        private static readonly Stopwatch callbackClock = Stopwatch.StartNew();
        private static readonly CallbackRecovery updateRecovery = new CallbackRecovery();

        public static bool Load(UnityModManager.ModEntry modEntry) {
            nextCallbackErrorLogUtc = DateTime.MinValue;
            updateRecovery.Reset();
            settings = Settings.Load<Settings>(modEntry);
            if (settings.EnableDiscord && string.IsNullOrEmpty(settings.DiscordApplicationId)) {
                settings.DiscordApplicationId = DiscordConfig.DefaultApplicationId;
            }
            presenceManager = new PresenceManager(modEntry.Logger);

            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();
            MuteBuiltInPresencePatch.Settings = settings;
            RunFreezeState.Reset();
            RunFreezeState.Logger = modEntry.Logger;

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnUpdate = OnUpdate;
            modEntry.OnUnload = OnUnload;

            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled) {
            // Keep the live settings reference while the mod is enabled. The
            // prefix evaluates EnableDiscord on every call, including after undo.
            MuteBuiltInPresencePatch.Settings = enabled ? settings : null;
            RunFreezeState.Reset();
            updateRecovery.Reset();
            if (enabled) {
                RunFreezeState.Logger = modEntry.Logger;
                RunFreezeState.DebugLogging = settings != null && settings.DebugLogging;
            }
            if (!enabled) {
                presenceManager?.Stop();
            }
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry) {
            try {
                settings?.Draw(modEntry, presenceManager);
            } catch (Exception e) {
                LogCallbackError(modEntry, "Failed to draw settings", e);
            }
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry) {
            try {
                settings?.Save(modEntry);
            } catch (Exception e) {
                LogCallbackError(modEntry, "Failed to save settings", e);
            }
        }

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime) {
            if (modEntry == null || !modEntry.Active || settings == null || presenceManager == null) {
                return;
            }
            updateRecovery.Run(callbackClock.Elapsed,
                () => {
                    RunFreezeState.DebugLogging = settings.DebugLogging;
                    presenceManager.Tick(settings, deltaTime);
                },
                () => presenceManager.Stop(),
                e => LogCallbackError(modEntry, "Activity update or cleanup failed (retry in 30 seconds)", e));
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry) {
            MuteBuiltInPresencePatch.Settings = null;
            presenceManager?.Stop();
            harmony.UnpatchAll(modEntry.Info.Id);
            RunFreezeState.Reset();
            return true;
        }

        private static void LogCallbackError(UnityModManager.ModEntry modEntry, string context, Exception error) {
            DateTime now = DateTime.UtcNow;
            if (now < nextCallbackErrorLogUtc) {
                return;
            }
            nextCallbackErrorLogUtc = now.AddSeconds(30);
            modEntry?.Logger?.Error(context + ": " + error.Message);
        }
    }
}
