using Microsoft.Xna.Framework.Audio;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using System.Text.RegularExpressions;

namespace VoiceOverFrameworkMod
{
    public partial class ModEntry : Mod
    {
        // Dialogue State Tracking
        private bool wasDialogueUpLastTick = false;
        private string lastDialogueText = null;
        private int lastDialoguePage = -1;
        private string lastSpeakerName = null;
        private MultilingualDictionary Multilingual;

        internal NPC CurrentDialogueSpeaker = null;
        internal string CurrentDialogueOriginalKey = null;

        internal bool IsMultiPageDialogueActive { get; set; } = false;


        // --- Tunables ---
        private const int DialogueCloseDebounceTicks = 0; // how many consecutive "not visible" ticks before we treat it as a real close 8 was magical number but set to 0 for testing.
      


        // --- Dialogue debounce / replay guards ---
        private int _dialogueNotVisibleTicks = 0;   
        private int _sameLineStableTicks = 0; 
        private string _lastPlayedLookupKey = null; // last key actually played for this page

        // --- V2 event serial state (per-event, per-speaker, increments once per displayed page) ---
        private string _v2_lastEventBase = null;
        private string _v2_lastEventPageFingerprint = null; // eventBase|speaker|sanitizedText
        private readonly Dictionary<string, int> _v2_eventSerialBySpeaker = new(StringComparer.OrdinalIgnoreCase);
        private string _lastLoggedEventCommandSnapshot = null;




        // Chooses the right sanitizer based on the loaded pack's FormatMajor
        private string SanitizeForPack(string raw, VoicePack pack)
            => (pack?.FormatMajor ?? 1) >= 2 ? SanitizeDialogueTextV2(raw) : SanitizeDialogueText(raw);










        // Main dialogue check loop called every tick (or less often if adjusted).
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (e.IsMultipleOf(2))
                CleanupStoppedVoiceInstances();

            bool eventActive = Game1.currentLocation?.currentEvent != null;

            if (eventActive)
            {
                var activeEvent = Game1.currentLocation.currentEvent;
                string[] commands = activeEvent.eventCommands ?? Array.Empty<string>();
                string snapshot = $"{activeEvent.id}|{activeEvent.CurrentCommand}|{string.Join("\u001f", commands)}";
                if (!string.Equals(snapshot, _lastLoggedEventCommandSnapshot, StringComparison.Ordinal))
                {
                    _lastLoggedEventCommandSnapshot = snapshot;
                    Monitor.Log($"[EventCommands] EventId={activeEvent.id}, CurrentCommand={activeEvent.CurrentCommand}, Count={commands.Length}", LogLevel.Info);
                    for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
                        Monitor.Log($"[EventCommands] {commandIndex}: {commands[commandIndex]}", LogLevel.Info);
                }
            }
            else if (!eventActive)
            {
                _lastLoggedEventCommandSnapshot = null;
            }

            var dlgBox = Game1.activeClickableMenu as DialogueBox;
            string speakerName = dlgBox?.characterDialogue?.speaker?.Name ?? Game1.currentSpeaker?.Name;

            VoicePack pack = null;
            if (!string.IsNullOrEmpty(speakerName))
                pack = GetSelectedVoicePack(speakerName);

            bool logThisTick = Config.developerModeOn && e.IsMultipleOf(15);
            if (logThisTick)
                Monitor.Log($"[Tick] EventActive={(eventActive ? "Y" : "N")} Speaker={speakerName ?? "null"} Pack={(pack?.VoicePackName ?? "null")} FormatMajor={(pack?.FormatMajor ?? -1)}", LogLevel.Trace);

            if (eventActive)
            {
                if (logThisTick) Monitor.Log("[Tick] Using V2 *event* pipeline", LogLevel.Trace);
                CheckForEventV2();           // <- your new event-specific parser
            }
            else if (pack != null && pack.FormatMajor >= 2)
            {
                if (logThisTick) Monitor.Log("[Tick] Using V2 *dialogue* pipeline", LogLevel.Trace);
                CheckForDialogueV2();        // <- your existing dialogue V2 parser
            }
            else
            {
                if (logThisTick) Monitor.Log("[Tick] Using V1 pipeline", LogLevel.Trace);
                CheckForDialogue();          // <- legacy pipeline
            }

