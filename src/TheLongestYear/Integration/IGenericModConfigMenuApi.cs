using System;
using StardewModdingAPI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Generic Mod Config Menu API surface. We use a local interface declaration so the
    /// project compiles without depending on the GMCM assembly. Reflection-based API
    /// resolution happens at runtime via Helper.ModRegistry.GetApi.
    ///
    /// Reference: https://github.com/spacechase0/StardewValleyMods/blob/develop/GenericModConfigMenu/IGenericModConfigMenuApi.cs
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue,
            Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddParagraph(IManifest mod, Func<string> text);
    }
}
