using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace fishingWithStudy.Config
{
    /// <summary>Minimal GMCM API interface for the methods we use.</summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void Unregister(IManifest mod);

        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue,
            Func<string> name, Func<string>? tooltip = null, string? fieldId = null);

        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue,
            Func<string> name, Func<string>? tooltip = null, int? min = null,
            int? max = null, int? interval = null, string? fieldId = null);

        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue,
            Func<string> name, Func<string>? tooltip = null,
            string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null,
            string? fieldId = null);

        void AddKeybindList(IManifest mod, Func<KeybindList> getValue,
            Action<KeybindList> setValue, Func<string> name, Func<string>? tooltip = null,
            string? fieldId = null);

        void OnFieldChanged(IManifest mod, Action<string, object> onChange);

        void OpenModMenu(IManifest mod);
    }
}