            // Bubbles after line handling
            CheckForSpeechBubblesLevel1();
        }








        /// <summary>
        /// Try to play by raw TranslationKey(s) (e.g., "Characters/Dialogue/Abigail:Mon", "Events/SeedShop:1:s0").
        /// Returns true if playback started. Falls back to V1 if provided.
        /// </summary>
        public bool TryToPlayVoiceByTranslationKeys(
            string characterName,
            IEnumerable<string> tkCandidates,
            LocalizedContentManager.LanguageCode languageCode,
            string? sanitizedDialogueTextForV1Fallback = null)
        {
            var candidates = tkCandidates?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new();
            if (candidates.Count == 0)
                return false;

            // Resolve V2-capable pack for this character
            var fullPath = GetAudioPathByTranslationKeys(characterName, candidates, languageCode, out VoicePack? packUsed, out string? matchedKey);

            if (!string.IsNullOrWhiteSpace(fullPath))
            {
                if (Config?.developerModeOn == true)
                    Monitor.Log($"[V2-RESULT] char='{characterName}' pack='{packUsed?.VoicePackName}' format={(packUsed?.FormatMajor.ToString() ?? "?")} match=Yes key='{matchedKey}' path='{fullPath}'", LogLevel.Info);

                PlayVoiceFromFile(fullPath!);
                return true;
            }

            if (Config?.developerModeOn == true)
                Monitor.Log($"[V2-RESULT] char='{characterName}' pack='{packUsed?.VoicePackName ?? "(none)"}' format={(packUsed?.FormatMajor.ToString() ?? "?")} match=No → fallback to V1 text", LogLevel.Info);

            // Optional fallback to V1 sanitized text
            if (!string.IsNullOrWhiteSpace(sanitizedDialogueTextForV1Fallback))
                TryToPlayVoice(characterName, sanitizedDialogueTextForV1Fallback!, languageCode);

            return false;
        }

        /// <summary>
        /// Resolve a V2 pack and try raw TK candidates against EntriesByTranslationKey. Returns absolute path if found.
        /// </summary>
        private string? GetAudioPathByTranslationKeys(
            string characterName,
            IEnumerable<string> tkCandidates,
            LocalizedContentManager.LanguageCode languageCode,
            out VoicePack? packUsed,
            out string? matchedKey)
        {
            packUsed = null;
            matchedKey = null;

            if (!VoicePacksByCharacter.TryGetValue(characterName, out var availablePacks) || !availablePacks.Any())
                return null;

            if (!SelectedVoicePacks.TryGetValue(characterName, out string selectedVoicePackId) ||
                string.IsNullOrEmpty(selectedVoicePackId) ||
                selectedVoicePackId.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                // Use the normal character fallback when no explicit pack was
                // configured. Exact TranslationKey lookups should not depend on
                // the user having manually selected the pack.
                var fallbackPack = GetSelectedVoicePack(characterName);
                if (fallbackPack == null)
                    return null;

                selectedVoicePackId = fallbackPack.VoicePackId;
            }

            // language selection (same as V1)
            string primaryLangStr = languageCode.ToString().ToLowerInvariant();
            string configDefaultLangStr = (Config?.DefaultLanguage ?? "en").ToLowerInvariant();
            const string hardcodedFallbackLangStr = "en";
            bool allowFallbackToEnglish = Config?.FallbackToDefaultIfMissing ?? true;

            // Pick a V2-capable pack
            VoicePack? pack = availablePacks.FirstOrDefault(p =>
                p.FormatMajor >= 2 &&
                p.VoicePackId.Equals(selectedVoicePackId, StringComparison.OrdinalIgnoreCase) &&
                p.Language.StartsWith(primaryLangStr, StringComparison.OrdinalIgnoreCase));

            if (pack == null && primaryLangStr != configDefaultLangStr)
            {
                pack = availablePacks.FirstOrDefault(p =>
                    p.FormatMajor >= 2 &&
                    p.VoicePackId.Equals(selectedVoicePackId, StringComparison.OrdinalIgnoreCase) &&
                    p.Language.StartsWith(configDefaultLangStr, StringComparison.OrdinalIgnoreCase));
            }

            if (pack == null && allowFallbackToEnglish && primaryLangStr != hardcodedFallbackLangStr && configDefaultLangStr != hardcodedFallbackLangStr)
            {
                pack = availablePacks.FirstOrDefault(p =>
                    p.FormatMajor >= 2 &&
                    p.VoicePackId.Equals(selectedVoicePackId, StringComparison.OrdinalIgnoreCase) &&
                    p.Language.StartsWith(hardcodedFallbackLangStr, StringComparison.OrdinalIgnoreCase));
            }

            // No V2 or no TK map? bail
            if (pack == null || pack.EntriesByTranslationKey == null || pack.EntriesByTranslationKey.Count == 0)
                return null;

            packUsed = pack;

            // Try exact TK match
            foreach (var tk in tkCandidates)
            {
                if (pack.EntriesByTranslationKey.TryGetValue(tk, out var rel))
                {
                    matchedKey = tk;
                    return PathUtilities.NormalizePath(Path.Combine(pack.BaseAssetPath, rel));
                }
            }

            // (Optional) If your packs keep templated TKs with {Vars} (rare), you can render here.
            // Not doing that by default because TKs are usually literal.

            return null;
        }




