using System;
using AdofaiRichPresence.Core;
using HarmonyLib;
using UnityModManagerNet;

namespace AdofaiRichPresence {
    internal static class Startup {
        private static Settings settings;
        private static PresenceManager presenceManager;
        private static Harmony harmony;
        private static DateTime nextCallbackErrorLogUtc;

        public static bool Load(UnityModManager.ModEntry modEntry) {
            nextCallbackErrorLogUtc = DateTime.MinValue;
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
            MuteBuiltInPresencePatch.Settings = enabled && settings != null && settings.EnableDiscord ? settings : null;
            RunFreezeState.Reset();
            if (!enabled) {
                presenceManager?.Stop();
            }
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry) {
            try {
                settings?.Draw(modEntry, presenceManager);
            } catch (Exception e) {
                LogCallbackError(modEntry, "설정 화면 갱신 실패", e);
            }
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry) {
            try {
                settings?.Save(modEntry);
            } catch (Exception e) {
                LogCallbackError(modEntry, "설정 저장 실패", e);
            }
        }

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime) {
            try {
                if (!modEntry.Active || settings == null || presenceManager == null) {
                    return;
                }
                RunFreezeState.DebugLogging = settings.DebugLogging;
                presenceManager.Tick(settings, deltaTime);
            } catch (Exception e) {
                LogCallbackError(modEntry, "상태 갱신 실패", e);
                presenceManager?.RequestReconnect();
            }
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
