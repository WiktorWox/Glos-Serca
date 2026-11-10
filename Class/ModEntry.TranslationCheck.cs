using System;
using System.IO;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace VoiceOverFrameworkMod
{
    /// <summary>
    /// The pack's numbering and texts come from one version of the Polish translation (declared in the
    /// voice pack manifest under "Translation"). Compare it with what is installed and tell the player.
    /// </summary>
    public partial class ModEntry
    {
        private sealed class TranslationRequirement
        {
            public string UniqueID;
            public string Name;
            public ISemanticVersion Version;
        }

        private enum TranslationStatus { Unknown, Missing, Older, Newer, Ok }

        private TranslationRequirement _translationRequirement;
        private bool _translationChecked;

        private TranslationRequirement LoadTranslationRequirement()
        {
            try
            {
                string path = Path.Combine(this.Helper.DirectoryPath, "GłosSerca", "VoicePack", "manifest.json");
                if (!File.Exists(path)) return null;
                var node = JObject.Parse(File.ReadAllText(path))["Translation"] as JObject;
                if (node == null) return null;
                string id = node.Value<string>("UniqueID");
                string ver = node.Value<string>("Version");
                if (string.IsNullOrWhiteSpace(id) || !SemanticVersion.TryParse(ver, out ISemanticVersion parsed)) return null;
                return new TranslationRequirement { UniqueID = id, Name = node.Value<string>("Name") ?? id, Version = parsed };
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"[Translation] cannot read requirement from the pack manifest: {ex.Message}", LogLevel.Debug);
                return null;
            }
        }

        private (TranslationStatus status, ISemanticVersion installed) GetTranslationStatus()
        {
            _translationRequirement ??= LoadTranslationRequirement();
            var req = _translationRequirement;
            if (req == null) return (TranslationStatus.Unknown, null);
            var mod = this.Helper.ModRegistry.Get(req.UniqueID);
            if (mod == null) return (TranslationStatus.Missing, null);
            var installed = mod.Manifest.Version;
            if (installed.IsOlderThan(req.Version)) return (TranslationStatus.Older, installed);
            if (installed.IsNewerThan(req.Version)) return (TranslationStatus.Newer, installed);
            return (TranslationStatus.Ok, installed);
        }

        private string TranslationStatusText()
        {
            var (status, installed) = GetTranslationStatus();
            var req = _translationRequirement;
            if (status == TranslationStatus.Unknown || req == null) return null;
            string key = status switch
            {
                TranslationStatus.Missing => "translation.missing",
                TranslationStatus.Older => "translation.older",
                TranslationStatus.Newer => "translation.newer",
                _ => "translation.ok",
            };
            return this.Helper.Translation.Get(key, new { name = req.Name, installed = installed?.ToString() ?? "-", required = req.Version.ToString() });
        }

        /// <summary>Once per session, after the first save is loaded: log and, when something is off, show it on screen.</summary>
        private void OnSaveLoaded_TranslationCheck(object sender, SaveLoadedEventArgs e)
        {
            if (_translationChecked) return;
            _translationChecked = true;
            var (status, _) = GetTranslationStatus();
            string text = TranslationStatusText();
            if (text == null) return;
            switch (status)
            {
                case TranslationStatus.Missing:
                case TranslationStatus.Older:
                    this.Monitor.Log(text, LogLevel.Warn);
                    Game1.addHUDMessage(new HUDMessage(text, HUDMessage.error_type));
                    break;
                case TranslationStatus.Newer:
                    this.Monitor.Log(text, LogLevel.Warn);
                    Game1.addHUDMessage(HUDMessage.ForCornerTextbox(text));
                    break;
                default:
                    this.Monitor.Log(text, LogLevel.Info);
                    break;
            }
        }
    }
}