        // Checks the current game state for active dialogue boxes and triggers voice playback.
        private void CheckForDialogue()
        {
            if (Game1.currentLocation == null || Game1.player == null) // || !Context.IsWorldReady
            {
                if (lastDialogueText != null) ResetDialogueState(); // Clear state if we exit world context
                return;
            }

            bool isDialogueBoxVisible = Game1.activeClickableMenu is DialogueBox;
            NPC currentSpeaker = Game1.currentSpeaker;

            // --- Debounce transient close between pages ---
            if (!isDialogueBoxVisible)
            {
                if (wasDialogueUpLastTick)
                {
                    _dialogueNotVisibleTicks++;
                    if (_dialogueNotVisibleTicks >= DialogueCloseDebounceTicks) // require consecutive ticks before reset
                    {
                        ResetDialogueState();
                        _dialogueNotVisibleTicks = 0;
                    }
                }
                return;
            }
            else
            {
                _dialogueNotVisibleTicks = 0; // visible again
            }



            string currentDisplayedString = null; // Renamed for clarity
            if (isDialogueBoxVisible)
            {

                DialogueBox dialogueBox = Game1.activeClickableMenu as DialogueBox;
                
                currentDisplayedString = dialogueBox?.getCurrentString();


                
            }

            // --- Text stabilization ---
            if (!string.IsNullOrWhiteSpace(currentDisplayedString))
            {
                if (currentDisplayedString == lastDialogueText)
                {
                    _sameLineStableTicks++;
                }
                else
                {

                    
                    // text/page detected
                    lastDialogueText = currentDisplayedString;
                    lastSpeakerName = currentSpeaker?.Name;
                    wasDialogueUpLastTick = true;
                    _sameLineStableTicks = 0;
                    _lastPlayedLookupKey = null; // allow one play for this page
                }

                if (_sameLineStableTicks < Config.TextStabilizeTicks) // wait more ticks for stability
                    return;

                if (currentSpeaker != null)
                {
                    string farmerName = Game1.player.Name;
                    string potentialOriginalText = currentDisplayedString;

                    if (!string.IsNullOrEmpty(farmerName) && potentialOriginalText.Contains(farmerName))
                        potentialOriginalText = potentialOriginalText.Replace(farmerName, "@");

                    string sanitizedStep1 = SanitizeDialogueText(potentialOriginalText);
                    string finalLookupKey = Regex.Replace(sanitizedStep1, @"#.+?#", "").Trim();

                    LocalizedContentManager.LanguageCode currentLanguageCode = LocalizedContentManager.CurrentLanguageCode;
                    string gameLanguage = currentLanguageCode.ToString();
                    string characterName = currentSpeaker.Name;
                    string voicePackLanguage = GetVoicePackLanguageForCharacter(characterName);


                    if (!string.IsNullOrWhiteSpace(finalLookupKey))
                    {
                        // Prevent replay on same page
                        if (string.Equals(finalLookupKey, _lastPlayedLookupKey, StringComparison.Ordinal))
                        {
                            if (Config.developerModeOn)
                                Monitor.Log($"[VOICE] Debounced repeat key on same page: '{finalLookupKey}'", LogLevel.Trace);
                            return;
                        }

                        // Normalize language compare (handles ja vs ja-JP, en vs en-US, etc.)
                        string gameLangNorm = (gameLanguage ?? "").ToLowerInvariant();
                        string packLangNorm = (voicePackLanguage ?? "").ToLowerInvariant();

                        bool sameLanguage =
                            gameLangNorm == packLangNorm ||
                            (!string.IsNullOrEmpty(gameLangNorm) && packLangNorm.StartsWith(gameLangNorm)) ||
                            (!string.IsNullOrEmpty(packLangNorm) && gameLangNorm.StartsWith(packLangNorm));

                        // --- Always attempt dictionary->DialogueFrom first (fixes V1 JA pack not playing when game is JA) ---
                        // Your GetDialogueFrom early-outs when gameLang == packLang, so force it to run by passing "different" lang strings.
                        // The lookup itself is based on sanitized displayed text anyway.
                        string resolvedFrom = null;
                        if (Multilingual != null)
                            resolvedFrom = Multilingual.GetDialogueFrom(characterName, "xx", "yy", currentDisplayedString);

                        if (Config.developerModeOn)
                        {
                            Monitor.Log($"[VOICE] Speaker={characterName} gameLang={gameLanguage} packLang={voicePackLanguage} sameLang={sameLanguage}", LogLevel.Trace);
                            Monitor.Log($"[VOICE] Displayed: \"{currentDisplayedString}\"", LogLevel.Trace);
                            Monitor.Log($"[VOICE] LookupKey(text): \"{finalLookupKey}\"", LogLevel.Trace);
                            if (!string.IsNullOrEmpty(resolvedFrom))
                                Monitor.Log($"[VOICE] Dictionary resolved DialogueFrom: \"{resolvedFrom}\"", LogLevel.Trace);
                        }

                        if (!string.IsNullOrEmpty(resolvedFrom))
                        {
                            // Prefer DialogueFrom-based playback when we can resolve it
                            TryToPlayVoiceFromDialogueKey(characterName, resolvedFrom, currentLanguageCode);
                            _lastPlayedLookupKey = finalLookupKey;
                            return;
                        }

                        // --- Fallback behavior ---
                        // If dictionary didn't resolve, use your original text-based lookup
                        TryToPlayVoice(characterName, finalLookupKey, currentLanguageCode);
                        _lastPlayedLookupKey = finalLookupKey;
                    }

                }
            }
            // No longer reset immediately here; handled by debounce above
        }

