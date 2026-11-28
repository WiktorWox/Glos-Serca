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
            /// <summary>Short line ("{Farmer_Name}..."): the name parts must be the current player's name.</summary>
            public bool NameMustMatch;
        }

        // Values the game fills in. {Farm_Name} appears in speech bubbles (generator wrote the game's {1} that way).
        private static readonly Regex TemplateMarkerRx = new(
            @"\{Farmer_Name\}|\{Farm_Name\}|@|\{\d+\}|%(?:adj|noun|place|name|spouse|firstnameletter|farm|favorite|kid1|kid2|pet|band|book|season|time|year)(?![A-Za-z])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TemplatePortraitRx = new(@"\s*\{Portrait:[^}]*\}", RegexOptions.Compiled);
        // Things the actor text keeps but the game never shows: gift item lists "[195 215]", %noturn, %fork…, $k,
        // %revealtaste<Npc><item> (older templates left "Emily207" after it).
        private static readonly Regex ActorOnlyRx = new(
            @"\[[^\]]+\]|%fork\d*|%noturn|%revealtaste\w*|\s*\$k\b|(?<=[.!?])\s*[A-Z][a-z]+\d+\s*$",
            RegexOptions.Compiled);

        /// <summary>
        /// Lines with less fixed text than this ("{Farmer_Name}!", "Hej, {Farmer_Name}.") would match almost anything,
        /// so their name parts must equal the player's actual name instead of "any text".
        /// </summary>
        private const int TemplateMinFixedLetters = 5;
        private static readonly Regex NameMarkerRx = new(@"^(?:\{Farmer_Name\}|@)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FirstLetterMarkerRx = new(@"^%firstnameletter$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>What the game shows for an actor text: portraits and actor-only tokens removed, whitespace as one space.</summary>
        internal static string ScreenTextFromActor(string actorText)
        {
            string t = TemplatePortraitRx.Replace(actorText ?? string.Empty, "");
            t = ActorOnlyRx.Replace(t, "");
            return Regex.Replace(t, @"\s+", " ").Trim();
        }

        internal static bool HasVariablePart(string screenText) => TemplateMarkerRx.IsMatch(screenText ?? string.Empty);

        /// <summary>Regex for one actor text, or null when it has no variable part.</summary>
        internal static TemplateLine BuildTemplate(string actorText, string audioPath)
        {
            string text = ScreenTextFromActor(actorText);
            if (string.IsNullOrWhiteSpace(text) || !HasVariablePart(text)) return null;

            string[] parts = TemplateMarkerRx.Split(text);
            var markers = TemplateMarkerRx.Matches(text).Select(m => m.Value).ToList();
            int letters = parts.Sum(p => p.Count(char.IsLetter));
            bool shortLine = letters < TemplateMinFixedLetters;
            // A short line is only usable when it names the player: then the name itself identifies it.
            if (shortLine && !markers.Any(m => NameMarkerRx.IsMatch(m) || FirstLetterMarkerRx.IsMatch(m)))
                return null;

            var sb = new StringBuilder("^");
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    string m = markers[i - 1];
                    if (shortLine && NameMarkerRx.IsMatch(m)) sb.Append("(?<name>.+?)");
                    else if (shortLine && FirstLetterMarkerRx.IsMatch(m)) sb.Append("(?<first>.+?)");
                    else sb.Append(".+?");          // the value the game puts in: at least one character
                }
                sb.Append(Regex.Escape(parts[i]).Replace("\\ ", " "));   // literal text, spaces literal (both sides normalized)
            }
            sb.Append('$');
            return new TemplateLine
            {
                Rx = new Regex(sb.ToString(), RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant),
                AudioPath = audioPath,
                FixedLetters = letters,
                NameMustMatch = shortLine
            };
        }

        /// <summary>Regex for a screen text without anchors: literal text, ".+?" for each game value.</summary>
        internal static string RegexBody(string screenText)
        {
            var sb = new StringBuilder();
            string[] parts = TemplateMarkerRx.Split(screenText);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append(".+?");
                sb.Append(Regex.Escape(parts[i]).Replace("\\ ", " "));
            }
            return sb.ToString();
        }

        /// <summary>For short lines: every name part is the player's name, every %firstnameletter part a start of it.</summary>
        private static bool NamePartsAreThePlayer(Match m)
        {
            string name = StardewValley.Game1.player?.Name;
            if (string.IsNullOrEmpty(name)) return false;
            foreach (System.Text.RegularExpressions.Capture c in m.Groups["name"].Captures)
                if (!string.Equals(c.Value, name, StringComparison.Ordinal)) return false;
            foreach (System.Text.RegularExpressions.Capture c in m.Groups["first"].Captures)
                if (!name.StartsWith(c.Value, StringComparison.Ordinal)) return false;
            return true;
        }

        private static readonly (Regex rx, string note)[] ActorNoteRules =
        {
            (new Regex(@"\{Farmer_Name\}"), "{Farmer_Name} – imię gracza (wstawia gra)."),
            (new Regex(@"\{Farm_Name\}|%farm(?![A-Za-z])", RegexOptions.IgnoreCase), "%farm / {Farm_Name} – nazwa farmy gracza (wstawia gra)."),
            (new Regex(@"%pet(?![A-Za-z])", RegexOptions.IgnoreCase), "%pet – imię zwierzaka gracza (wstawia gra)."),
            (new Regex(@"%kid[12](?![A-Za-z])", RegexOptions.IgnoreCase), "%kid1 / %kid2 – imię pierwszego / drugiego dziecka gracza (wstawia gra)."),
            (new Regex(@"%spouse(?![A-Za-z])", RegexOptions.IgnoreCase), "%spouse – imię współmałżonka gracza (wstawia gra)."),
            (new Regex(@"%time(?![A-Za-z])", RegexOptions.IgnoreCase), "%time – aktualna godzina w grze, np. „06:20” (wstawia gra)."),
            (new Regex(@"%season(?![A-Za-z])", RegexOptions.IgnoreCase), "%season – bieżąca pora roku (wstawia gra)."),
            (new Regex(@"%year(?![A-Za-z])", RegexOptions.IgnoreCase), "%year – liczba lat / numer roku (wstawia gra)."),
            (new Regex(@"%book(?![A-Za-z])", RegexOptions.IgnoreCase), "%book – losowy tytuł książki (wstawia gra)."),
            (new Regex(@"%band(?![A-Za-z])", RegexOptions.IgnoreCase), "%band – losowa nazwa zespołu (wstawia gra)."),
            (new Regex(@"%(?:adj|noun|place|name)(?![A-Za-z])", RegexOptions.IgnoreCase), "%adj / %noun / %place / %name – losowe słowo: przymiotnik, rzeczownik, miejsce, imię (wstawia gra)."),
            (new Regex(@"%firstnameletter(?![A-Za-z])", RegexOptions.IgnoreCase), "%firstnameletter – pierwsza część imienia gracza (wstawia gra)."),
            (new Regex(@"%favorite(?![A-Za-z])", RegexOptions.IgnoreCase), "%favorite – ulubiona rzecz gracza podana przy tworzeniu postaci (wstawia gra)."),
            (new Regex(@"\{\d+\}"), "{0}, {1}… – wartość wstawiana przez grę, np. nazwa przedmiotu albo liczba."),
            (new Regex(@"\*[^*]+\*"), "*…* – opis odgłosu albo czynności, nie słowo do przeczytania."),
            (new Regex(@"\[[\d ]+\]"), "[…] – numery przedmiotów, które postać daje graczowi; nie czytać."),
        };

        /// <summary>Generated note for the recording lists: what the game tokens in an actor text mean (no wording advice).</summary>
        internal static string BuildActorNote(string actorText)
        {
            if (string.IsNullOrWhiteSpace(actorText)) return null;
            var notes = ActorNoteRules.Where(r => r.rx.IsMatch(actorText)).Select(r => r.note).ToList();
            return notes.Count == 0 ? null : string.Join(" ", notes);
        }

        /// <summary>Displayed text in the form pack keys use: line breaks and runs of spaces as one space.</summary>
        internal static string ScreenKey(string displayed) => Regex.Replace(displayed ?? string.Empty, @"\s+", " ").Trim();

        /// <summary>
        /// The one text lookup: exact screen text (lines without variables), then the templates (lines with variables).
        /// </summary>
        private string MatchLine(VoicePack pack, string displayed)
        {
            if (pack == null || string.IsNullOrWhiteSpace(displayed)) return null;
            string key = ScreenKey(displayed);
            if (pack.Entries != null && pack.Entries.TryGetValue(key, out var rel) && !string.IsNullOrWhiteSpace(rel))
                return rel;
            return MatchTemplate(pack, displayed);
        }

        /// <summary>Relative audio path of the pack line whose template matches the displayed text (most fixed text wins).</summary>
        private string MatchTemplate(VoicePack pack, string displayed)
        {
            if (pack?.TemplateEntries == null || pack.TemplateEntries.Count == 0 || string.IsNullOrWhiteSpace(displayed))
                return null;
            string text = ScreenKey(displayed);
            TemplateLine best = null;
            foreach (var t in pack.TemplateEntries)
            {
                if (best != null && t.FixedLetters <= best.FixedLetters) continue;
                var m = t.Rx.Match(text);
                if (m.Success && (!t.NameMustMatch || NamePartsAreThePlayer(m)))
                    best = t;
            }
            return best?.AudioPath;
        }

        /// <summary>Play the template-matched line. True when a line matched (even if its audio is not recorded yet).</summary>
        private bool TryPlayTemplate(VoicePack pack, string displayed, string tag)
        {
            if (TryPlayQuestReply(pack, displayed, tag)) return true;
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
