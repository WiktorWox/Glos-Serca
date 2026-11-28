using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace VoiceOverFrameworkMod
{
    /// <summary>
    /// Captions drawn by the game's own cutscene code, outside any DialogueBox: the tea cutscene in
    /// Caroline's sunroom event (EventScript_GreenTea), Maru's comet minigame (MaruComet) and Abigail's
    /// remarks while playing Prairie King together (AbigailGame). The text is read from the running
    /// object by reflection and voiced through the pack entry whose TranslationKey is the string key.
    /// </summary>
    public partial class ModEntry
    {
        private sealed class CaptionSource
        {
            public string Name;          // for logs and per-source state
            public string Character;     // voice pack owner
            public string[] GameKeys;    // keys as the game loads them (backslash form)
        }

        private static readonly CaptionSource GreenTeaCaptions = new()
        {
            Name = "GreenTea", Character = "Caroline",
            GameKeys = Enumerable.Range(1, 3).Select(i => $"Strings\\Locations:Caroline_Tea_Event{i}").ToArray(),
        };
        private static readonly CaptionSource MaruCometCaptions = new()
        {
            Name = "MaruComet", Character = "Maru",
            GameKeys = Enumerable.Range(1, 5).Select(i => $"Strings\\Events:Maru_comet{i}").ToArray(),
        };
        private static readonly CaptionSource AbigailGameCaptions = new()
        {
            Name = "AbigailGame", Character = "Abigail",
            GameKeys = new[] { 11896, 11897, 11898, 11901, 11902 }.Select(i => $"Strings\\StringsFromCSFiles:AbigailGame.cs.{i}").ToArray(),
        };

        private readonly Dictionary<string, string> _lastCaptionBySource = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _captionTextCache = new(StringComparer.Ordinal);

        private void CheckForCutsceneCaptions()
        {
            // 1) custom event script inside an event (tea cutscene)
            object script = Game1.currentLocation?.currentEvent?.currentCustomEventScript;
            if (script != null && script.GetType().Name == "EventScript_GreenTea")
                HandleCaption(GreenTeaCaptions, ReadField<string>(script, "text"));
            else
                _lastCaptionBySource.Remove(GreenTeaCaptions.Name);

            // 2) minigames started by events
            object minigame = Game1.currentMinigame;
            string minigameType = minigame?.GetType().Name;

            if (minigameType == "MaruComet")
                HandleCaption(MaruCometCaptions, ReadField<string>(minigame, "currentString"));
            else
                _lastCaptionBySource.Remove(MaruCometCaptions.Name);

            if (minigameType == "AbigailGame" && ReadField<int>(minigame, "abigailPortraitTimer") > 0)
                HandleCaption(AbigailGameCaptions, ReadField<string>(minigame, "AbigailDialogue"));
            else
                _lastCaptionBySource.Remove(AbigailGameCaptions.Name);
        }

        private void HandleCaption(CaptionSource source, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _lastCaptionBySource.Remove(source.Name);
                return;
            }
            if (_lastCaptionBySource.TryGetValue(source.Name, out var last) && string.Equals(last, text, StringComparison.Ordinal))
                return;
            _lastCaptionBySource[source.Name] = text;

            string gameKey = source.GameKeys.FirstOrDefault(k => string.Equals(LoadCaptionText(k), text, StringComparison.Ordinal));
            if (Config.developerModeOn)
                Monitor.Log($"[V2-CAPTION] source={source.Name} character='{source.Character}' key='{gameKey ?? "?"}' text='{text}'", LogLevel.Debug);
            if (gameKey == null)
                return;

            // non-event entries are indexed with their page suffix (":p0"); try that first, then the bare key
            string packKey = gameKey.Replace('\\', '/');
            TryToPlayVoiceByTranslationKeys(source.Character, new[] { $"{packKey}:p0", packKey }, LocalizedContentManager.CurrentLanguageCode);
        }

        private string LoadCaptionText(string gameKey)
        {
            if (_captionTextCache.TryGetValue(gameKey, out var cached))
                return cached;
            string value;
            try { value = Game1.content.LoadString(gameKey); }
            catch { value = null; }
            _captionTextCache[gameKey] = value;
            return value;
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            try
            {
                var field = AccessTools.Field(target.GetType(), fieldName);
                return field?.GetValue(target) is T value ? value : default;
            }
            catch { return default; }
        }

        /// <summary>Forget cached caption strings (e.g. after a language change).</summary>
        private void ResetCaptionCache()
        {
            _captionTextCache.Clear();
            _lastCaptionBySource.Clear();
        }
    }
}
