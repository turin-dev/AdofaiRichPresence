namespace AdofaiRichPresence {
    internal static class DiscordConfig {
        // Ship the mod with a working default so end users don't have to create
        // their own Discord Application. Fill this in once you have a real ID
        // (discord.com/developers/applications) — the "Advanced" settings panel
        // still lets anyone override it.
        internal const string DefaultApplicationId = "1547603976663605369";

        internal const string DefaultLargeImageKey = "logo";
        internal const string DefaultLargeImageKeyPaused = "";
        internal const string DefaultLargeImageKeyMenu = "";
        internal const string DefaultLargeImageKeyEditor = "";
        internal const string DefaultSmallImageKeyPlaying = "";

        internal const string DefaultCdnUploadUrl = "https://cdn.adofai.turin.my/upload";
        internal const string DefaultCdnUploadSecret = "";
    }
}