        private void ResetDialogueState()
        {
            lastDialogueText = null;
            lastSpeakerName = null;
            wasDialogueUpLastTick = false;

            // reset debounce/stability guards
            _dialogueNotVisibleTicks = 0;
            _sameLineStableTicks = 0;
            _lastPlayedLookupKey = null;

            if (currentVoiceInstance != null && !currentVoiceInstance.IsDisposed && currentVoiceInstance.State == SoundState.Playing)
            {
                try { currentVoiceInstance.Stop(true); }
                catch (Exception ex) { Monitor.Log($"Error stopping voice instance during dialogue reset: {ex.Message}", LogLevel.Warn); }
            }

            CurrentDialogueSpeaker = null;
            CurrentDialogueOriginalKey = null;
            IsMultiPageDialogueActive = false;
        }


       

        private Event GetCurrentEvent()
        {
            // Try both; different SDV versions expose one or the other
            var ev = Game1.CurrentEvent;
            if (ev == null)
                ev = Game1.currentLocation?.currentEvent;
            return ev;
        }

        private bool IsFestivalEvent(Event ev)
        {
            try { return ev?.isFestival == true || (ev?.id?.StartsWith("festival_", StringComparison.OrdinalIgnoreCase) ?? false); }
            catch { return false; }
        }

