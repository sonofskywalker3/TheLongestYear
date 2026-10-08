using System;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Reaches Tech's Cross-Mod Bundles (Nexus 51035) by reflection for
    /// <see cref="TechBundlesReroll"/>. Its board generator is a private static method, run by the
    /// mod itself only when a save is created; calling it rolls a new board with Game1.random,
    /// stores it as the mod's Data/Bundles and writes it to the world. TLY names the type and
    /// method only; none of that mod's code or data is copied here.</summary>
    internal sealed class TechCrossModBundlesTarget : ITechBundlesRerollTarget
    {
        private const string TypeName = "TechsCrossModBundles.ModEntry";
        private const string MethodName = "GenerateBundles";

        private readonly IModRegistry _registry;

        public TechCrossModBundlesTarget(IModRegistry registry)
        {
            _registry = registry;
        }

        public bool IsLoaded => _registry.IsLoaded(TechBundlesReroll.ModId);

        public void Reroll()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(TypeName, throwOnError: false))
                .FirstOrDefault(t => t != null)
                ?? throw new TypeLoadException($"{TypeName} not found");
            MethodInfo method = type.GetMethod(MethodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                ?? throw new MissingMethodException(TypeName, MethodName);
            // GenerateBundles(object sender, SaveCreatedEventArgs e = null): both arguments unused.
            object[] args = method.GetParameters().Select(_ => (object)null).ToArray();
            method.Invoke(null, args);
        }
    }
}
