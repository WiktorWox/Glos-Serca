// ModEntry.SpeechBubblePoller.cs
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;
using System.Linq;

namespace VoiceOverFrameworkMod
{
    public partial class ModEntry
    {
        // Non-public NPC fields (SDV 1.6) via Harmony
        private static readonly AccessTools.FieldRef<NPC, string> NPC_textAboveHead
            = AccessTools.FieldRefAccess<NPC, string>("textAboveHead");
        private static readonly AccessTools.FieldRef<NPC, int> NPC_textAboveHeadTimer
            = AccessTools.FieldRefAccess<NPC, int>("textAboveHeadTimer");
        private static readonly AccessTools.FieldRef<NPC, int> NPC_textAboveHeadPreTimer
            = AccessTools.FieldRefAccess<NPC, int>("textAboveHeadPreTimer");
        private static readonly AccessTools.FieldRef<NPC, float> NPC_textAboveHeadAlpha
            = AccessTools.FieldRefAccess<NPC, float>("textAboveHeadAlpha");

        /// <summary>Poll NPC speech bubbles and play audio once per bubble instance.</summary>
        private void CheckForSpeechBubblesLevel1()
        {
            // Event textAboveHead commands are rendered while the event's
            // DialogueBox can still be active. Handle those here; retain the
            // old behavior for ordinary world bubbles.
            bool eventActive = Game1.currentLocation?.currentEvent != null;
            if (!eventActive && Game1.activeClickableMenu is StardewValley.Menus.DialogueBox)
                return;

            var npcs = EnumerateBubbleSpeakers().OfType<NPC>().Distinct();
            if (npcs == null) return;

            foreach (var npc in npcs)
            {
                if (npc == null) continue;

                string text = NPC_textAboveHead(npc);
                int timer = NPC_textAboveHeadTimer(npc);
                int pre = NPC_textAboveHeadPreTimer(npc);
                float alpha = NPC_textAboveHeadAlpha(npc);

                // Bubble gone → reset state
                if (string.IsNullOrWhiteSpace(text) || timer <= 0)
                {
                    if (_bubbleStates.TryGetValue(npc, out var gone))
                    {
                        gone.LastText = null;
                        gone.LastTimer = -1;
                        gone.Played = false;
                    }
                    continue;
                }

                // Must be actually visible
                if (pre > 0 || alpha <= 0f)
                    continue;

                var state = _bubbleStates.GetOrCreateValue(npc);

                // Detect new bubble instance
                bool isNewInstance =
                    state.LastText == null
                    || !string.Equals(text, state.LastText, StringComparison.Ordinal)
                    || timer > state.LastTimer + 5; // timer reset/jump = new bubble

                if (isNewInstance)
                {
                    state.LastText = text;
                    state.LastTimer = timer;
                    state.Played = false;
                }
                else
                {
                    state.LastTimer = timer;
                }

                if (state.Played)
                    continue;

                string characterName = npc.Name;
                var pack = GetSelectedVoicePack(characterName);
                if (pack == null)
                {
                    state.Played = true;
                    continue;
                }

                string patternKey = SanitizeBubbleText(text);
                if (string.IsNullOrWhiteSpace(patternKey))
                {
                    state.Played = true;
                    continue;
                }

                string fullPath = null;

                // Event bubbles have a stable event TranslationKey even when
                // their displayed text is localized or sanitized differently.
                if (eventActive && pack.FormatMajor >= 2)
                {
                    var currentEvent = Game1.currentLocation.currentEvent;
                    int bubbleCommandIndex = -1;
                    int eventCommand = currentEvent?.CurrentCommand ?? -1;
                    for (int commandIndex = Math.Min(eventCommand, currentEvent?.eventCommands?.Length - 1 ?? -1); commandIndex >= 0; commandIndex--)
                    {
                        string command = currentEvent.eventCommands[commandIndex];
                        if (!command.StartsWith("textAboveHead ", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var commandParts = command.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                        if (commandParts.Length >= 3 &&
                            commandParts[1].Trim().Trim('"').TrimEnd('?').Equals(characterName, StringComparison.OrdinalIgnoreCase) &&
                            commandParts[2].Contains(text, StringComparison.Ordinal))
                        {
                            bubbleCommandIndex = commandIndex;
                            break;
                        }
                    }

                    int? eventSpeakIndex = bubbleCommandIndex >= 0
                        ? ComputeSpeakIndexForCurrent(currentEvent, characterName, 0, bubbleCommandIndex)
                        : ComputeSpeakIndexForCurrent(currentEvent, characterName, 0);
                    string eventBaseKey = BuildEventBaseKeyForCurrent(currentEvent);
                    if (Config.developerModeOn)
                        Monitor.Log($"[V2-BUBBLE] actor='{characterName}' text='{text}' commandIndex={bubbleCommandIndex} speakIndex={(eventSpeakIndex.HasValue ? eventSpeakIndex.Value.ToString() : "null")} base='{eventBaseKey}' pack='{pack.VoicePackName}'", LogLevel.Debug);
                    if (!string.IsNullOrWhiteSpace(eventBaseKey) && eventSpeakIndex.HasValue)
                    {
                        string eventTranslationKey = $"{eventBaseKey}:s{eventSpeakIndex.Value}";
                        if (TryToPlayVoiceByTranslationKeys(
                                characterName,
                                new[] { eventTranslationKey },
                                LocalizedContentManager.CurrentLanguageCode,
                                interruptCurrent: false))
                        {
                            state.Played = true;
                            continue;
                        }
                    }
                }

                if (pack.FormatMajor >= 2)
                {
                    if (pack.EntriesByDisplayPattern != null &&
                        pack.EntriesByDisplayPattern.TryGetValue(patternKey, out var relA))
                    {
                        fullPath = PathUtilities.NormalizePath(Path.Combine(pack.BaseAssetPath, relA));
                    }
                    else if (pack.Entries != null &&
                             pack.Entries.TryGetValue(patternKey, out var relB))
                    {
                        fullPath = PathUtilities.NormalizePath(Path.Combine(pack.BaseAssetPath, relB));
                    }
                }
                else
                {
                    // V1 fallback
                    TryToPlayVoice(characterName, patternKey, LocalizedContentManager.CurrentLanguageCode);
                    state.Played = true;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    if (Config.developerModeOn)
                        Monitor.Log($"[Bubble] {characterName}: \"{text}\" → \"{patternKey}\"", LogLevel.Info);

                    if (File.Exists(fullPath))
                    {
                        PlayVoiceFromFile(fullPath, interruptCurrent: false);
                        state.Played = true;
                    }
                    else
                    {
                        if (_collectV2Failures)
                            V2AddFailure(
                                speaker: characterName,
                                dialogue: text,
                                removables: Enumerable.Empty<string>(),
                                stripped: SanitizeBubbleText(text),
                                key: patternKey,
                                matched: true,
                                missingAudio: true,
                                audioPath: fullPath
                            );
                        state.Played = true;
                    }
                }
                else
                {
                    if (_collectV2Failures)
                        V2AddFailure(
                            speaker: characterName,
                            dialogue: text,
                            removables: Enumerable.Empty<string>(),
                            stripped: SanitizeBubbleText(text),
                            key: patternKey,
                            matched: false,
                            missingAudio: false
                        );

                    if (Config.developerModeOn)
                        Monitor.Log($"[Bubble] No pack match for {characterName} pattern \"{patternKey}\".", LogLevel.Trace);

                    state.Played = true;
                }
            }
        }



    }
}