        /// <summary>
        /// Build "Events/{Map}:{NumericId}" from the active event.
        /// </summary>
        private string BuildEventBaseKeyForCurrent(Event ev)
        {
            try
            {
                string map = Game1.currentLocation?.Name ?? "Unknown";
                // ev.id for regular events is like "6963327/f Abigail 3500/O Abigail/t 610 1700"
                // extract the leading number
                string id = ev?.id;
                if (string.IsNullOrWhiteSpace(id))
                    return null;

                var m = System.Text.RegularExpressions.Regex.Match(id, @"^(?<num>\d+)");
                if (!m.Success)
                    return null;

                string num = m.Groups["num"].Value;
                return $"Events/{map}:{num}";
            }
            catch { return null; }
        }

        /// <summary>
        /// Count how many speak-ish commands for this speaker occur up to and including the current command.
        /// This reproduces the s{index} we assigned at template time without Harmony.
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

            int current = commandIndexOverride >= 0 ? commandIndexOverride : ev.CurrentCommand; // public property
            if (current < 0) current = 0;
            if (current >= ev.eventCommands.Length) current = ev.eventCommands.Length - 1;

            int serial = 0;

            static bool IsSpeakCommand(string value) =>
                value.StartsWith("speak ", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("splitSpeak ", StringComparison.OrdinalIgnoreCase);

            static int CountSlots(string value)
            {
                if (!IsSpeakCommand(value))
                    return 0;

                bool split = value.StartsWith("splitSpeak ", StringComparison.OrdinalIgnoreCase);
                int branches = split ? 1 : value.Count(c => c == '^') + 1;
                int pages = split ? 0 : Regex.Matches(value, @"#?\$b#?").Count;
                return branches + pages;
            }

            static string FirstPageText(string value)
            {
                int quote = value.IndexOf('"');
                if (quote < 0) return string.Empty;
                string text = value.Substring(quote + 1);
                int endQuote = text.LastIndexOf('"');
                if (endQuote >= 0) text = text.Substring(0, endQuote);
                text = text.Split(new[] { "#$b#" }, StringSplitOptions.None)[0];
                text = text.Replace("$h", string.Empty).Replace("$s", string.Empty);
                return text.Trim();
            }

            static bool MatchesDisplayed(string commandText, string displayed)
            {
                if (string.IsNullOrWhiteSpace(commandText) || string.IsNullOrWhiteSpace(displayed))
                    return false;

                string expected = commandText.Replace("@", Utility.FilterUserName(Game1.player?.Name) ?? string.Empty);
                string actual = displayed.Trim();
                return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase) ||
                       actual.Contains(expected, StringComparison.OrdinalIgnoreCase) ||
                       expected.Contains(actual, StringComparison.OrdinalIgnoreCase);
            }

