using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using AdofaiRichPresence.Core;

internal static class LocalizationChecks {
    internal static void Run() {
        Require(Localization.NormalizeLanguage(null) == "ko", "legacy settings default");
        Require(Localization.NormalizeLanguage("invalid") == "ko", "invalid language fallback");
        Require(Localization.NormalizeLanguage(" EN ") == "en", "language normalization");
        Require(Localization.Text("en", "플레이 중") == "Playing", "English state");
        Require(Localization.Text("ko", "플레이 중") == "플레이 중", "Korean state");
        Require(Localization.Text("en", "unknown key") == "unknown key", "missing key fallback");
        Require(Localization.Format("en", "남은 {0}/{1} 타일", 580, 1000) == "580/1000 tiles left", "English word order");
        Require(Localization.Format("ko", "남은 {0}/{1} 타일", 580, 1000) == "남은 580/1000 타일", "Korean word order");
        var catalog = (Dictionary<string, string>)typeof(Localization)
            .GetField("English", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        foreach (var entry in catalog) {
            Require(!string.IsNullOrWhiteSpace(entry.Value) && !Regex.IsMatch(entry.Value, "[가-힣]"), "English translation: " + entry.Key);
            var koreanSlots = Regex.Matches(entry.Key, @"\{\d+\}").Select(m => m.Value).OrderBy(s => s);
            var englishSlots = Regex.Matches(entry.Value, @"\{\d+\}").Select(m => m.Value).OrderBy(s => s);
            Require(koreanSlots.SequenceEqual(englishSlots), "format placeholders: " + entry.Key);
            Localization.Format("en", entry.Key, 1, 2, 3);
        }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Settings.cs"))) root = root.Parent;
        Require(root != null, "source checkout for translation coverage");
        foreach (string file in new[] { "Settings.cs", "Core/PresenceManager.cs" }) {
            foreach (var line in File.ReadLines(Path.Combine(root.FullName, file))) {
                foreach (Match literal in Regex.Matches(line, "\"(?:\\\\.|[^\"\\\\])*\"")) {
                    string value = JsonSerializer.Deserialize<string>(literal.Value);
                    if (!Regex.IsMatch(value, "[가-힣]") || value == "한국어" || value == "언어 / Language") continue;
                    Require(catalog.ContainsKey(value), "missing translation in " + file + ": " + value);
                    Require(line.Contains("Text(") || line.Contains("ConnectionError(") || line.Contains("KoreanTabs ="),
                        "untranslated call site in " + file + ": " + value);
                }
            }
        }
        Console.WriteLine($"PASS: {catalog.Count} English translations, format placeholders and UI/presence coverage.");
    }

    private static void Require(bool condition, string label) {
        if (!condition) throw new Exception("Localization: " + label);
    }
}
