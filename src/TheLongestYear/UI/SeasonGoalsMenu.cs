using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Loop;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Per-season goal tracker (UX2 from the 2026-05-26 playtest). The weekly hub is the SELECTION
    /// surface; bundle progress belongs on its own page so the hub stays focused on "pick a theme."
    /// This menu lists every classified bundle for the current run with:
    ///   - bundle name + theme tag,
    ///   - donated / required count (toward full bundle completion),
    ///   - badge "needs N before {NextSeason} 1" (or empty when this season's checkpoint is met),
    ///   - icons for the still-missing ingredients that gate THIS season's checkpoint.
    /// </summary>
    internal sealed class SeasonGoalsMenu : IClickableMenu
    {
        private const int PanelWidth = 1180;
        private const int PanelHeight = 760;
        private const int PanelPadding = 32;
        private const int TitleBarHeight = 80;

        private const int RowHeight = 84;
        private const int RowSpacing = 8;
        private const int IngredientIconSize = 36;
        private const int IngredientIconGap = 4;
        private const int IngredientIconY = 44;        // offset from row top

        private const int RowIdBase = 8000;
        private const int ScrollUpId = 8900;
        private const int ScrollDownId = 8901;

        private readonly IMonitor _monitor;
        private readonly RunState _run;
        private readonly IReadOnlyList<BundleRequirement> _requirements;
        private readonly CoreSeason _season;

        private List<BundleEntry> _entries = new();
        private int _scrollIndex;
        private int _rowsPerPage;

        private readonly List<ClickableComponent> _rowSlots = new();
        private ClickableTextureComponent _scrollUp;
        private ClickableTextureComponent _scrollDown;

        private string _hoverText = "";

        public SeasonGoalsMenu(IMonitor monitor, RunState run, IReadOnlyList<BundleRequirement> requirements)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _monitor = monitor;
            _run = run;
            _requirements = requirements ?? new List<BundleRequirement>();
            _season = run.Season;

            BuildEntries();
            RecomputeBoundsAndLayout();

            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                this.snapToDefaultClickableComponent();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeBoundsAndLayout();
        }

        // ---------- data ----------

        private void BuildEntries()
        {
            ISet<string> donated = _run.DonatedSet();
            _entries.Clear();
            // Order by theme then bundle name so the same physical menu position holds the same
            // bundle session-over-session (less scanning churn for the player).
            foreach (var br in _requirements
                .OrderBy(b => b.Theme)
                .ThenBy(b => b.Name, StringComparer.Ordinal))
            {
                // Only show bundles with an obligation due BY this season's checkpoint —
                // Seasonal bundles for the current season, PerItem bundles with any pin
                // due ≤ current, Percentage bundles with a non-zero quota this season.
                // Past-season Seasonal and future-only PerItem bundles are hidden; this is
                // the "what do I owe THIS season" tracker, not a year-wide overview.
                if (!IsRelevantForCurrentSeason(br)) continue;

                int have = br.Ingredients.Count(donated.Contains);
                int need = br.NumberOfSlots;

                IReadOnlyList<string> missingThisSeason = MissingForSeason(br, donated, _season);
                int missingCount = missingThisSeason.Count;

                _entries.Add(new BundleEntry(br, have, need, missingCount, missingThisSeason));
            }
        }

        /// <summary>True if this bundle has any obligation that's due BY the current season's
        /// day-28 checkpoint. Mirrors the kind-specific shape:
        /// Seasonal — its season matches the current one.
        /// PerItem — at least one pin has season ≤ current.
        /// Percentage — cumulative quota for the current season is &gt; 0.</summary>
        private bool IsRelevantForCurrentSeason(BundleRequirement br)
        {
            switch (br.Kind)
            {
                case BundleKind.Seasonal:
                    return br.SeasonalSeason!.Value == _season;
                case BundleKind.PerItem:
                    return br.ItemSeasonPins!.Any(kv => (int)kv.Value <= (int)_season);
                case BundleKind.Percentage:
                    return br.CumulativeRequiredBySeason![(int)_season] > 0;
                default:
                    return false;
            }
        }

        /// <summary>Ingredients that this bundle still owes for the <paramref name="season"/>
        /// checkpoint (the predicate <see cref="BundleRequirement.IsSatisfiedAtSeasonEnd"/> spelled out).
        /// Returns IDs in a stable order so the icon row position is consistent.</summary>
        private static IReadOnlyList<string> MissingForSeason(
            BundleRequirement br, ISet<string> donated, CoreSeason season)
        {
            switch (br.Kind)
            {
                case BundleKind.Seasonal:
                    if (br.SeasonalSeason!.Value != season) return Array.Empty<string>();
                    return br.Ingredients.Where(i => !donated.Contains(i))
                        .OrderBy(s => s, StringComparer.Ordinal).ToList();

                case BundleKind.PerItem:
                    return br.ItemSeasonPins!
                        .Where(kv => (int)kv.Value <= (int)season && !donated.Contains(kv.Key))
                        .Select(kv => kv.Key)
                        .OrderBy(s => s, StringComparer.Ordinal)
                        .ToList();

                case BundleKind.Percentage:
                    int required = br.CumulativeRequiredBySeason![(int)season];
                    int have = br.Ingredients.Count(donated.Contains);
                    if (have >= required) return Array.Empty<string>();
                    // Show the not-yet-donated ones — player can choose any to satisfy quota.
                    return br.Ingredients.Where(i => !donated.Contains(i))
                        .OrderBy(s => s, StringComparer.Ordinal).ToList();

                default:
                    return Array.Empty<string>();
            }
        }

        // ---------- layout ----------

        private void RecomputeBoundsAndLayout()
        {
            width = Math.Min(PanelWidth, Game1.uiViewport.Width - 64);
            height = Math.Min(PanelHeight, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int listX = xPositionOnScreen + PanelPadding;
            int listY = yPositionOnScreen + TitleBarHeight;
            int listWidth = width - PanelPadding * 2 - 56;     // leave room for scroll arrows
            int listHeight = height - TitleBarHeight - PanelPadding;
            _rowsPerPage = Math.Max(1, listHeight / (RowHeight + RowSpacing));

            _rowSlots.Clear();
            for (int i = 0; i < _rowsPerPage; i++)
            {
                int rowY = listY + i * (RowHeight + RowSpacing);
                var slot = new ClickableComponent(new Rectangle(listX, rowY, listWidth, RowHeight),
                    "row-" + i)
                {
                    myID = RowIdBase + i,
                    upNeighborID = i == 0 ? ScrollUpId : RowIdBase + i - 1,
                    downNeighborID = i == _rowsPerPage - 1 ? ScrollDownId : RowIdBase + i + 1,
                    rightNeighborID = ScrollUpId
                };
                _rowSlots.Add(slot);
            }

            int arrowX = listX + listWidth + 8;
            _scrollUp = new ClickableTextureComponent("scroll-up",
                new Rectangle(arrowX, listY, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f)
            {
                myID = ScrollUpId,
                downNeighborID = ScrollDownId,
                leftNeighborID = RowIdBase
            };
            _scrollDown = new ClickableTextureComponent("scroll-down",
                new Rectangle(arrowX, listY + listHeight - 48, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f)
            {
                myID = ScrollDownId,
                upNeighborID = ScrollUpId,
                leftNeighborID = RowIdBase + _rowsPerPage - 1
            };

            this.initializeUpperRightCloseButton();

            // populateClickableComponentList defaults to public-only via reflection — fill manually.
            allClickableComponents = new List<ClickableComponent>();
            allClickableComponents.AddRange(_rowSlots);
            allClickableComponents.Add(_scrollUp);
            allClickableComponents.Add(_scrollDown);
            if (upperRightCloseButton != null)
                allClickableComponents.Add(upperRightCloseButton);

            ClampScroll();
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _rowSlots.Count > 0 ? _rowSlots[0] : null;
            if (currentlySnappedComponent != null)
                this.snapCursorToCurrentSnappedComponent();
        }

        // ---------- input ----------

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hoverText = "";

            int visibleCount = Math.Min(_rowsPerPage, Math.Max(0, _entries.Count - _scrollIndex));
            for (int i = 0; i < visibleCount; i++)
            {
                if (!_rowSlots[i].containsPoint(x, y)) continue;
                BundleEntry e = _entries[_scrollIndex + i];

                // Hover over an ingredient icon → show its name + a "missing" tag.
                int iconRowX = _rowSlots[i].bounds.X + 16;
                int iconRowY = _rowSlots[i].bounds.Y + IngredientIconY;
                for (int k = 0; k < e.MissingItems.Count; k++)
                {
                    var iconRect = new Rectangle(
                        iconRowX + k * (IngredientIconSize + IngredientIconGap),
                        iconRowY, IngredientIconSize, IngredientIconSize);
                    if (iconRect.Contains(x, y))
                    {
                        Item probe = ResolveItem(e.MissingItems[k]);
                        _hoverText = probe?.DisplayName ?? e.MissingItems[k];
                        return;
                    }
                }
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);
            if (_scrollUp.containsPoint(x, y)) { Scroll(-1); return; }
            if (_scrollDown.containsPoint(x, y)) { Scroll(+1); return; }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            Scroll(direction > 0 ? -1 : +1);
        }

        public override void receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons b)
        {
            if (b == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                int id = currentlySnappedComponent.myID;
                if (id == ScrollUpId) { Scroll(-1); return; }
                if (id == ScrollDownId) { Scroll(+1); return; }
            }
            base.receiveGamePadButton(b);
        }

        private void Scroll(int delta)
        {
            int before = _scrollIndex;
            _scrollIndex += delta;
            ClampScroll();
            if (_scrollIndex != before)
                Game1.playSound("shwip");
        }

        private void ClampScroll()
        {
            int maxStart = Math.Max(0, _entries.Count - _rowsPerPage);
            if (_scrollIndex < 0) _scrollIndex = 0;
            if (_scrollIndex > maxStart) _scrollIndex = maxStart;
        }

        // ---------- drawing ----------

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            // Title bar.
            string title = $"Season Goals — {_season} (day {_run.DayOfMonth})";
            SpriteText.drawStringHorizontallyCenteredAt(b, title,
                xPositionOnScreen + width / 2, yPositionOnScreen + 24);

            // Visible rows.
            int visibleCount = Math.Min(_rowsPerPage, Math.Max(0, _entries.Count - _scrollIndex));
            for (int i = 0; i < visibleCount; i++)
                DrawRow(b, _rowSlots[i], _entries[_scrollIndex + i]);

            // Scroll arrows.
            _scrollUp.draw(b, _scrollIndex > 0 ? Color.White : Color.Gray, 1f);
            _scrollDown.draw(b, _scrollIndex + _rowsPerPage < _entries.Count ? Color.White : Color.Gray, 1f);

            base.draw(b);
            if (!string.IsNullOrEmpty(_hoverText))
                IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);
            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawRow(SpriteBatch b, ClickableComponent slot, BundleEntry e)
        {
            bool satisfiedThisSeason = e.MissingThisSeasonCount == 0;
            Color tint = satisfiedThisSeason ? Color.LightGreen * 0.7f : Color.White;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, tint, 1f, false);

            // Top line: "BundleName  (Theme)   N/X"
            string headline = $"{e.Bundle.Name}  ({e.Bundle.Theme})   {e.Have}/{e.Need}";
            Color headColor = satisfiedThisSeason ? Color.DarkGreen : Game1.textColor;
            Utility.drawTextWithShadow(b, headline, Game1.smallFont,
                new Vector2(slot.bounds.X + 16, slot.bounds.Y + 12), headColor);

            // Badge on the right side of the top line.
            string badge = BadgeFor(e);
            {
                Vector2 badgeSize = Game1.smallFont.MeasureString(badge);
                Color badgeColor = satisfiedThisSeason ? Color.DarkGreen : new Color(160, 34, 34);
                Utility.drawTextWithShadow(b, badge, Game1.smallFont,
                    new Vector2(slot.bounds.X + slot.bounds.Width - 16 - badgeSize.X, slot.bounds.Y + 12),
                    badgeColor);
            }

            // Bottom line: missing-item icons (or a "done" marker).
            if (satisfiedThisSeason) return;

            int iconRowX = slot.bounds.X + 16;
            int iconRowY = slot.bounds.Y + IngredientIconY;
            int maxIcons = Math.Max(1,
                (slot.bounds.Width - 32) / (IngredientIconSize + IngredientIconGap));
            int drawCount = Math.Min(e.MissingItems.Count, maxIcons);
            for (int k = 0; k < drawCount; k++)
            {
                Item probe = ResolveItem(e.MissingItems[k]);
                var pos = new Vector2(
                    iconRowX + k * (IngredientIconSize + IngredientIconGap),
                    iconRowY);
                if (probe != null)
                {
                    probe.drawInMenu(b, pos, IngredientIconSize / 64f, 1f, 0.86f,
                        StackDrawType.Hide, Color.White, false);
                }
                else
                {
                    var qSrc = new Rectangle(403, 496, 5, 7);
                    const int qScale = 3;
                    int qW = qSrc.Width * qScale;
                    int qH = qSrc.Height * qScale;
                    b.Draw(Game1.mouseCursors,
                        new Rectangle((int)pos.X + (IngredientIconSize - qW) / 2,
                                      (int)pos.Y + (IngredientIconSize - qH) / 2, qW, qH),
                        qSrc, Color.White);
                }
            }
            // "+N more" overflow indicator if the row can't hold all missing icons.
            if (e.MissingItems.Count > drawCount)
            {
                string more = $"+{e.MissingItems.Count - drawCount} more";
                Utility.drawTextWithShadow(b, more, Game1.smallFont,
                    new Vector2(iconRowX + drawCount * (IngredientIconSize + IngredientIconGap),
                                iconRowY + 8),
                    Game1.textColor);
            }
        }

        /// <summary>Right-aligned status badge: "checkpoint met" once the season's quota is
        /// satisfied; "needs N before {NextSeason} 1" otherwise; Winter swaps to "by run end"
        /// since Winter day-28 IS the run-end checkpoint (no next-season day 1 to point at).</summary>
        private string BadgeFor(BundleEntry e)
        {
            if (e.MissingThisSeasonCount == 0) return "checkpoint met";
            if (_season == CoreSeason.Winter) return $"needs {e.MissingThisSeasonCount} by run end";
            return $"needs {e.MissingThisSeasonCount} before {(CoreSeason)((int)_season + 1)} 1";
        }

        private static Item ResolveItem(string id)
        {
            try { return ItemRegistry.Create(id, 1, 0, allowNull: true); }
            catch (Exception) { return null; }
        }

        // ---------- types ----------

        private sealed class BundleEntry
        {
            public BundleRequirement Bundle { get; }
            public int Have { get; }
            public int Need { get; }
            public int MissingThisSeasonCount { get; }
            public IReadOnlyList<string> MissingItems { get; }

            public BundleEntry(BundleRequirement bundle, int have, int need, int missingThisSeason,
                IReadOnlyList<string> missingItems)
            {
                Bundle = bundle;
                Have = have;
                Need = need;
                MissingThisSeasonCount = missingThisSeason;
                MissingItems = missingItems;
            }
        }
    }
}
