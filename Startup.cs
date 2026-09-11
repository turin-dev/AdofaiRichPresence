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
            if (string.IsNullOrEmpty(settings.DiscordApplicationId)) {
                settings.DiscordApplicationId = DiscordConfig.DefaultApplicationId;
            }
            presenceManager = new PresenceManager(modEntry.Logger);

            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();
            MuteBuiltInPresencePatch.Settings = settings;
            RunFreezeState.Logger = modEntry.Logger;

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnUpdate = OnUpdate;
            modEntry.OnUnload = OnUnload;

            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled) {
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry) {
            settings.Draw(modEntry);
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
            presenceManager.Dispose();
            harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }
    }
}
