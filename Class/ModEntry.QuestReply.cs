// ModEntry.QuestReply.cs
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
    /// Board quest (ItemDeliveryQuest) completion: the game glues three random fragments into the first box,
    /// greeting (ItemDeliveryQuest.cs.13526–13528) + reaction (13530–13536) + thanks (13538–13540), and shows it
    /// with no translation key. Each fragment is its own pack entry; this matches the whole box with one regex in
    /// that order and plays the three recordings one after another. The payment box (13542–13544) is an ordinary line.
    /// </summary>
    public partial class ModEntry
    {
        public sealed class QuestReplyMatcher
        {
            public Regex Rx;
            public Dictionary<string, string> AudioByGroup = new();   // "g0" → assets/pl/…/n.ogg
        }

        private const string QuestFragmentPrefix = "Strings/StringsFromCSFiles:ItemDeliveryQuest.cs.";
        private static readonly string[][] QuestReplyGroups =
        {
            new[] { "13526", "13527", "13528" },                    // greeting
            new[] { "13530", "13532", "13534", "13535", "13536" },  // reaction
            new[] { "13538", "13539", "13540" },                    // thanks
        };
        private static readonly char[] QuestGroupLetter = { 'g', 'r', 't' };

        /// <summary>Collects fragment entries of one pack while it loads; Build() makes the matcher.</summary>
        internal sealed class QuestReplyBuilder
        {
            private readonly List<(string body, string audio)>[] parts = { new(), new(), new() };

            public void Add(string translationKey, string actorVariant, string audioPath)
            {
                if (translationKey == null || !translationKey.StartsWith(QuestFragmentPrefix, StringComparison.Ordinal)) return;
                string id = translationKey.Substring(QuestFragmentPrefix.Length);
                for (int g = 0; g < QuestReplyGroups.Length; g++)
                    if (QuestReplyGroups[g].Contains(id))
                    {
                        string screen = ScreenTextFromActor(actorVariant);
                        if (screen.Length > 0) parts[g].Add((RegexBody(screen), audioPath));
                    }
            }

            public QuestReplyMatcher Build()
            {
                if (parts.Any(p => p.Count == 0)) return null;
                var m = new QuestReplyMatcher();
                var sb = new StringBuilder("^");
                for (int g = 0; g < parts.Length; g++)
                {
                    if (g > 0) sb.Append(' ');
                    sb.Append("(?:");
                    for (int i = 0; i < parts[g].Count; i++)
                    {
                        string name = $"{QuestGroupLetter[g]}{i}";
                        if (i > 0) sb.Append('|');
                        sb.Append($"(?<{name}>{parts[g][i].body})");
                        m.AudioByGroup[name] = parts[g][i].audio;
                    }
                    sb.Append(')');
                }
                sb.Append('$');
                m.Rx = new Regex(sb.ToString(), RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
                return m;
            }
        }

        /// <summary>Relative audio paths (greeting, reaction, thanks) when the displayed box is a quest reply.</summary>
        private static List<string> MatchQuestReply(VoicePack pack, string displayed)
        {
            if (pack?.QuestReply == null || string.IsNullOrWhiteSpace(displayed)) return null;
            var m = pack.QuestReply.Rx.Match(ScreenKey(displayed));
            if (!m.Success) return null;
            var list = new List<string>();
            foreach (char letter in QuestGroupLetter)
            {
                var hit = pack.QuestReply.AudioByGroup.FirstOrDefault(kv => kv.Key[0] == letter && m.Groups[kv.Key].Success);
                if (hit.Value != null) list.Add(hit.Value);
            }
            return list;
        }

        private bool TryPlayQuestReply(VoicePack pack, string displayed, string tag)
        {
            var rels = MatchQuestReply(pack, displayed);
            if (rels == null) return false;
            if (Config.developerModeOn)
                Monitor.Log($"[V2-QUESTREPLY] {tag} character='{pack.Character}' parts={string.Join(" + ", rels)} text='{displayed}'", LogLevel.Debug);
            var full = rels.Select(r => PathUtilities.NormalizePath(Path.Combine(pack.BaseAssetPath, r))).Where(File.Exists).ToList();
            PlayVoiceQueue(full);
            return true;
        }
    }
}
