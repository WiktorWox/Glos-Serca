// ModEntry.TemplateMatch.cs
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VoiceOverFrameworkMod
{
    /// <summary>
    /// Matching lines whose on-screen text contains values the game fills in: the player's name ({Farmer_Name}),
    /// game tokens (%farm, %pet, %kid1, %book, %year…) and format inserts ({0}). The pack's actor text keeps those
    /// markers, so each such line becomes a regex with "any text" in their place and literal text elsewhere. Nothing
    /// is cut out of the displayed text first, so a player called "Ola" or a farm called "Las" can't damage other
    /// words, and no word boundaries are involved (works for CJK text too).
    /// </summary>
    public partial class ModEntry
    {
        public sealed class TemplateLine
        {
            public Regex Rx;
            public string AudioPath;
            public int FixedLetters;
        }

        private static readonly Regex TemplateMarkerRx = new(
            @"\{Farmer_Name\}|@|\{\d+\}|%(?:adj|noun|place|name|spouse|firstnameletter|farm|favorite|kid1|kid2|pet|band|book|season|time|year)(?![A-Za-z])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TemplatePortraitRx = new(@"\s*\{Portrait:[^}]*\}", RegexOptions.Compiled);

        /// <summary>Lines with less fixed text than this are too generic to identify ("{Farmer_Name}!").</summary>
        private const int TemplateMinFixedLetters = 5;

        /// <summary>Regex for one actor text, or null when it has no variable part.</summary>
        internal static TemplateLine BuildTemplate(string actorText, string audioPath)
        {
            if (string.IsNullOrWhiteSpace(actorText)) return null;
            string text = TemplatePortraitRx.Replace(actorText, "");
            if (!TemplateMarkerRx.IsMatch(text)) return null;

            var sb = new StringBuilder("^\\s*");
            int letters = 0;
            string[] parts = TemplateMarkerRx.Split(text);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append(".*?");
                string part = Regex.Replace(parts[i], @"\s+", " ");
                letters += part.Count(char.IsLetter);
                // literal text; whitespace flexible (the game wraps lines, a removed value may leave a space or not)
                sb.Append(string.Join("\\s*", part.Split(' ').Select(Regex.Escape)));
            }
            sb.Append("\\s*$");
            if (letters < TemplateMinFixedLetters) return null;
            return new TemplateLine
            {
                Rx = new Regex(sb.ToString(), RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant),
                AudioPath = audioPath,
                FixedLetters = letters
            };
        }

        /// <summary>Relative audio path of the pack line whose template matches the displayed text (most fixed text wins).</summary>
        private string MatchTemplate(VoicePack pack, string displayed)
        {
            if (pack?.TemplateEntries == null || pack.TemplateEntries.Count == 0 || string.IsNullOrWhiteSpace(displayed))
                return null;
            string text = Regex.Replace(displayed, @"\s+", " ").Trim();
            TemplateLine best = null;
            foreach (var t in pack.TemplateEntries)
                if ((best == null || t.FixedLetters > best.FixedLetters) && t.Rx.IsMatch(text))
                    best = t;
            return best?.AudioPath;
        }

        /// <summary>Play the template-matched line. True when a line matched (even if its audio is not recorded yet).</summary>
        private bool TryPlayTemplate(VoicePack pack, string displayed, string tag)
        {
            string rel = MatchTemplate(pack, displayed);
            if (Config.developerModeOn)
                Monitor.Log($"[V2-TEMPLATE] {tag} character='{pack?.Character}' match={(rel ?? "no")} text='{displayed}'", LogLevel.Debug);
            if (string.IsNullOrWhiteSpace(rel)) return false;
            string full = PathUtilities.NormalizePath(Path.Combine(pack.BaseAssetPath, rel));
            if (File.Exists(full))
                PlayVoiceFromFile(full);
            return true;
        }
    }
}
