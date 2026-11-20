using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Shops;
using StardewValley.TokenizableStrings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VoiceOverFrameworkMod
{
    /// <summary>
    /// Lines the game takes from shared string files instead of the NPC's own dialogue sheet (Strings/Characters,
    /// Strings/StringsFromCSFiles, Strings/Locations, Data/ExtraDialogue, Strings/1_6_Strings) and the shop owner
    /// lines from Data/Shops. Who says what follows the game code (NPC.cs, GameLocation.cs, Event.cs, …): a shared
    /// line is added only to NPCs who can actually say it, and only when the NPC has no own dialogue key that the
    /// game would use instead. Lines with substitutions ({0}, %item…) are skipped, except where the game always
    /// passes the same values. Appended after every older source so existing numbers do not move.
    /// </summary>
    public partial class ModEntry : Mod
    {
        // {0}-style inserts (item, amount, price, another NPC's name) can't be recorded; %farm, %pet, %year… can, the
        // actor skips or rephrases them like the player's name
        private static readonly Regex SharedDynamicRx = new(@"\{\d\}", RegexOptions.Compiled);

        /// <summary>A leading '%' not followed by a game token marks narration (Dialogue.percentTokens).</summary>
        private static bool IsNarration(string text)
        {
            string t = (text ?? string.Empty).TrimStart();
            return t.StartsWith("%") && !Dialogue.percentTokens.Any(tok => t.StartsWith(tok, StringComparison.Ordinal));
        }

        private IEnumerable<VoiceEntryTemplate> BuildFromSharedGameStrings(string characterName, string languageCode, IGameContentHelper content, ref int entryNumber, string ext)
        {
            var outList = new List<VoiceEntryTemplate>();
            string suffix = AssetLangSuffix(languageCode);
            var cache = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, string> Sheet(string asset)
            {
                if (!cache.TryGetValue(asset, out var d))
                {
                    try { d = content.Load<Dictionary<string, string>>(asset + suffix); } catch (ContentLoadException) { d = null; }
                    cache[asset] = d ?? new Dictionary<string, string>();
                }
                return cache[asset];
            }

            Game1.characterData.TryGetValue(characterName, out CharacterData data);
            var own = Sheet($"Characters/Dialogue/{characterName}");
            bool Has(params string[] keys) => keys.Any(k => own.ContainsKey(k));
            bool villager = data?.BirthSeason != null;
            bool candidate = data?.CanBeRomanced == true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int n = entryNumber;

            void Emit(string asset, string key, string text = null, string tkOverride = null)
            {
                text ??= Sheet(asset).TryGetValue(key, out var t) ? t : null;
                if (string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("(") || IsNarration(text)) return;
                string tk = tkOverride ?? $"{asset}:{key}";
                if (!seen.Add(tk)) return;
                // only pages with a {0}-style insert are skipped (e.g. the wedding date on the third page)
                foreach (var seg in DialogueUtil.SplitAndSanitize(text))
                    if (!SharedDynamicRx.IsMatch(seg.Actor ?? string.Empty))
                        outList.Add(MakeEntry(characterName, languageCode, ext, ref n, key, tk, seg.PageIndex, seg));
            }
            // Text shown as-is, not parsed as dialogue: question boxes ('^' is a line break there) and
            // speech bubbles (no pages; "#!*#%*!" is a curse, not three pages)
            void EmitPlain(string asset, string key, string text = null)
            {
                text ??= Sheet(asset).TryGetValue(key, out var t) ? t : null;
                if (string.IsNullOrWhiteSpace(text) || SharedDynamicRx.IsMatch(text)) return;
                string tk = $"{asset}:{key}";
                if (!seen.Add(tk)) return;
                text = System.Text.RegularExpressions.Regex.Replace(text.Replace('^', ' '), @"\s+", " ").Trim();
                outList.Add(MakeEntry(characterName, languageCode, ext, ref n, key, tk, 0, new PageSeg { Actor = text, Display = text, PageIndex = 0 }));
            }
            // Game1.LoadStringByGender: '/'-separated male/female forms, picked by the NPC's gender
            void EmitGendered(string asset, string key)
            {
                if (!Sheet(asset).TryGetValue(key, out var t) || t == null) return;
                var parts = t.Split('/');
                Emit(asset, key, data?.Gender == Gender.Male ? parts[0] : parts[^1]);
            }
            // NPC.GetDispositionModifiedString: <key>_<Name>, then manner / social anxiety / optimism / age variants
            void EmitDisposition(string asset, string key)
            {
                var sheet = Sheet(asset);
                var tries = new List<string> { characterName };
                if (data != null)
                    tries.AddRange(new[] { data.Manner.ToString(), data.SocialAnxiety.ToString(), data.Optimism.ToString(), data.Age.ToString() });
                foreach (var t in tries)
                {
                    string k = $"{key}_{char.ToUpperInvariant(t[0])}{t.Substring(1).ToLowerInvariant()}";
                    if (sheet.ContainsKey(k)) { Emit(asset, k); return; }
                }
                Emit(asset, key);
            }

            const string CH = "Strings/Characters", CS = "Strings/StringsFromCSFiles", LOC = "Strings/Locations", EX = "Data/ExtraDialogue", S16 = "Strings/1_6_Strings";

            // ── lines of one specific NPC ──
            switch (characterName)
            {
                case "Abigail":
                    foreach (var k in new[] { "AbigailInMineFirst", "AbigailInMine0", "AbigailInMine1", "AbigailInMine2", "AbigailInMine3", "AbigailInMine4" }) Emit(CH, k);
                    break;
                case "Krobus":
                    Emit(CH, "KrobusDarkTalisman"); Emit(CH, "KrobusDarkTalisman_elsewhere"); Emit(CS, "NPC.cs.3990");
                    Emit(CS, "Krobus_AfterWedding");
                    break;
                case "Henchman":
                    for (int i = 1; i <= 6; i++) Emit(CH, $"Henchman{i}");
                    break;
                case "Leo":
                    Emit(CH, "Leo_Memory");
                    break;
                case "Pierre":
                    Emit(CH, "PierreStockListDialogue");
                    break;
                case "Robin":
                    Emit(CS, "NPC.cs.3926"); Emit(CS, "NPC.cs.3927");
                    // yes/no question boxes (GameLocation.houseUpgradeOffer / communityUpgradeOffer)
                    EmitPlain(LOC, "ScienceHouse_Carpenter_UpgradeHouse1");
                    // houseUpgradeOffer always passes the same values
                    if (Sheet(LOC).TryGetValue("ScienceHouse_Carpenter_UpgradeHouse2", out var up2))
                        EmitPlain(LOC, "ScienceHouse_Carpenter_UpgradeHouse2", string.Format(up2, "65,000", "100"));
                    EmitPlain(LOC, "ScienceHouse_Carpenter_UpgradeHouse3");
                    EmitPlain(LOC, "ScienceHouse_Carpenter_CommunityUpgrade1"); EmitPlain(LOC, "ScienceHouse_Carpenter_CommunityUpgrade2");
                    break;
                case "Demetrius": Emit(CS, "Event.cs.1018"); break;
                case "Maru": Emit(CS, "Event.cs.1020"); break;
                case "Lewis":
                    Emit(CS, "Event.cs.1025"); Emit(CS, "Event.cs.1730");
                    Emit(CS, "Event.cs.1862"); Emit(CS, "Event.cs.1871");                 // festival winner announcements
                    // wedding speech (Data/Weddings → [LocalizedText Strings\StringsFromCSFiles:Utility.cs.53xx])
                    foreach (var k in new[] { "5367", "5369", "5370", "5371", "5373", "5375", "5377", "5379", "5381", "5383" })
                        Emit(CS, $"Utility.cs.{k}");
                    break;
                case "Elliott":
                    foreach (var k in new[] { "1257", "1258", "1259", "1260" }) Emit(CS, $"Event.cs.{k}"); // Event.ElliottBookTalk
                    break;
                case "Grandpa":
                    // Event.GrandpaEvaluation(2): written into the event script with ReplaceCurrentCommand("speak Grandpa …")
                    foreach (var k in new[] { "1306", "1307", "1308", "1309", "1315", "1316", "1317", "1318" }) Emit(CS, $"Event.cs.{k}");
                    break;
                case "Shane":
                    foreach (var k in new[] { "1760", "1761", "1763", "1764" }) Emit(CS, $"Event.cs.{k}");     // Event.answerDialogue
                    break;
                case "Marnie": Emit(CS, "Event.cs.1798"); break;
                case "Clint": Emit(CS, "Tool.cs.14317"); break;                                   // tool left for an upgrade
                case "Willy":
                    foreach (var k in Sheet(LOC).Keys.Where(k => k.StartsWith("BoatTunnel_willyText_")).ToList()) EmitPlain(LOC, k); // bubbles
                    break;
                case "Birdie":
                    // Strings/Locations:alreadyGotNuts is a whole event script; take the text of its speak commands
                    if (Sheet(LOC).TryGetValue("alreadyGotNuts", out var nutsScript))
                    {
                        int i = 0;
                        foreach (Match m in Regex.Matches(nutsScript, @"speak Birdie \""([^""]*)\"""))
                            Emit(LOC, $"alreadyGotNuts", m.Groups[1].Value, $"{LOC}:alreadyGotNuts:{i++}");
                    }
                    break;
                case "Gunther":
                case "Marlon":
                case "Morris":
                    Emit("Strings/StringsFromMaps", $"MovieTheater_{characterName}");             // lobby of the movie theater
                    break;
                case "Bouncer":
                    Emit(CS, "NPC.cs.4020"); Emit(CS, "NPC.cs.4021"); Emit(CS, "NPC.cs.4192");
                    EmitPlain(LOC, "Club_Bouncer_TextAboveHead1"); EmitPlain(LOC, "Club_Bouncer_TextAboveHead2");
                    break;
                case "MrQi":
                case "Mister Qi":
                    Emit(EX, "MisterQi_PlayerClubMember");
                    Emit(S16, "QiSummitCheat");
                    break;
                case "Professor Snail":
                    foreach (var k in Sheet(EX).Keys.Where(k => k.StartsWith("ProfessorSnail_Hint_")).ToList()) Emit(EX, k);
                    foreach (var k in Sheet(LOC).Keys.ToList())
                    {
                        if (k.StartsWith("IslandFieldOffice_Welcome_")) EmitPlain(LOC, k);                                  // bubbles
                        else if (k.EndsWith("_Question") || k.Contains("_Prompt_")) continue;                                  // survey form, not speech
                        else if (k.StartsWith("IslandFieldOffice_Survey_") && !k.EndsWith("_Yes") && !k.EndsWith("_Notyet") && !k.Contains("_Answer")) Emit(LOC, k);
                        else if (k.StartsWith("FieldOfficeDonated_")) Emit(LOC, k);
                    }
                    break;
            }

            // special order deliveries (DeliverObjective: the NPC says the objective's Message)
            foreach (var k in Sheet("Strings/SpecialOrderStrings").Keys
                         .Where(k => Regex.IsMatch(k, $@"^{Regex.Escape(characterName)}\d*_Objective_\d+_Message$")).ToList())
                Emit("Strings/SpecialOrderStrings", k);

            // phone: Phone_<Name>_… (calling the shop) and Phone_Ring_<Name> (the NPC calling)
            foreach (var k in Sheet(CH).Keys.Where(k => k.StartsWith($"Phone_{characterName}_") || k == $"Phone_Ring_{characterName}").ToList())
                Emit(CH, k);
            // NPC.engagementResponse: own Strings/StringsFromCSFiles:<Name>_Engaged(Roommate) lines
            Emit(CS, $"{characterName}_Engaged"); Emit(CS, $"{characterName}_EngagedRoommate");

            // ── shared lines ──
            if (villager)
            {
                Emit(CS, "NPC.cs.4293"); Emit(CS, "NPC.cs.4294");                                          // hit by the player
                Emit(EX, $"Town_DumpsterDiveComment_{(data.Age == NpcAge.Child ? "Child" : data.Age == NpcAge.Teen ? "Teen" : "Adult")}");

                // birthday gifts (NPC.GetGiftReaction), unless own AcceptBirthdayGift_* keys cover that taste
                bool rude = data.Manner == NpcManner.Rude;
                if (!Has("AcceptBirthdayGift", "AcceptBirthdayGift_Positive") && !Has("AcceptBirthdayGift_Loved") | !Has("AcceptBirthdayGift_Liked"))
                {
                    if (rude) { EmitGendered(CS, "NPC.cs.4274"); EmitGendered(CS, "NPC.cs.4276"); }
                    else { Emit(CS, "NPC.cs.4275"); EmitGendered(CS, "NPC.cs.4277"); }
                }
                if (!Has("AcceptBirthdayGift", "AcceptBirthdayGift_Negative") && (!Has("AcceptBirthdayGift_Disliked") || !Has("AcceptBirthdayGift_Hated")))
                    EmitGendered(CS, rude ? "NPC.cs.4278" : "NPC.cs.4279");
                if (!Has("AcceptBirthdayGift", "AcceptBirthdayGift_Neutral", "AcceptBirthdayGift_Positive"))
                {
                    if (rude) Emit(CS, "NPC.cs.4280"); else EmitGendered(CS, "NPC.cs.4281");
                }

                // movie ticket (NPC.tryToReceiveActiveObject)
                if (!Has("MovieInvitation")) EmitDisposition(CH, "MovieInvite_Invited");
                if (!Has("RejectMovieTicket", "RejectMovieTicket_DontWantToSeeThatMovie")) EmitDisposition(CH, "MovieInvite_Reject");
                if (!Has("RejectMovieTicket", "RejectMovieTicket_AlreadyWatchedThisWeek")) EmitDisposition(CH, "MovieInvite_AlreadySeen");
                if (!Has("RejectMovieTicket", "RejectMovieTicket_AlreadyInvitedBySomeoneElse")) EmitDisposition(CH, "MovieInvite_InvitedBySomeoneElse");

                // bouquet given to someone who can't be dated
                if (!candidate && !Has("RejectBouquet_NotDatable", "RejectBouquet")) { Emit(CS, "NPC.cs.3956"); EmitGendered(CS, "NPC.cs.3957"); }
            }

            if (candidate)
            {
                Emit(CH, $"MovieInvite_Spouse_{characterName}");
                if (!Has("RejectBouquet_VeryLowHearts", "RejectBouquet")) { Emit(CS, "NPC.cs.3958"); EmitGendered(CS, "NPC.cs.3959"); }
                if (!Has("RejectBouquet_LowHearts", "RejectBouquet")) { Emit(CS, "NPC.cs.3960"); Emit(CS, "NPC.cs.3961"); }
                if (!Has("AcceptBouquet")) { EmitGendered(CS, "NPC.cs.3962"); EmitGendered(CS, "NPC.cs.3963"); }
                if (!Has("RejectBouquet_Divorced", "RejectBouquet")) Emit(CH, "Divorced_bouquet");
                if (!Has("RejectMovieTicket_Divorced", "RejectMovieTicket")) Emit(CH, "Divorced_gift");
                Emit(CH, "WipedMemory");
                if (!Has("RejectMermaidPendant_PlayerWithSomeoneElse", "RejectMermaidPendant")) { Emit(CS, "NPC.cs.3967"); Emit(CS, "NPC.cs.3968"); }
                if (!Has("RejectMermaidPendant_Under10Hearts")) { Emit(CS, "NPC.cs.3972"); Emit(CS, "NPC.cs.3973"); }
                if (!Sheet(CS).ContainsKey($"{characterName}_Engaged")) Emit(CS, "NPC.cs.3980");
                if (!Has("SpouseStardrop")) Emit(CS, "NPC.cs.4001");
                Emit(CS, "NPC.cs.4500");
                Emit(EX, "Spouse_MonstersInHouse");
                for (int i = 1; i <= 12; i++) EmitPlain(LOC, $"FarmHouse_SpouseAttacked{i}");             // bubbles
                Emit(S16, "Spouse_Goodnight0"); Emit(S16, "Spouse_Goodnight1");
                if (!Has("FlowerDance_Accept")) { Emit(CS, "Event.cs.1632"); Emit(CS, "Event.cs.1633"); Emit(CS, "Event.cs.1634"); }
                if (!Has("FlowerDance_Decline")) Emit(CS, "Event.cs.1635");
            }

            entryNumber = n;
            return outList;
        }

        /// <summary>
        /// What a shop owner says in the shop menu (Data/Shops → Owners → Dialogues), for owners named after this NPC.
        /// The menu draws it in its own box next to the portrait; see CheckForShopOwnerDialogue.
        /// </summary>
        private IEnumerable<VoiceEntryTemplate> BuildFromShopOwnerDialogues(string characterName, string languageCode, IGameContentHelper content, ref int entryNumber, string ext)
        {
            var outList = new List<VoiceEntryTemplate>();
            Dictionary<string, ShopData> shops;
            try { shops = DataLoader.Shops(Game1.content); } catch { return outList; }
            int n = entryNumber;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (shopId, shop) in shops)
            {
                foreach (var owner in shop?.Owners ?? new List<ShopOwnerData>())
                {
                    if (!string.Equals(owner?.Name, characterName, StringComparison.OrdinalIgnoreCase) || owner.Dialogues == null) continue;
                    foreach (var dlg in owner.Dialogues)
                    {
                        var texts = new List<string>();
                        if (dlg.RandomDialogue != null && dlg.RandomDialogue.Count > 0) texts.AddRange(dlg.RandomDialogue);
                        else if (!string.IsNullOrWhiteSpace(dlg.Dialogue)) texts.Add(dlg.Dialogue);
                        for (int i = 0; i < texts.Count; i++)
                        {
                            string text = TokenParser.ParseText(texts[i])?.Trim();
                            if (string.IsNullOrWhiteSpace(text) || SharedDynamicRx.IsMatch(text) || text.Contains('[')) continue;
                            if (!seen.Add(text)) continue;
                            string tk = $"Data/Shops:{shopId}:{dlg.Id}";
                            foreach (var seg in DialogueUtil.SplitAndSanitize(text))
                                outList.Add(MakeEntry(characterName, languageCode, ext, ref n, $"Shop_{shopId}", tk, i, seg));
                        }
                    }
                }
            }
            entryNumber = n;
            return outList;
        }
    }
}
