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
using TheLongestYear.Donations;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>
    /// The Junimo Shrine upgrade shop: spend Junimo Points (mod currency in MetaState) on permanent
    /// upgrades. Built fresh as an IClickableMenu — vanilla ShopMenu's static currency switch is
    /// hardcoded to gold/festival/club/QiGems (ShopMenu.cs:802-812), so reusing it would require
    /// monkey-patching a 2400-line file. A custom menu is the "fix the data" path.
    /// </summary>
    internal sealed class JunimoShrineMenu : IClickableMenu
    {
        private const int PanelWidth = 1100;
        private const int PanelHeight = 700;
        private const int TabWidth = 200;
        private const int TabHeight = 56;
        private const int TabSpacing = 8;
        private const int RowHeight = 96;
        private const int RowSpacing = 8;
        private const int PanelPadding = 32;

        private const int TabIdBase = 6100;          // 6100..6105 for 6 tabs
        private const int RowIdBase = 7000;          // one id per visible row slot
        private const int ScrollUpId = 7900;
        private const int ScrollDownId = 7901;

        // Cached at class load time so the gamepad-button handler doesn't have to recount on every press.
        private static readonly int CategoryCount = Enum.GetValues(typeof(UpgradeCategory)).Length;

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly UpgradePurchaseService _purchases;

        private UpgradeCategory _activeCategory = UpgradeCategory.Loadout;
        private int _scrollIndex;
        private int _rowsPerPage;

        private readonly List<ClickableTextureComponent> _tabs = new List<ClickableTextureComponent>();
        private readonly List<ClickableComponent> _rowSlots = new List<ClickableComponent>();
        private ClickableTextureComponent _scrollUp;
        private ClickableTextureComponent _scrollDown;

        private string _hoverText = "";

        // Green used for the "you already own this" confirmation rows (and their "Owned" label).
        private static readonly Color OwnedGreen = new Color(30, 120, 30);

        /// <summary>One drawn row: a catalog entry plus whether it's an owned-confirmation row
        /// (green, no cost, not clickable) vs. a buyable row.</summary>
        private readonly struct ShrineRow
        {
            public readonly UpgradeDefinition Def;
            public readonly bool Owned;
            public ShrineRow(UpgradeDefinition def, bool owned) { Def = def; Owned = owned; }
        }

        public JunimoShrineMenu(IMonitor monitor, MetaStore store, UpgradePurchaseService purchases)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _monitor = monitor;
            _store = store;
            _purchases = purchases;

            RecomputeBoundsAndLayout();

            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                this.snapToDefaultClickableComponent();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeBoundsAndLayout();
        }

        private void RecomputeBoundsAndLayout()
        {
            width = Math.Min(PanelWidth, Game1.uiViewport.Width - 64);
            height = Math.Min(PanelHeight, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int listX = xPositionOnScreen + PanelPadding + TabWidth + 16;
            int listY = yPositionOnScreen + 80; // leave room for title bar
            int listWidth = width - (listX - xPositionOnScreen) - PanelPadding;
            int listHeight = height - 80 - PanelPadding;
            _rowsPerPage = Math.Max(1, listHeight / (RowHeight + RowSpacing));

            // Tabs (vertical strip on the left)
            _tabs.Clear();
            UpgradeCategory[] categories = (UpgradeCategory[])Enum.GetValues(typeof(UpgradeCategory));
            for (int i = 0; i < categories.Length; i++)
            {
                int tabX = xPositionOnScreen + PanelPadding;
                int tabY = yPositionOnScreen + 80 + (i * (TabHeight + TabSpacing));
                var tab = new ClickableTextureComponent(
                    name: categories[i].ToString(),
                    bounds: new Rectangle(tabX, tabY, TabWidth, TabHeight),
                    label: null,
                    hoverText: categories[i].ToString(),
                    texture: Game1.mouseCursors,
                    sourceRect: new Rectangle(16, 368, 16, 16), // simple panel slice — re-skin later
                    scale: 1f)
                {
                    myID = TabIdBase + i,
                    upNeighborID = i == 0 ? -1 : TabIdBase + i - 1,
                    downNeighborID = i == categories.Length - 1 ? -1 : TabIdBase + i + 1,
                    rightNeighborID = RowIdBase
                };
                _tabs.Add(tab);
            }

            // Row slots (visible window into ByCategory).
            _rowSlots.Clear();
            for (int i = 0; i < _rowsPerPage; i++)
            {
                int rowY = listY + (i * (RowHeight + RowSpacing));
                var slot = new ClickableComponent(new Rectangle(listX, rowY, listWidth - 56, RowHeight),
                    "row-" + i)
                {
                    myID = RowIdBase + i,
                    upNeighborID = i == 0 ? ScrollUpId : RowIdBase + i - 1,
                    downNeighborID = i == _rowsPerPage - 1 ? ScrollDownId : RowIdBase + i + 1,
                    leftNeighborID = TabIdBase
                };
                _rowSlots.Add(slot);
            }

            // Scroll arrows on the right
            int arrowX = listX + listWidth - 48;
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

            // Base populateClickableComponentList uses GetType().GetFields() which defaults to PUBLIC only,
            // so our private fields would be skipped. Build the list ourselves.
            allClickableComponents = new List<ClickableComponent>();
            allClickableComponents.AddRange(_tabs);
            allClickableComponents.AddRange(_rowSlots);
            allClickableComponents.Add(_scrollUp);
            allClickableComponents.Add(_scrollDown);
            if (upperRightCloseButton != null)
                allClickableComponents.Add(upperRightCloseButton);

            ClampScroll();
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _tabs.Count > 0 ? (ClickableComponent)_tabs[(int)_activeCategory] : null;
            if (currentlySnappedComponent != null)
                this.snapCursorToCurrentSnappedComponent();
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hoverText = "";

            IReadOnlyList<ShrineRow> rows = VisibleRows(out _, out _);
            for (int i = 0; i < rows.Count; i++)
            {
                if (_rowSlots[i].containsPoint(x, y))
                {
                    UpgradeDefinition def = rows[i].Def;
                    string footer = rows[i].Owned ? "Owned" : $"Cost: {def.Cost} JP";
                    _hoverText = $"{def.DisplayName}\n{def.Description}\n{footer}";
                    return;
                }
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            for (int i = 0; i < _tabs.Count; i++)
                if (_tabs[i].containsPoint(x, y)) { SetActiveCategory((UpgradeCategory)i); return; }

            if (_scrollUp.containsPoint(x, y)) { Scroll(-1); return; }
            if (_scrollDown.containsPoint(x, y)) { Scroll(+1); return; }

            IReadOnlyList<ShrineRow> rows = VisibleRows(out _, out _);
            for (int i = 0; i < rows.Count; i++)
                if (_rowSlots[i].containsPoint(x, y))
                {
                    if (!rows[i].Owned) TryBuy(rows[i].Def);    // owned rows are display-only
                    return;
                }
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
                if (id >= TabIdBase && id < TabIdBase + CategoryCount) { SetActiveCategory((UpgradeCategory)(id - TabIdBase)); return; }
                if (id == ScrollUpId) { Scroll(-1); return; }
                if (id == ScrollDownId) { Scroll(+1); return; }
                if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
                {
                    IReadOnlyList<ShrineRow> rows = VisibleRows(out _, out _);
                    int slot = id - RowIdBase;
                    if (slot < rows.Count && !rows[slot].Owned) TryBuy(rows[slot].Def);
                    return;
                }
            }
            base.receiveGamePadButton(b);
        }

        private void TryBuy(UpgradeDefinition def)
        {
            _purchases.TryPurchase(def.Id);
            // The list redraws next frame from the updated MetaState — no extra refresh needed.
        }

        private void SetActiveCategory(UpgradeCategory category)
        {
            _activeCategory = category;
            _scrollIndex = 0;
            Game1.playSound("smallSelect");
        }

        private void Scroll(int delta)
        {
            int before = _scrollIndex;
            _scrollIndex += delta;
            ClampScroll();
            if (_scrollIndex != before)
                Game1.playSound("shwip");
        }

        /// <summary>
        /// All catalog entries in the active category that should be VISIBLE in the shrine
        /// shop: not owned, prereq satisfied (or no prereq), meta-requirement satisfied
        /// (or no meta-requirement). Chain-locked rows disappear entirely — the player only
        /// sees the next purchasable tier in each chain.
        /// </summary>
        /// <summary>Rows for the active category: buyable keeps first (catalog order), then the
        /// owned-confirmation "leaf" rows (green, no cost) so the player can see what they already
        /// have and read its effect to test it.</summary>
        private IReadOnlyList<ShrineRow> VisibleCatalogForActiveCategory()
        {
            var rows = new List<ShrineRow>();
            foreach (UpgradeDefinition def in
                KeepShopFilter.BuyableInCategory(_activeCategory, _store.State, RunReachEvaluator.Meets))
                rows.Add(new ShrineRow(def, owned: false));
            foreach (UpgradeDefinition def in
                KeepShopFilter.OwnedLeavesInCategory(_activeCategory, _store.State, RunReachEvaluator.Meets))
                rows.Add(new ShrineRow(def, owned: true));
            return rows;
        }

        private void ClampScroll()
        {
            int total = VisibleCatalogForActiveCategory().Count;
            int maxStart = Math.Max(0, total - _rowsPerPage);
            if (_scrollIndex < 0) _scrollIndex = 0;
            if (_scrollIndex > maxStart) _scrollIndex = maxStart;
        }

        private IReadOnlyList<ShrineRow> VisibleRows(out int total, out int startIndex)
        {
            IReadOnlyList<ShrineRow> all = VisibleCatalogForActiveCategory();
            total = all.Count;
            startIndex = _scrollIndex;
            int count = Math.Min(_rowsPerPage, total - startIndex);
            return all.Skip(startIndex).Take(count).ToList();
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);

            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            SpriteText.drawStringHorizontallyCenteredAt(b, "Junimo Shrine",
                xPositionOnScreen + width / 2, yPositionOnScreen + 24);

            string jp = $"JP: {_store.State.JunimoPoints}";
            Vector2 jpSize = Game1.dialogueFont.MeasureString(jp);
            Utility.drawTextWithShadow(b, jp, Game1.dialogueFont,
                new Vector2(xPositionOnScreen + width - PanelPadding - jpSize.X, yPositionOnScreen + 24),
                Game1.textColor);

            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                Color tint = ((UpgradeCategory)i == _activeCategory)
                    ? Color.White
                    : Color.White * 0.7f;
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    tab.bounds.X, tab.bounds.Y, tab.bounds.Width, tab.bounds.Height, tint, 1f, false);

                string label = ((UpgradeCategory)i).ToString();
                // Scale the label down if it would overflow the tab (e.g. "Obtainability"),
                // so long category names always fit inside the frame.
                Vector2 labelSize = Game1.smallFont.MeasureString(label);
                float available = tab.bounds.Width - 24;
                float labelScale = labelSize.X > available ? available / labelSize.X : 1f;
                Utility.drawTextWithShadow(b, label, Game1.smallFont,
                    new Vector2(tab.bounds.X + 12, tab.bounds.Y + (tab.bounds.Height - labelSize.Y * labelScale) / 2),
                    Game1.textColor, labelScale);
            }

            IReadOnlyList<ShrineRow> rows = VisibleRows(out int total, out int startIndex);
            for (int i = 0; i < rows.Count; i++)
                DrawRow(b, _rowSlots[i], rows[i]);

            _scrollUp.draw(b, _scrollIndex > 0 ? Color.White : Color.Gray, 1f);
            _scrollDown.draw(b, (startIndex + _rowsPerPage) < total ? Color.White : Color.Gray, 1f);

            base.draw(b);
            if (!string.IsNullOrEmpty(_hoverText))
                IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);
            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawRow(SpriteBatch b, ClickableComponent slot, ShrineRow row)
        {
            UpgradeDefinition def = row.Def;

            if (row.Owned)
            {
                // Owned confirmation row: green, no cost, display-only.
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, Color.White, 1f, false);
                Utility.drawTextWithShadow(b, def.DisplayName, Game1.dialogueFont,
                    new Vector2(slot.bounds.X + 16, slot.bounds.Y + 12), OwnedGreen);
                Utility.drawTextWithShadow(b, "Owned", Game1.smallFont,
                    new Vector2(slot.bounds.X + 16, slot.bounds.Y + 56), OwnedGreen);
                return;
            }

            bool affordable = _store.State.JunimoPoints >= def.Cost;

            Color tint = affordable ? Color.White : Color.White * 0.55f;

            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, tint, 1f, false);

            Utility.drawTextWithShadow(b, def.DisplayName, Game1.dialogueFont,
                new Vector2(slot.bounds.X + 16, slot.bounds.Y + 12), Game1.textColor);

            string statusLine = $"Cost: {def.Cost} JP" + (!affordable ? "  (insufficient)" : "");
            Utility.drawTextWithShadow(b, statusLine, Game1.smallFont,
                new Vector2(slot.bounds.X + 16, slot.bounds.Y + 56),
                affordable ? Game1.textColor : Color.Red);
        }
    }
}
