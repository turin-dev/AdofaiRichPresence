using AdofaiRichPresence.Core;
using HarmonyLib;
using UnityModManagerNet;

namespace AdofaiRichPresence {
    internal static class Startup {
        private static Settings settings;
        private static PresenceManager presenceManager;
        private static Harmony harmony;

        public static bool Load(UnityModManager.ModEntry modEntry) {
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
            MuteBuiltInPresencePatch.Settings = enabled && settings.EnableDiscord ? settings : null;
            if (!enabled) {
                presenceManager?.Stop();
            }
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry) {
            settings.Draw(modEntry, presenceManager);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry) {
            settings.Save(modEntry);
        }

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime) {
            if (!modEntry.Active) {
                return;
            }
            RunFreezeState.DebugLogging = settings.DebugLogging;
            presenceManager.Tick(settings, deltaTime);
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry) {
            MuteBuiltInPresencePatch.Settings = null;
            presenceManager?.Stop();
            harmony.UnpatchAll(modEntry.Info.Id);
            RunFreezeState.Reset();
            return true;
        }
    }
}
