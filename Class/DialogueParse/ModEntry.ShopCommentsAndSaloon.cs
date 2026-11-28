using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VoiceOverFrameworkMod
{
    public partial class ModEntry : Mod
    {
        /// <summary>
        /// Comments on an item the player sold to a shop, said the next day by a random villager who "bought" it
        /// (Game1.UpdateShopPlayerItemInventory → ShopLocation / FishShop.getPurchasedItemDialogueForNPC).
        /// Who: Data/Characters CanCommentOnPurchasedShopItems, else HomeRegion "Town"; social; not a child.
        /// Pierre's shop: Abigail, Caroline, Pierre, Haley, Elliott, Alex and Leah always say their own line; rude NPCs only
        /// the _3_…_Rude lines; teens also PurchasedItem_Teen; Jodi her _2_QualityHigh_Jodi; _3_NonRude is never shown
        /// (the game drops that dialogue). Willy's shop: the …_Willy lines for everyone, Willy himself only
        /// PurchasedItem_Pierre_…_Willy. The name in a key is the shop or the speaker, so these keys are not matched by name
        /// in GetExtraDialogueForCharacter.
        /// </summary>
        private IEnumerable<VoiceEntryTemplate> BuildFromPurchasedItemComments(string characterName, string languageCode, IGameContentHelper content, ref int entryNumber, string ext)
        {
            var outList = new List<VoiceEntryTemplate>();
            if (!Game1.characterData.TryGetValue(characterName, out CharacterData data) || data == null) return outList;
            bool canComment = data.CanCommentOnPurchasedShopItems ?? data.HomeRegion == "Town";
            bool social = !string.Equals(data.CanSocialize?.Trim(), "FALSE", StringComparison.OrdinalIgnoreCase);
            if (!canComment || !social || data.Age == NpcAge.Child) return outList;

            Dictionary<string, string> sheet;
            try { sheet = content.Load<Dictionary<string, string>>("Data/ExtraDialogue" + AssetLangSuffix(languageCode)); }
            catch (ContentLoadException) { return outList; }
            int n = entryNumber;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Add(string key)
            {
                if (!seen.Add(key) || !sheet.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text)) return;
                foreach (var seg in DialogueUtil.SplitAndSanitize(text))
                    outList.Add(MakeEntry(characterName, languageCode, ext, ref n, key, $"Data/ExtraDialogue:{key}", seg.PageIndex, seg));
            }
            bool rude = data.Manner == NpcManner.Rude;
            bool jodi = characterName == "Jodi";

            // Pierre's shop (ShopLocation)
            var own = new Dictionary<string, string[]>
            {
                ["Abigail"] = new[] { "PurchasedItem_Abigail_QualityLow", "PurchasedItem_Abigail_QualityHigh" },
                ["Caroline"] = new[] { "PurchasedItem_Caroline_QualityLow", "PurchasedItem_Caroline_QualityHigh" },
                ["Pierre"] = new[] { "PurchasedItem_Pierre_QualityLow", "PurchasedItem_Pierre_QualityHigh" },
                ["Haley"] = new[] { "PurchasedItem_Haley" }, ["Elliott"] = new[] { "PurchasedItem_Elliott" },
                ["Alex"] = new[] { "PurchasedItem_Alex" }, ["Leah"] = new[] { "PurchasedItem_Leah" },
            };
            if (own.TryGetValue(characterName, out var ownKeys))
                foreach (var k in ownKeys) Add(k);
            else
            {
                if (rude) { Add("PurchasedItem_3_QualityHigh_Rude"); Add("PurchasedItem_3_QualityLow_Rude"); }
                else
                {
                    Add("PurchasedItem_1_QualityLow"); Add("PurchasedItem_1_QualityHigh");
                    Add(jodi ? "PurchasedItem_2_QualityHigh_Jodi" : "PurchasedItem_2_QualityHigh"); Add("PurchasedItem_2_QualityLow");
                    Add("PurchasedItem_4");
                    Add("PurchasedItem_5_VegetableOrFruit"); Add("PurchasedItem_5_Cooking"); Add("PurchasedItem_5_Foraged");
                }
                if (data.Age == NpcAge.Teen) Add("PurchasedItem_Teen");
            }

            // Willy's shop (FishShop)
            if (characterName == "Willy") { Add("PurchasedItem_Pierre_QualityLow_Willy"); Add("PurchasedItem_Pierre_QualityHigh_Willy"); }
            else if (rude) { Add("PurchasedItem_3_QualityHigh_Rude_Willy"); Add("PurchasedItem_3_QualityLow_Rude_Willy"); }
            else
            {
                Add("PurchasedItem_1_QualityLow_Willy"); Add("PurchasedItem_1_QualityHigh_Willy");
                Add(jodi ? "PurchasedItem_2_QualityHigh_Jodi_Willy" : "PurchasedItem_2_QualityHigh_Willy"); Add("PurchasedItem_2_QualityLow_Willy");
                Add("PurchasedItem_3_NonRude_Willy"); Add("PurchasedItem_4_Willy");
            }
            entryNumber = n;
            return outList;
        }

        private static readonly Regex SaloonTvSpotRx = new(@"(?:^|/)\s*a?\d{3,4} Saloon (\d+) (\d+)", RegexOptions.Compiled);

        /// <summary>
        /// Cheers in the saloon's sports room (GameLocation: every 7th day 12:00–15:00 once "saloonSportsRoom" is open, NPCs
        /// standing at X &gt; 26, Y &lt; 12 shout Strings/Characters:Saloon_good/bad/neutralEvent_0–4 in a bubble). Only NPCs
        /// whose schedule puts them there get them (vanilla: Alex, George, Gus, Kent, Shane).
        /// </summary>
        private IEnumerable<VoiceEntryTemplate> BuildFromSaloonCheers(string characterName, string languageCode, IGameContentHelper content, ref int entryNumber, string ext)
        {
            var outList = new List<VoiceEntryTemplate>();
            Dictionary<string, string> schedule = null, chars = null;
            try { schedule = content.Load<Dictionary<string, string>>($"Characters/schedules/{characterName}"); } catch (ContentLoadException) { }
            if (schedule == null || !schedule.Values.Any(v => SaloonTvSpotRx.Matches(v ?? "").Any(m =>
                    int.Parse(m.Groups[1].Value) > 26 && int.Parse(m.Groups[2].Value) < 12)))
                return outList;
            try { chars = content.Load<Dictionary<string, string>>("Strings/Characters" + AssetLangSuffix(languageCode)); } catch (ContentLoadException) { }
            if (chars == null) return outList;
            int n = entryNumber;
            foreach (var kind in new[] { "goodEvent", "badEvent", "neutralEvent" })
                for (int i = 0; i < 5; i++)
                {
                    string key = $"Saloon_{kind}_{i}";
                    if (!chars.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text)) continue;
                    text = Regex.Replace(text, @"\s+", " ").Trim();   // bubble: shown as-is
                    outList.Add(MakeEntry(characterName, languageCode, ext, ref n, key, $"Strings/Characters:{key}", 0,
                        new PageSeg { Actor = text, Display = text, PageIndex = 0 }));
                }
            entryNumber = n;
            return outList;
        }
    }
}
