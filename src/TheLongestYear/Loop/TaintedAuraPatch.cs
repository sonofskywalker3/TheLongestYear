using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>The dark aura (spec 2026-09-21): every copy of the exact item tampered away (its
    /// flavour included, so Dried Apples glow and Dried Cucumbers do not) is drawn
    /// with a pulsing dim purple glow under the sprite wherever an item is drawn in a menu or
    /// held overhead. Both prefixes only draw the glow; the original draw then runs. Patched
    /// manually from ModEntry so a signature mismatch fails loudly at startup. Nothing glows on a
    /// save without an active run, and a prefix never throws into the item's own draw: the first
    /// failure is logged and the aura stays quiet after it.</summary>
    internal static class TaintedAuraPatch
    {
        private static readonly Color AuraColor = new(120, 20, 180);
        private const int GlowTextureSize = 64;
        private const float MenuAuraScale = 1.6f;
        private const float HeldAuraScale = 1.5f;
        private const float CenterOffset = 32f;
        private const float DepthStep = 0.0001f;
        private const float HeldDepthDivisor = 10000f;
        // Vanilla draws the held sprite at StandingPixel.Y + 3 (+ 4 for a ColoredObject's tint layer);
        // 3 keeps the aura at the base sprite's depth, under both.
        private const int HeldDepthPixels = 3;

        /// <summary>Set by ModEntry: what was tampered away this loop, refreshed and ready to ask.</summary>
        internal static Func<TaintedItems> Tainted;

        private static readonly Type[] DrawInMenuArgs =
        {
            typeof(SpriteBatch), typeof(Vector2), typeof(float), typeof(float), typeof(float),
            typeof(StackDrawType), typeof(Color), typeof(bool),
        };

        private static readonly Type[] DrawWhenHeldArgs =
        {
            typeof(SpriteBatch), typeof(Vector2), typeof(Farmer),
        };

        internal static void Apply(Harmony harmony)
        {
            // ColoredObject (coloured flowers, roe, aged roe, dyed goods) overrides both draws
            // without calling base for the menu draw, so the Object prefixes never see it.
            harmony.Patch(
                AccessTools.Method(typeof(StardewValley.Objects.ColoredObject), nameof(StardewValley.Objects.ColoredObject.drawInMenu), DrawInMenuArgs)
                    ?? throw new MissingMethodException("ColoredObject.drawInMenu"),
                prefix: new HarmonyMethod(typeof(TaintedAuraPatch), nameof(MenuPrefix)));
            harmony.Patch(
                AccessTools.Method(typeof(StardewValley.Objects.ColoredObject), nameof(StardewValley.Objects.ColoredObject.drawWhenHeld), DrawWhenHeldArgs)
                    ?? throw new MissingMethodException("ColoredObject.drawWhenHeld"),
                prefix: new HarmonyMethod(typeof(TaintedAuraPatch), nameof(ColoredHeldPrefix)));
            harmony.Patch(
                AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.drawInMenu), DrawInMenuArgs)
                    ?? throw new MissingMethodException("Object.drawInMenu"),
                prefix: new HarmonyMethod(typeof(TaintedAuraPatch), nameof(MenuPrefix)));
            harmony.Patch(
                AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.drawWhenHeld), DrawWhenHeldArgs)
                    ?? throw new MissingMethodException("Object.drawWhenHeld"),
                prefix: new HarmonyMethod(typeof(TaintedAuraPatch), nameof(HeldPrefix)));
        }

        private static Texture2D _glow;
        /// <summary>The device the texture belongs to. A graphics device reset throws every texture
        /// made on the old one away, so the next draw notices and builds a fresh one.</summary>
        private static GraphicsDevice _builtOn;
        /// <summary>True once a prefix has thrown. The failure is logged once and the aura stays
        /// off for the session rather than logging every frame.</summary>
        private static bool _failed;

        /// <summary>A white soft-edged disc, built once per graphics device. Game1.shadowTexture
        /// cannot carry the colour: its pixels are black, so tinting it only darkens.</summary>
        private static Texture2D Glow(GraphicsDevice device)
        {
            if (_glow != null && !_glow.IsDisposed && ReferenceEquals(_builtOn, device)) return _glow;
            Texture2D old = _glow;
            _glow = null;
            _builtOn = null;
            if (old != null && !old.IsDisposed) old.Dispose();
            var pixels = new Color[GlowTextureSize * GlowTextureSize];
            float half = GlowTextureSize / 2f;
            for (int y = 0; y < GlowTextureSize; y++)
            for (int x = 0; x < GlowTextureSize; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float a = TaintedItems.Falloff((float)Math.Sqrt(dx * dx + dy * dy));
                pixels[y * GlowTextureSize + x] = Color.White * a; // premultiplied
            }
            var made = new Texture2D(device, GlowTextureSize, GlowTextureSize);
            made.SetData(pixels);
            _glow = made;
            _builtOn = device;
            return _glow;
        }

        /// <summary>The exact item, flavour included (designer, 2026-10-07): a Dried Apple's flavour
        /// is its preservedParentSheetIndex, the bare id of the apple (ObjectDataDefinition
        /// .CreateFlavoredDriedFruit). Reads two existing strings, so it allocates nothing.</summary>
        private static bool IsTainted(StardewValley.Object item)
        {
            if (!RunActivation.IsActive) return false;
            TaintedItems tainted = Tainted?.Invoke();
            return tainted != null && tainted.IsTainted(item.QualifiedItemId, item.preservedParentSheetIndex.Value);
        }

        private static void DrawAura(SpriteBatch b, Vector2 center, float scale, float depth)
        {
            if (Game1.currentGameTime == null || b.GraphicsDevice == null) return;
            Texture2D tex = Glow(b.GraphicsDevice);
            float pulse = TaintedItems.Pulse(Game1.currentGameTime.TotalGameTime.TotalMilliseconds);
            b.Draw(tex, center, tex.Bounds, AuraColor * pulse, 0f,
                new Vector2(tex.Width / 2f, tex.Height / 2f), scale, SpriteEffects.None,
                Math.Max(0f, depth - DepthStep));
        }

        /// <summary>Log the first failure, then switch the aura off for the session: a draw prefix
        /// that throws would take the item's own draw down with it.</summary>
        private static void Fail(string where, Exception ex)
        {
            if (_failed) return;
            _failed = true;
            PatchLog.Warn($"Darkness: the tainted aura failed in {where}, so tainted items draw without it for the rest of the session. {ex}");
        }

        private static void MenuPrefix(StardewValley.Object __instance, SpriteBatch spriteBatch, Vector2 location, float scaleSize, float layerDepth)
        {
            if (_failed) return;
            try
            {
                if (!IsTainted(__instance)) return;
                DrawAura(spriteBatch, location + new Vector2(CenterOffset, CenterOffset) * scaleSize,
                    MenuAuraScale * scaleSize, layerDepth);
            }
            catch (Exception ex)
            {
                Fail("drawInMenu", ex);
            }
        }

        private static void HeldPrefix(StardewValley.Object __instance, SpriteBatch spriteBatch, Vector2 objectPosition, Farmer f)
        {
            // ColoredObject.drawWhenHeld calls Object.drawWhenHeld in one branch. Its own prefix
            // has already drawn the aura, so the base call must not draw a second one.
            if (__instance is StardewValley.Objects.ColoredObject) return;
            ColoredHeldPrefix(__instance, spriteBatch, objectPosition, f);
        }

        private static void ColoredHeldPrefix(StardewValley.Object __instance, SpriteBatch spriteBatch, Vector2 objectPosition, Farmer f)
        {
            if (_failed) return;
            try
            {
                if (!IsTainted(__instance)) return;
                // objectPosition is the sprite's top-left; a 16px sprite at 4x is a 64px square.
                float depth = Math.Max(0f, (f.StandingPixel.Y + HeldDepthPixels) / HeldDepthDivisor);
                DrawAura(spriteBatch, objectPosition + new Vector2(CenterOffset, CenterOffset), HeldAuraScale, depth);
            }
            catch (Exception ex)
            {
                Fail("drawWhenHeld", ex);
            }
        }
    }
}
