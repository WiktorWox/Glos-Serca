using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;

namespace VoiceOverFrameworkMod
{
    /// <summary>
    /// Event slot numbering at runtime. The pack keys event lines as Events/&lt;map&gt;:&lt;id&gt;:s&lt;n&gt;, where n is the
    /// serial the template generator (ModEntry.Events.cs, BuildFromEvents) assigned while walking the script. This
    /// file re-implements that walk with the generator's own regexes and DialogueUtil.SplitAndSanitize, so both
    /// sides count the same by construction. Runtime differences handled here: the game inserts the chosen
    /// quickQuestion branch into eventCommands after the question (skipped as a block), and a 'fork' replaces the
    /// whole command list (keyed by fork name in BuildEventBaseKeyForCurrent).
    /// </summary>
    public partial class ModEntry
    {
        // same regexes as BuildFromEvents
        private static readonly Regex EvSpeakRx = new(@"^\s*speak\s+(\w+)\s+""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex EvNamedRx = new(@"\b(?:textAboveHead|drawDialogue|message|showText)\s*(\w*)\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex EvGenericRx = new(@"""((?:[^""\\]|\\.){4,})""", RegexOptions.Compiled);
        private static readonly Regex EvQuickQuestionRx = new(@"^\s*quickQuestion\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Texts the generator would emit for one command, each attributed to a speaker. Mirrors ProcessOneCommand.</summary>
        private static List<(string Speaker, string Text)> GeneratorEmitsForCommand(string cmdRaw, ref string lastSpeaker)
        {
            var result = new List<(string, string)>();
            string command = (cmdRaw ?? string.Empty).Trim();
            if (command.Length == 0)
                return result;

            var mSpeak = EvSpeakRx.Match(command);
            if (mSpeak.Success)
            {
                lastSpeaker = mSpeak.Groups[1].Value;
                result.Add((lastSpeaker, mSpeak.Groups[2].Value));
                return result;
            }
            var mNamed = EvNamedRx.Match(command);
            if (mNamed.Success)
            {
                string maybeSpeaker = mNamed.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(maybeSpeaker))
                    lastSpeaker = maybeSpeaker;
                if (!string.IsNullOrWhiteSpace(lastSpeaker))
                    result.Add((lastSpeaker, mNamed.Groups[2].Value));
                return result;
            }
            if (!string.IsNullOrWhiteSpace(lastSpeaker))
            {
                foreach (Match gm in EvGenericRx.Matches(command))
                {
                    string chunk = gm.Groups[1].Value.Trim();
                    if (chunk.Length > 3 && !chunk.StartsWith("..."))
                        result.Add((lastSpeaker, gm.Groups[1].Value));
                }
            }
            return result;
        }

        /// <summary>Segments (= slots) the generator makes out of one captured text.</summary>
        private static List<PageSeg> GeneratorSegments(string captured)
        {
            string text;
            try { text = Regex.Unescape(captured ?? string.Empty); }
            catch { text = captured ?? string.Empty; }
            return DialogueUtil.SplitAndSanitize(text, splitBAsPage: true) ?? new List<PageSeg>();
        }

        /// <summary>quickQuestion branches as the generator sees them: pieces after "(break)", '\' normalised to '/', split on '/'.</summary>
        private static IEnumerable<string> GeneratorBranchCommands(string branchPiece)
            => (branchPiece ?? string.Empty).Replace('\\', '/').Split('/');

        /// <summary>What Event.ParseCommands makes of one raw command: gender switch blocks resolved, tokens parsed, trimmed.</summary>
        private static string ResolveLikeGame(string rawCommand)
        {
            try
            {
                var gender = Game1.player?.Gender ?? StardewValley.Gender.Male;
                string s = Dialogue.applyGenderSwitchBlocks(gender, rawCommand ?? string.Empty);
                s = StardewValley.TokenizableStrings.TokenParser.ParseText(s);
                return s.Trim();
            }
            catch { return (rawCommand ?? string.Empty).Trim(); }
        }

        private string _auditMapOverride; // audit runs against events that are not in the current location
        private Event _rawEvent;
        private string[] _rawEventCommands;
        private Dictionary<string, string> _rawByResolved;

        /// <summary>
        /// Map from a runtime command (as in ev.eventCommands) back to the raw script text the generator saw,
        /// including quickQuestion branch sub-commands. The generator counts gender switch blocks
        /// (${a^b}$) as two slots, but the runtime list only holds one variant, so counting needs the raw text.
        /// </summary>
        private Dictionary<string, string> GetRawCommandMap(Event ev, string map)
        {
            if (ReferenceEquals(ev, _rawEvent) && ReferenceEquals(ev.eventCommands, _rawEventCommands))
                return _rawByResolved;
            _rawEvent = ev; _rawEventCommands = ev.eventCommands;
            _rawByResolved = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                string wantedKey = ResolveForkKey(ev, map) ?? CleanEventId(ev.id);
                var scripts = Game1.content.Load<Dictionary<string, string>>("Data\\Events\\" + map);
                // the same event id can have several variants (different preconditions, slightly different
                // text); merge them all, the runtime commands pick the matching raw text by equality
                foreach (var kv in scripts)
                {
                    if (CleanEventId(kv.Key) != wantedKey || string.IsNullOrEmpty(kv.Value)) continue;
                    string script = kv.Value;
                    foreach (string piece in script.Split('/'))
                    {
                        string resolved = ResolveLikeGame(piece);
                        if (resolved.Length > 0 && !_rawByResolved.ContainsKey(resolved))
                            _rawByResolved[resolved] = piece;
                        if (EvQuickQuestionRx.IsMatch(piece) && piece.Contains("(break)"))
                            foreach (string branch in piece.Split(new[] { "(break)" }, StringSplitOptions.None).Skip(1))
                                foreach (string sub in branch.Split('\\'))
                                {
                                    string r = ResolveLikeGame(sub);
                                    if (r.Length > 0 && !_rawByResolved.ContainsKey(r))
                                        _rawByResolved[r] = sub;
                                }
                    }
                }
                if (Config.developerModeOn)
                    Monitor.Log($"[V2-EVENT] raw script for {map}:{wantedKey}: {(_rawByResolved.Count > 0 ? _rawByResolved.Count + " commands" : "not found")}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                if (Config.developerModeOn)
                    Monitor.Log($"[V2-EVENT] raw script lookup failed: {ex.Message}", LogLevel.Debug);
            }
            return _rawByResolved;
        }

        private static string NormalizeForCompare(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Replace("{Farmer_Name}", " ").Replace("@", " ");
            string farmer = Utility.FilterUserName(Game1.player?.Name);
            if (!string.IsNullOrEmpty(farmer))
                s = s.Replace(farmer, " ");
            s = Regex.Replace(s, @"\{[^}]*\}", " ");      // {Portrait:...}
            s = Regex.Replace(s, @"\$(?:h|s|u|l|a|\d+|q|r|b|e|k|c|y)\b", " ");
            s = Regex.Replace(s, @"[\s#^""]+", " ").Trim().ToLowerInvariant();
            return s;
        }

        /// <summary>Which of a command's segments is on screen: by text, else by gender and page.</summary>
        private static int PickSegment(List<PageSeg> segs, string displayed, int currentPageIndex)
        {
            if (segs.Count <= 1)
                return 0;
            string want = NormalizeForCompare(displayed);
            if (want.Length > 0)
            {
                for (int i = 0; i < segs.Count; i++)
                    if (NormalizeForCompare(segs[i].Display ?? segs[i].Actor) == want)
                        return i;
                if (want.Length >= 8)
                    for (int i = 0; i < segs.Count; i++)
                    {
                        string have = NormalizeForCompare(segs[i].Display ?? segs[i].Actor);
                        if (have.Length >= 8 && (have.Contains(want) || want.Contains(have)))
                            return i;
                    }
            }
            string gender = Game1.player?.IsMale == false ? "female" : "male";
            var byGender = segs.Select((s, i) => (s, i))
                .Where(t => string.IsNullOrEmpty(t.s.Gender) || t.s.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var byPage = byGender.FirstOrDefault(t => t.s.PageIndex == currentPageIndex);
            if (byPage.s != null) return byPage.i;
            if (byGender.Count > 0) return byGender[Math.Clamp(currentPageIndex, 0, byGender.Count - 1)].i;
            return Math.Clamp(currentPageIndex, 0, segs.Count - 1);
        }

        /// <summary>
        /// Index n of the line currently shown, such that the pack key is Events/&lt;map&gt;:&lt;id&gt;:s&lt;n&gt;.
        /// Returns null when the current command has no line of this speaker (then text matching takes over).
        /// </summary>
        private int? ComputeSpeakIndexForCurrent(
            Event ev,
            string speakerName,
            int currentPageIndex = 0,
            int commandIndexOverride = -1,
            string currentDisplayedText = null)
        {
            if (ev?.eventCommands == null || ev.eventCommands.Length == 0 || string.IsNullOrWhiteSpace(speakerName))
                return null;
            string[] cmds = ev.eventCommands;
            int current = commandIndexOverride >= 0 ? commandIndexOverride : ev.CurrentCommand;
            current = Math.Clamp(current, 0, cmds.Length - 1);

            int serial = 0;
            string lastSpeaker = null;
            bool Mine(string s) => s != null && s.Equals(speakerName, StringComparison.OrdinalIgnoreCase);
            var rawMap = GetRawCommandMap(ev, _auditMapOverride ?? Game1.currentLocation?.Name ?? "Unknown");
            string Raw(string runtimeCmd) => rawMap != null && rawMap.TryGetValue((runtimeCmd ?? string.Empty).Trim(), out var raw) ? raw : runtimeCmd;

            for (int i = 0; i <= current; i++)
            {
                if (string.IsNullOrWhiteSpace(cmds[i]))
                    continue;
                string line = Raw(cmds[i]);

                if (EvQuickQuestionRx.IsMatch(line) && line.IndexOf("(break)", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string[] pieces = line.Split(new[] { "(break)" }, StringSplitOptions.None);

                    // Did the game already insert the chosen branch right after this command?
                    int chosen = -1, materialized = 0;
                    for (int b = 1; b < pieces.Length && chosen < 0; b++)
                    {
                        string[] gameCmds = pieces[b].Split('\\').Select(ResolveLikeGame).ToArray();
                        bool match = gameCmds.Length > 0 && i + gameCmds.Length < cmds.Length;
                        for (int c = 0; c < gameCmds.Length && match; c++)
                            if (!string.Equals(cmds[i + 1 + c]?.Trim(), gameCmds[c], StringComparison.Ordinal))
                                match = false;
                        if (match) { chosen = b; materialized = gameCmds.Length; }
                    }
                    bool currentInside = materialized > 0 && current > i && current <= i + materialized;
                    string currentCmdText = currentInside ? cmds[current].Trim() : null;

                    for (int b = 1; b < pieces.Length; b++)
                    {
                        bool chosenBranch = b == chosen;
                        bool consumedCurrent = false;
                        foreach (string sub in GeneratorBranchCommands(pieces[b]))
                        {
                            bool isCurrentCmd = chosenBranch && currentInside && !consumedCurrent &&
                                                string.Equals(ResolveLikeGame(sub), currentCmdText, StringComparison.Ordinal);
                            foreach (var emit in GeneratorEmitsForCommand(sub, ref lastSpeaker))
                            {
                                if (!Mine(emit.Speaker)) continue;
                                var segs = GeneratorSegments(emit.Text);
                                if (segs.Count == 0) continue;
                                if (isCurrentCmd)
                                    return serial + PickSegment(segs, currentDisplayedText, currentPageIndex);
                                // not materialized yet (e.g. audit on a fresh Event): find the shown text in the branches
                                if (i == current && materialized == 0 && !string.IsNullOrWhiteSpace(currentDisplayedText))
                                {
                                    string want = NormalizeForCompare(currentDisplayedText);
                                    for (int k = 0; k < segs.Count; k++)
                                        if (NormalizeForCompare(segs[k].Display ?? segs[k].Actor) == want)
                                            return serial + k;
                                }
                                serial += segs.Count;
                            }
                            if (isCurrentCmd) consumedCurrent = true;
                        }
                    }
                    if (currentInside)
                        return null; // current line inside the branch but not this speaker's
                    i += materialized;
                    continue;
                }

                var emits = GeneratorEmitsForCommand(line, ref lastSpeaker);
                foreach (var emit in emits)
                {
                    if (!Mine(emit.Speaker)) continue;
                    var segs = GeneratorSegments(emit.Text);
                    if (segs.Count == 0) continue;
                    if (i == current)
                    {
                        bool inSpeakQuestion = emit.Text.IndexOf("$q", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               emit.Text.IndexOf("$r", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (inSpeakQuestion)
                        {
                            // Only the question text lives in these slots; the pages shown after the player's
                            // answer come from Characters/Dialogue/<npc>:<responseKey> and are matched by text.
                            string want = NormalizeForCompare(currentDisplayedText);
                            for (int k = 0; k < segs.Count; k++)
                                if (want.Length > 0 && NormalizeForCompare(segs[k].Display ?? segs[k].Actor) == want)
                                    return serial + k;
                            return null;
                        }
                        return serial + PickSegment(segs, currentDisplayedText, currentPageIndex);
                    }
                    serial += segs.Count;
                }
                if (i == current)
                    return null;
            }
            return null;
        }

        /// <summary>
        /// Developer check: for every event script and speaker compare the generator's slot count, the runtime
        /// counter's result for the last line, and the pack's highest slot. Usage: vof_audit_events [character]
        /// </summary>
        private void AuditEventIndexCommand(string command, string[] args)
        {
            if (args.Length >= 3) { AuditEventVerbose(args[0], args[1], args[2]); return; }
            string onlyChar = args.Length > 0 ? args[0] : null;
            string lang = GetGameLanguageTag();
            int events = 0, pairs = 0, runtimeBad = 0, packBad = 0;
            var lines = new List<string>();
            var packCheck = new Dictionary<(string, string, string), (int pack, List<int> counts)>();
            foreach (var (mapName, eventData) in EnumerateEventSources(lang, this.Helper.GameContent))
            {
                foreach (var (eventIdRaw, script) in eventData)
                {
                    if (string.IsNullOrWhiteSpace(script)) continue;
                    events++;
                    string cleanId = CleanEventId(eventIdRaw);

                    // generator totals and the last emitted segment per speaker
                    var total = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var lastSeg = new Dictionary<string, (int cmdIndex, PageSeg seg)>(StringComparer.OrdinalIgnoreCase);
                    string last = null;
                    string[] topCommands = script.Split('/');
                    void Process(string cmd, int topIndex)
                    {
                        if (EvQuickQuestionRx.IsMatch(cmd) && cmd.IndexOf("(break)", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var pieces = cmd.Split(new[] { "(break)" }, StringSplitOptions.None);
                            for (int b = 1; b < pieces.Length; b++)
                                foreach (var sub in GeneratorBranchCommands(pieces[b]))
                                    Process(sub, topIndex);
                            return;
                        }
                        foreach (var emit in GeneratorEmitsForCommand(cmd, ref last))
                        {
                            var segs = GeneratorSegments(emit.Text);
                            if (segs.Count == 0) continue;
                            total[emit.Speaker] = total.GetValueOrDefault(emit.Speaker) + segs.Count;
                            lastSeg[emit.Speaker] = (topIndex, segs[^1]);
                        }
                    }
                    for (int t = 0; t < topCommands.Length; t++)
                        Process(topCommands[t], t);

                    Event ev = null;
                    try { ev = new Event(script, $"Data\\Events\\{mapName}", cleanId, Game1.player); } catch { }
                    _auditMapOverride = mapName;

                    foreach (var kv in total)
                    {
                        string speaker = kv.Key;
                        if (onlyChar != null && !speaker.Equals(onlyChar, StringComparison.OrdinalIgnoreCase)) continue;
                        var pack = GetSelectedVoicePack(speaker);
                        if (pack == null) continue; // nobody voices this speaker
                        pairs++;

                        int packMax = -1;
                        string prefix = $"Events/{mapName}:{cleanId}:s";
                        if (pack.EntriesByTranslationKey != null)
                            foreach (var key in pack.EntriesByTranslationKey.Keys)
                                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(key.Substring(prefix.Length).Split(':')[0], out int n))
                                    packMax = Math.Max(packMax, n);

                        int? runtime = null;
                        if (ev?.eventCommands != null)
                        {
                            var (cmdIndex, seg) = lastSeg[speaker];
                            string resolved = ResolveLikeGame(topCommands[cmdIndex]);
                            int rt = Array.FindIndex(ev.eventCommands, c => string.Equals(c?.Trim(), resolved, StringComparison.Ordinal));
                            if (rt >= 0)
                                runtime = ComputeSpeakIndexForCurrent(ev, speaker, seg.PageIndex, rt, seg.Display ?? seg.Actor);
                        }
                        bool rtOk = runtime.HasValue && runtime.Value == kv.Value - 1;
                        if (!rtOk) { runtimeBad++; lines.Add($"  {mapName}:{cleanId} {speaker}: generator={kv.Value} runtime={(runtime.HasValue ? (runtime.Value + 1).ToString() : "?")}"); }
                        if (packMax >= 0)
                        {
                            var pk = (mapName, cleanId, speaker);
                            if (!packCheck.TryGetValue(pk, out var pc)) packCheck[pk] = pc = (packMax + 1, new List<int>());
                            pc.counts.Add(kv.Value);
                        }
                    }
                }
            }
            foreach (var kv in packCheck)
                if (!kv.Value.counts.Contains(kv.Value.pack))
                { packBad++; lines.Add($"  {kv.Key.Item1}:{kv.Key.Item2} {kv.Key.Item3}: pack={kv.Value.pack} generator variants={string.Join("/", kv.Value.counts)}"); }
            _auditMapOverride = null;
            Monitor.Log($"[Audit] events={events} (event,speaker) pairs with a pack={pairs}; runtime mismatches={runtimeBad}; pack numbering differs={packBad}", LogLevel.Info);
            foreach (var l in lines) Monitor.Log(l, LogLevel.Info);
        }

        /// <summary>vof_audit_events &lt;character&gt; &lt;map&gt; &lt;eventId&gt;: per-command serials from the generator walk and from the runtime counter.</summary>
        private void AuditEventVerbose(string speaker, string map, string eventId)
        {
            string lang = GetGameLanguageTag();
            foreach (var (mapName, eventData) in EnumerateEventSources(lang, this.Helper.GameContent))
            {
                if (!mapName.Equals(map, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var (eventIdRaw, script) in eventData)
                {
                    if (CleanEventId(eventIdRaw) != eventId) continue;
                    string[] top = script.Split('/');
                    Event ev = null;
                    try { ev = new Event(script, $"Data\\Events\\{mapName}", eventId, Game1.player); } catch { }
                    _auditMapOverride = mapName;
                    Monitor.Log($"[Audit] {mapName}:{eventId} speaker={speaker}; raw commands={top.Length}, parsed commands={ev?.eventCommands?.Length ?? -1}", LogLevel.Info);
                    string last = null; int serial = 0;
                    for (int t = 0; t < top.Length; t++)
                    {
                        var pieces = EvQuickQuestionRx.IsMatch(top[t]) && top[t].Contains("(break)") ? top[t].Split(new[] { "(break)" }, StringSplitOptions.None) : null;
                        IEnumerable<string> subs = pieces == null ? new[] { top[t] } : pieces.Skip(1).SelectMany(GeneratorBranchCommands);
                        foreach (var sub in subs)
                            foreach (var emit in GeneratorEmitsForCommand(sub, ref last))
                            {
                                if (!emit.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase)) continue;
                                var segs = GeneratorSegments(emit.Text);
                                if (segs.Count == 0) continue;
                                int rtIndex = ev?.eventCommands == null ? -1 : Array.FindIndex(ev.eventCommands, c => string.Equals(c?.Trim(), ResolveLikeGame(top[t]), StringComparison.Ordinal));
                                int? rt = rtIndex >= 0 ? ComputeSpeakIndexForCurrent(ev, speaker, segs[^1].PageIndex, rtIndex, segs[^1].Display ?? segs[^1].Actor) : null;
                                Monitor.Log($"  cmd#{t} gen s{serial}..s{serial + segs.Count - 1} runtime(last)={(rt.HasValue ? "s" + rt.Value : "null")} | {sub.Trim().Substring(0, Math.Min(90, sub.Trim().Length))}", LogLevel.Info);
                                for (int k = 0; k < segs.Count; k++)
                                    Monitor.Log($"      s{serial + k} p{segs[k].PageIndex}{(segs[k].Gender != null ? "/" + segs[k].Gender : "")}: {segs[k].Display}", LogLevel.Info);
                                serial += segs.Count;
                            }
                    }
                    if (ev?.eventCommands != null)
                        for (int k = 0; k < ev.eventCommands.Length; k++)
                            if (k >= top.Length || !string.Equals(ev.eventCommands[k].Trim(), top[k].Trim(), StringComparison.Ordinal))
                            { Monitor.Log($"  first parsed/raw difference at #{k}: parsed='{(k < ev.eventCommands.Length ? ev.eventCommands[k] : "")}' raw='{(k < top.Length ? top[k] : "")}'", LogLevel.Info); break; }
                }
            }
            _auditMapOverride = null;
        }
    }
}
