using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VoiceOverFrameworkMod
{
    /// <summary>
    /// Template sources added after the original generator: location entry lines (Characters/Dialogue
    /// &lt;Location&gt;_Entry), schedule lines (Strings/schedules/&lt;npc&gt;) and quest completion lines (Data/Quests).
    /// All three are appended after every older source, so the numbers of entries that were already recorded
    /// do not move.
    /// </summary>
    public partial class ModEntry : Mod
    {
        /// <summary>
        /// NPC.arriveAt shows one random '/'-separated variant of a "&lt;Location&gt;_Entry" line as a speech bubble.
        /// Returns the variants for such a key, or null for any other key.
        /// </summary>
        private static List<string> SplitLocationEntryVariants(string key, string raw)
        {
            if (string.IsNullOrEmpty(key) || !key.EndsWith("_Entry", StringComparison.Ordinal) || string.IsNullOrEmpty(raw))
                return null;
            return raw.Split('/').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
        }

        private VoiceEntryTemplate MakeEntry(string characterName, string languageCode, string ext, ref int entryNumber,
                                             string from, string translationKey, int pageIndex, PageSeg seg)
        {
            string file = $"{entryNumber}{(string.IsNullOrEmpty(seg.Gender) ? "" : "_" + seg.Gender)}.{ext}";
            entryNumber++;
            return new VoiceEntryTemplate
            {
                DialogueFrom = from,
                DialogueText = seg.Actor,
                AudioPath = Path.Combine("assets", languageCode, characterName, file).Replace('\\', '/'),
                TranslationKey = translationKey,
                PageIndex = pageIndex,
                DisplayPattern = seg.Display,
                GenderVariant = seg.Gender
            };
        }

        /// <summary>
        /// Variants 2..n of every "&lt;Location&gt;_Entry" line. Variant 1 keeps its old slot in
        /// BuildFromCharacterDialogue; each variant is its own page of the same TranslationKey.
        /// </summary>
        private IEnumerable<VoiceEntryTemplate> BuildFromLocationEntryVariants(string characterName, string languageCode, IGameContentHelper content, ref int entryNumber, string ext)
        {
            var outList = new List<VoiceEntryTemplate>();
            Dictionary<string, string> sheet = null;
            try { sheet = content.Load<Dictionary<string, string>>($"Characters/Dialogue/{characterName}{AssetLangSuffix(languageCode)}"); }
            catch (ContentLoadException) { }
            if (sheet == null) return outList;

            foreach (var kv in sheet)
            {
                var variants = SplitLocationEntryVariants(kv.Key?.Trim(), kv.Value);
                if (variants == null) continue;
                string key = kv.Key.Trim();
                for (int v = 1; v < variants.Count; v++)
                    foreach (var seg in DialogueUtil.SplitAndSanitize(variants[v]))
                        outList.Add(MakeEntry(characterName, languageCode, ext, ref entryNumber, key, $"Characters/Dialogue/{characterName}:{key}", v, seg));
            }
            return outList;
        }

        /// <summary>
        /// Lines an NPC says at the end of a schedule route (Strings/schedules/&lt;npc&gt;:&lt;key&gt;), shown in a
        /// normal dialogue box when the player talks to them there.
        /// </summary>
        private IEnumerable<VoiceEntryTemplate> BuildFromScheduleStrings(string characterName, string languageCode, IGameContentHelper content, ref int entryNumber, string ext)
        {
            var outList = new List<VoiceEntryTemplate>();
            Dictionary<string, string> sheet = null;
            try { sheet = content.Load<Dictionary<string, string>>($"Strings/schedules/{characterName}{AssetLangSuffix(languageCode)}"); }
            catch (ContentLoadException) { }
            if (sheet == null) return outList;

            foreach (var kv in sheet)
            {
                string key = kv.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(kv.Value)) continue;
                foreach (var seg in DialogueUtil.SplitAndSanitize(kv.Value))
                    outList.Add(MakeEntry(characterName, languageCode, ext, ref entryNumber, key, $"Strings/schedules/{characterName}:{key}", seg.PageIndex, seg));
            }
            return outList;
        }

        /// <summary>
        /// What the NPC says when a story quest is completed: field 9 of ItemDelivery, LostItem and
        /// SecretLostItem quests in Data/Quests, where field 4 starts with the NPC's name.
        /// </summary>
        private IEnumerable<VoiceEntryTemplate> BuildFromQuestCompletions(string characterName, string languageCode, IGameContentHelper content, ref int entryNumber, string ext)
        {
            var outList = new List<VoiceEntryTemplate>();
            Dictionary<string, string> quests = null;
            try { quests = content.Load<Dictionary<string, string>>($"Data/Quests{AssetLangSuffix(languageCode)}"); }
            catch (ContentLoadException) { }
            if (quests == null) return outList;

            foreach (var kv in quests)
            {
                string[] fields = (kv.Value ?? string.Empty).Split('/');
                if (fields.Length < 10 || string.IsNullOrWhiteSpace(fields[9])) continue;
                string type = fields[0].Trim();
                if (type != "ItemDelivery" && type != "LostItem" && type != "SecretLostItem") continue;
                string target = fields[4].Trim().Split(' ')[0];
                if (!target.Equals(characterName, StringComparison.OrdinalIgnoreCase)) continue;

                string key = kv.Key.Trim();
                foreach (var seg in DialogueUtil.SplitAndSanitize(fields[9]))
                    outList.Add(MakeEntry(characterName, languageCode, ext, ref entryNumber, $"Quest_{key}", $"Data/Quests:{key}", seg.PageIndex, seg));
            }
            return outList;
        }
    }
}