            bool skipQuickQuestionResponseCommand = false;
            for (int i = 0; i <= current; i++)
            {
                string line = ev.eventCommands[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Commands include normal dialogue and event speech bubbles.
                bool isSpeak = line.StartsWith("speak ", StringComparison.OrdinalIgnoreCase);
                bool isSplit = line.StartsWith("splitSpeak ", StringComparison.OrdinalIgnoreCase);
                bool isTextAboveHead = line.StartsWith("textAboveHead ", StringComparison.OrdinalIgnoreCase);

                // Stardew keeps the selected quickQuestion response as the
                // next runtime event command. Its serial was already assigned
                // from the answer alternatives above, so do not count this
                // materialized response a second time.
                if (skipQuickQuestionResponseCommand)
                {
                    if (isSpeak || isSplit || isTextAboveHead)
                    {
                        skipQuickQuestionResponseCommand = false;
                        continue;
                    }

                    // A pause or bookkeeping command may appear between the
                    // question and its materialized response.
                    if (!line.StartsWith("pause ", StringComparison.OrdinalIgnoreCase) &&
                        !line.StartsWith("addWorldState ", StringComparison.OrdinalIgnoreCase))
                    {
                        skipQuickQuestionResponseCommand = false;
                    }
                }

                if (line.StartsWith("quickQuestion ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] sections = line.Split(new[] { "(break)" }, StringSplitOptions.None);
                    int branchSerial = serial;
                    int selectedBranch = -1;

                    for (int branch = 1; branch < sections.Length; branch++)
                    {
                        string branchText = sections[branch];
                        string actorToken = branchText.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1)?.Trim().Trim('"').TrimEnd('?');

                        if ((i == current || current == i + 1) &&
                            selectedBranch < 0 &&
                            MatchesDisplayed(FirstPageText(branchText), currentDisplayedText))
                        {
                            selectedBranch = branch;
                            // Each player answer occupies one serial slot.
                            // The embedded speak text may contain several
                            // #$b# pages, but those pages are not separate
                            // answer alternatives in the generated pack.
                            return branchSerial + branch - 1;
                        }

                        serial++;
                    }

                    // The current page may be a later #$b# page from the
                    // selected embedded branch. Match it against every page
                    // in each branch before falling back to the branch start.
                    if ((i == current || current == i + 1) && selectedBranch < 0 && !string.IsNullOrWhiteSpace(currentDisplayedText))
                    {
                        for (int branch = 1; branch < sections.Length; branch++)
                        {
                            string branchText = sections[branch];
                            int firstQuote = branchText.IndexOf('"');
                            int lastQuote = branchText.LastIndexOf('"');
                            string branchPayload = firstQuote >= 0 && lastQuote > firstQuote
                                ? branchText.Substring(firstQuote + 1, lastQuote - firstQuote - 1)
                                : string.Empty;
                            string[] pages = string.IsNullOrWhiteSpace(branchPayload)
                                ? Array.Empty<string>()
                                : branchPayload.Split(new[] { "#$b#" }, StringSplitOptions.None);
                            for (int page = 0; page < pages.Length; page++)
                            {
                                if (MatchesDisplayed(pages[page].Replace("$h", string.Empty).Replace("$s", string.Empty), currentDisplayedText))
                                {
                                    return branchSerial + branch - 1;
                                }
                            }
                        }

                        // No displayed text matched this question's embedded
                        // branches. The event has already moved past the
                        // question, so count all answer alternatives and
                        // continue scanning later commands.
                        serial = branchSerial + Math.Max(0, sections.Length - 1);
                    }

                    skipQuickQuestionResponseCommand = true;
                    continue;
                }

                if (!isSpeak && !isSplit && !isTextAboveHead)
                    continue;

                // extract actor token (2nd token)
                string actor = null;
                var parts = line.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    actor = parts[1].Trim().Trim('"').TrimEnd('?'); // optional-NPC markers can appear with '?' suffix
                }

                if (actor != null && actor.Equals(speakerName, StringComparison.OrdinalIgnoreCase))
                {
                    // Gender alternatives occupy separate first-page slots, while
                    // #$b# pages after the selected branch are shared continuations.
                    // Do not multiply the continuation pages by the branch count.
                    int branchCount = isSpeak ? line.Count(c => c == '^') + 1 : 1;
                    int continuationPageCount = isSpeak
                        ? Regex.Matches(line, @"#?\$b#?").Count
                        : 0;

                    if (i == current)
                    {
                        int branchIndex = branchCount > 1 && Game1.player?.IsMale != true ? 1 : 0;
                        branchIndex = Math.Clamp(branchIndex, 0, branchCount - 1);

                        if (currentPageIndex <= 0)
                            return serial + branchIndex;

                        int continuationPage = Math.Clamp(
                            currentPageIndex - 1,
                            0,
                            Math.Max(0, continuationPageCount - 1));
                        return serial + branchCount + continuationPage;
                    }

                    serial += branchCount + continuationPageCount;
                }
            }

            return serial > 0 ? serial - 1 : 0;
        }


      





    }
}
