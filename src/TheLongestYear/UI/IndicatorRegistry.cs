using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Draws ? or ! bubble sprites above interactable world tiles to signal "new thing here".
    /// Dismissed indicators are persisted in <see cref="MetaState.DismissedIndicators"/> so
    /// they don't re-appear across resets.
    ///
    /// Usage:
    ///   IndicatorRegistry.Register("tly.cookbook", farmhouse, new Vector2(5, 7), IndicatorKind.Question);
    ///   IndicatorRegistry.Dismiss("tly.cookbook");  // call when the player first opens the menu
    ///   bool show = IndicatorRegistry.IsActive("tly.cookbook");
    /// </summary>
    internal static class IndicatorRegistry
    {
        // Source rects inside Game1.mouseCursors (1× pixel coords from the vanilla spritesheet).
        // Question-mark bubble: used for "new undiscovered thing here".
        private static readonly Rectangle QuestionSourceRect   = new Rectangle(397, 489, 10, 10);
        // Exclamation bubble: used for "urgent action needed".
        private static readonly Rectangle ExclamationSourceRect = new Rectangle(410, 501, 10, 10);

        private const float Scale   = 3f;  // renders at 30×30 px at 100% zoom
        private const int   OffsetY = 32;  // pixels above tile top edge (world-space pre-zoom)

        private sealed class Entry
        {
            public GameLocation Location { get; }
            public Vector2 Tile { get; }
            public IndicatorKind Kind { get; }

            public Entry(GameLocation location, Vector2 tile, IndicatorKind kind)
            {
                Location = location;
                Tile     = tile;
                Kind     = kind;
            }
        }

        private static readonly Dictionary<string, Entry> _entries = new();
        private static MetaState _meta;

        /// <summary>Call once from ModEntry.OnSaveLoaded to link the registry to the live MetaState.</summary>
        public static void Attach(MetaState meta) => _meta = meta;

        /// <summary>Call from ModEntry.OnSaveLoaded to reset the in-memory registration table
        /// (previous run's locations are stale after loadForNewGame).</summary>
        public static void ClearRegistrations() => _entries.Clear();

        /// <summary>Register a bubble for the given id, location, and tile. Safe to call multiple
        /// times — re-registration updates the entry (e.g. location object changes after reset).</summary>
        public static void Register(string id, GameLocation location, Vector2 tile, IndicatorKind kind)
        {
            _entries[id] = new Entry(location, tile, kind);
        }

        /// <summary>Dismiss this indicator. Writes to <see cref="MetaState.DismissedIndicators"/>
        /// so the dismissed state survives across sessions and resets.</summary>
        public static void Dismiss(string id)
        {
            _meta?.DismissedIndicators.Add(id);
        }

        /// <summary>True when the indicator is registered, not dismissed, and its location is
        /// the current location (so we only draw in the right room).</summary>
        public static bool IsActive(string id)
        {
            if (_meta == null) return false;
            if (_meta.DismissedIndicators.Contains(id)) return false;
            return _entries.ContainsKey(id);
        }

        /// <summary>SMAPI RenderedWorld handler. Call from ModEntry.</summary>
        public static void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (_meta == null) return;

            foreach (var kvp in _entries)
            {
                string id    = kvp.Key;
                Entry  entry = kvp.Value;

                // Only draw if not dismissed and the current location matches.
                if (_meta.DismissedIndicators.Contains(id)) continue;
                if (Game1.currentLocation != entry.Location) continue;

                DrawBubble(e.SpriteBatch, entry.Tile, entry.Kind);
            }
        }

        private static void DrawBubble(SpriteBatch b, Vector2 tile, IndicatorKind kind)
        {
            Rectangle src = kind == IndicatorKind.Question ? QuestionSourceRect : ExclamationSourceRect;

            // Convert tile coords to screen coords: tile * tileSize → world coords
            // → subtract viewport origin → apply zoom.
            float worldX = tile.X * Game1.tileSize + (Game1.tileSize / 2f) - (src.Width * Scale / 2f);
            float worldY = tile.Y * Game1.tileSize - OffsetY;

            Vector2 screenPos = new Vector2(
                (worldX - Game1.viewport.X) * Game1.options.zoomLevel,
                (worldY - Game1.viewport.Y) * Game1.options.zoomLevel);

            b.Draw(Game1.mouseCursors, screenPos, src,
                Color.White, 0f, Vector2.Zero,
                Scale * Game1.options.zoomLevel, SpriteEffects.None, 1f);
        }
    }

    internal enum IndicatorKind { Question, Exclamation }
}
