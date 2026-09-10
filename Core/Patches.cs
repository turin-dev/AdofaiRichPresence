using HarmonyLib;

namespace AdofaiRichPresence.Core {
    [HarmonyPatch(typeof(DiscordController), "UpdatePresence")]
    internal static class MuteBuiltInPresencePatch {
        internal static Settings Settings;

        private static bool Prefix() {
            return !(Settings?.MuteBuiltInPresence ?? false);
        }
    }
}
