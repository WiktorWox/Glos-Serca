// ModEntry.MenuLines.cs
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VoiceOverFrameworkMod
{
    /// <summary>
    /// Character lines the game shows outside a normal NPC dialogue box:
    /// the shop owner's line next to the portrait in ShopMenu, and yes/no questions an NPC asks without
    /// being the dialogue speaker (Robin's upgrade offers). Matched by text in that
    /// character's pack only, so nothing plays unless the pack has exactly this line.
    /// </summary>
    public partial class ModEntry
    {
        /// <summary>Locations whose speakerless question boxes are spoken by one NPC.</summary>
        private static readonly Dictionary<string, string> SpeakerlessQuestionOwners = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ScienceHouse"] = "Robin",
        };

        private string _lastShopOwnerText;
        private string _lastSpeakerlessText;

        private void CheckForMenuLines()
        {
            // shop owner box
            if (Game1.activeClickableMenu is ShopMenu shop && !string.IsNullOrWhiteSpace(shop.potraitPersonDialogue))
            {
                if (!string.Equals(shop.potraitPersonDialogue, _lastShopOwnerText, StringComparison.Ordinal))
                {
                    _lastShopOwnerText = shop.potraitPersonDialogue;
                    foreach (string owner in ShopOwnerNames(shop.ShopId))
                        if (TryPlayCharacterLineByText(owner, shop.potraitPersonDialogue, "SHOP"))
                            break;
                }
            }
            else
                _lastShopOwnerText = null;

            // question box without a speaker, in a location owned by one NPC (Robin's upgrade offers)
            // (createQuestionDialogue leaves characterDialogue empty; Game1.currentSpeaker can still hold the previous NPC)
            if (Game1.activeClickableMenu is DialogueBox box && box.characterDialogue == null && box.isQuestion && !Game1.eventUp
                && Game1.currentLocation != null && SpeakerlessQuestionOwners.TryGetValue(Game1.currentLocation.Name, out var speaker))
            {
                string text = box.getCurrentString();
                if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, _lastSpeakerlessText, StringComparison.Ordinal))
                {
                    _lastSpeakerlessText = text;
                    // the question string keeps '^' (a line break in this box); the pack stores it as a space
                    TryPlayCharacterLineByText(speaker, text.Replace('^', ' '), "QUESTION");
                }
            }
            else
                _lastSpeakerlessText = null;
        }

        private static IEnumerable<string> ShopOwnerNames(string shopId)
        {
            if (string.IsNullOrWhiteSpace(shopId)) yield break;
            StardewValley.GameData.Shops.ShopData data = null;
            try { DataLoader.Shops(Game1.content).TryGetValue(shopId, out data); } catch { }
            foreach (var owner in data?.Owners ?? new List<StardewValley.GameData.Shops.ShopOwnerData>())
                if (!string.IsNullOrWhiteSpace(owner?.Name))
                    yield return owner.Name;
        }

        /// <summary>Play the line of <paramref name="character"/>'s pack whose display text equals <paramref name="displayed"/>.</summary>
        private bool TryPlayCharacterLineByText(string character, string displayed, string tag)
        {
            var pack = GetSelectedVoicePack(character);
            if (pack == null) return false;
            string farmer = Game1.player?.Name;
            string text = displayed;
            if (!string.IsNullOrEmpty(farmer)) text = text.Replace(farmer, "");
            string canon = CanonDisplay(text);
            string rel = null;
            // Pack display patterns come from DialogueUtil.SplitAndSanitize, which also strips token-like letter runs
            // (e.g. "pet" out of "tapet", "farm" out of "farmer"); try the plain text first, then the same sanitizing.
            var candidates = new List<string> { canon };
            var segs = DialogueUtil.SplitAndSanitize(text);
            if (segs != null && segs.Count > 0)
                candidates.Add(CanonDisplay(string.Join(" ", segs.Select(seg => seg.Display))));
            foreach (string c in candidates.Distinct())
            {
                if (pack.EntriesByDisplayPattern != null && pack.EntriesByDisplayPattern.TryGetValue(c, out rel)) break;
                if (pack.Entries != null && pack.Entries.TryGetValue(c, out rel)) break;
                rel = null;
            }
            if (Config.developerModeOn)
                Monitor.Log($"[V2-{tag}] character='{character}' text='{canon}' match={(rel ?? "no")}", LogLevel.Debug);
            if (string.IsNullOrWhiteSpace(rel))
                return TryPlayTemplate(pack, displayed, tag);
            string full = PathUtilities.NormalizePath(Path.Combine(pack.BaseAssetPath, rel));
            if (!File.Exists(full)) return false;
            PlayVoiceFromFile(full);
            return true;
        }
    }
}
