using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Slot-grid menu for managing banked crafting recipes in <see cref="MetaState.CraftbookRecipes"/>.
    /// Opened when the player interacts with the Craftbook world object.
    ///
    /// Slot count = <see cref="UpgradeCatalog.CraftbookSlotCount"/> of the highest owned Craftbook tier.
    /// Empty slot click → inline recipe picker (currently-known, unslotted recipes only).
    /// Filled slot click → confirm-removal dialog.
    /// On dismiss, <c>MetaState.DismissedIndicators</c> gets "tly.craftbook" so the one-time
    /// craftbook intro quest doesn't re-fire on subsequent loop resets.
    /// </summary>
    internal sealed class CraftbookMenu : IClickableMenu
    {
        private const int PanelWidth  = 900;
        private const int PanelHeight = 640;
        private const int PanelPad    = 32;
        private const int RowHeight   = 72;
        private const int RowSpacing  = 8;
        private const int RowIdBase   = 8200;
        private const int ScrollUpId  = 8950;
        private const int ScrollDownId = 8951;

        private readonly IMonitor _monitor;
        private readonly MetaState _meta;
        private readonly int _slotCount;

        // Sub-mode: when non-null we are in "pick a recipe to fill slot _pendingSlot".
        private int _pendingSlot = -1;
        private List<string> _pickerList;   // recipe ids available to pick; null = normal mode
        private int _pickerScroll;

        private int _scroll;
        private int _rowsPerPage;
        private readonly List<ClickableComponent> _rowSlots = new List<ClickableComponent>();
        private ClickableTextureComponent _scrollUp;
        private ClickableTextureComponent _scrollDown;

        public CraftbookMenu(IMonitor monitor, MetaState meta)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _monitor = monitor;
            _meta    = meta;
            int tier = meta.HighestKeptTier("craftbook_", maxTier: 3);
            _slotCount = UpgradeCatalog.CraftbookSlotCount(tier);
            RecomputeLayout();
            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                this.snapToDefaultClickableComponent();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeLayout();
        }

        private void RecomputeLayout()
        {
            width  = Math.Min(PanelWidth,  Game1.uiViewport.Width  - 64);
            height = Math.Min(PanelHeight, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width  - width)  / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int listX = xPositionOnScreen + PanelPad;
            int listY = yPositionOnScreen + 80;
            int listW = width - PanelPad * 2 - 52;   // leave room for scroll arrows
            int listH = height - 80 - PanelPad;
            _rowsPerPage = Math.Max(1, listH / (RowHeight + RowSpacing));

            _rowSlots.Clear();
            for (int i = 0; i < _rowsPerPage; i++)
            {
                int rowY = listY + i * (RowHeight + RowSpacing);
                _rowSlots.Add(new ClickableComponent(
                    new Rectangle(listX, rowY, listW, RowHeight), "row-" + i)
                {
                    myID = RowIdBase + i,
                    upNeighborID   = i == 0 ? ScrollUpId : RowIdBase + i - 1,
                    downNeighborID = i == _rowsPerPage - 1 ? ScrollDownId : RowIdBase + i + 1,
                });
            }

            int arrowX = listX + listW + 4;
            _scrollUp = new ClickableTextureComponent("scroll-up",
                new Rectangle(arrowX, listY, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f)
            { myID = ScrollUpId, downNeighborID = ScrollDownId, leftNeighborID = RowIdBase };

            _scrollDown = new ClickableTextureComponent("scroll-down",
                new Rectangle(arrowX, listY + listH - 48, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f)
            { myID = ScrollDownId, upNeighborID = ScrollUpId, leftNeighborID = RowIdBase + _rowsPerPage - 1 };

            this.initializeUpperRightCloseButton();
            allClickableComponents = new List<ClickableComponent>(_rowSlots) { _scrollUp, _scrollDown };
            if (upperRightCloseButton != null) allClickableComponents.Add(upperRightCloseButton);
            ClampScroll();
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _rowSlots.Count > 0 ? _rowSlots[0] : null;
            if (currentlySnappedComponent != null) this.snapCursorToCurrentSnappedComponent();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_scrollUp.containsPoint(x, y))   { Scroll(-1); return; }
            if (_scrollDown.containsPoint(x, y))  { Scroll(+1); return; }

            if (_pickerList != null)
            {
                // Picker sub-mode: row click selects a recipe.
                for (int i = 0; i < _rowSlots.Count; i++)
                {
                    if (!_rowSlots[i].containsPoint(x, y)) continue;
                    int pickerIndex = _pickerScroll + i;
                    if (pickerIndex >= _pickerList.Count) break;
                    BankRecipe(_pickerList[pickerIndex]);
                    return;
                }
                // Click outside rows → cancel picker
                _pickerList = null;
                _pickerScroll = 0;
                return;
            }

            // Normal slot mode.
            for (int i = 0; i < _rowSlots.Count; i++)
            {
                if (!_rowSlots[i].containsPoint(x, y)) continue;
                int slotIndex = _scroll + i;
                if (slotIndex >= _slotCount) break;

                if (slotIndex < _meta.CraftbookRecipes.Count)
                    PromptRemove(slotIndex);
                else
                    OpenPicker(slotIndex);
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
                if (id == ScrollUpId)   { Scroll(-1); return; }
                if (id == ScrollDownId) { Scroll(+1); return; }
                if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
                    receiveLeftClick(_rowSlots[id - RowIdBase].bounds.Center.X,
                                     _rowSlots[id - RowIdBase].bounds.Center.Y);
                return;
            }
            base.receiveGamePadButton(b);
        }

        private void OpenPicker(int slotIndex)
        {
            _pendingSlot = slotIndex;
            _pickerList  = AvailableRecipesToBank();
            _pickerScroll = 0;
            if (_pickerList.Count == 0)
            {
                Game1.addHUDMessage(new HUDMessage("No new recipes to add — learn more recipes first.", HUDMessage.newQuest_type));
                _pickerList = null;
                _pendingSlot = -1;
            }
        }

        private void BankRecipe(string recipeId)
        {
            if (_meta.CraftbookRecipes.Contains(recipeId)) return;  // guard
            _meta.CraftbookRecipes.Add(recipeId);
            Game1.playSound("smallSelect");
            _monitor.Log($"CraftbookMenu: banked recipe '{recipeId}'.", LogLevel.Trace);
            _pickerList  = null;
            _pendingSlot = -1;
        }

        private void PromptRemove(int slotIndex)
        {
            string recipeId = _meta.CraftbookRecipes[slotIndex];
            string name = RecipeDisplayName(recipeId, isCooking: false);
            Game1.activeClickableMenu = new ConfirmationDialog(
                $"Remove \"{name}\" from the craftbook?\nThis recipe won't carry over next run unless re-banked.",
                _ =>
                {
                    _meta.CraftbookRecipes.RemoveAt(slotIndex);
                    Game1.playSound("trashcan");
                    _monitor.Log($"CraftbookMenu: removed recipe '{recipeId}' from slot {slotIndex}.", LogLevel.Trace);
                    Game1.activeClickableMenu = this;
                },
                _ => Game1.activeClickableMenu = this);
        }

        private List<string> AvailableRecipesToBank()
        {
            var already = new HashSet<string>(_meta.CraftbookRecipes);
            return Game1.player.craftingRecipes.Keys
                .Where(id => !already.Contains(id))
                .OrderBy(id => id)
                .ToList();
        }

        private void Scroll(int delta)
        {
            int before = _scroll;
            if (_pickerList != null)
                _pickerScroll = Math.Max(0, Math.Min(_pickerList.Count - _rowsPerPage, _pickerScroll + delta));
            else
                _scroll += delta;
            ClampScroll();
            if (_scroll != before) Game1.playSound("shwip");
        }

        private void ClampScroll()
        {
            if (_pickerList != null)
            {
                _pickerScroll = Math.Max(0, Math.Min(Math.Max(0, _pickerList.Count - _rowsPerPage), _pickerScroll));
                return;
            }
            int maxStart = Math.Max(0, _slotCount - _rowsPerPage);
            _scroll = Math.Max(0, Math.Min(maxStart, _scroll));
        }

        public override void emergencyShutDown()
        {
            base.emergencyShutDown();
            _meta.DismissedIndicators.Add("tly.craftbook");
        }

        protected override void cleanupBeforeExit()
        {
            base.cleanupBeforeExit();
            _meta.DismissedIndicators.Add("tly.craftbook");
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);

            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            string title = _pickerList != null
                ? "Choose a recipe to bank:"
                : $"Craftbook — {_meta.CraftbookRecipes.Count} / {_slotCount} slots";
            StardewValley.BellsAndWhistles.SpriteText.drawStringHorizontallyCenteredAt(
                b, title, xPositionOnScreen + width / 2, yPositionOnScreen + 24);

            if (_pickerList != null)
                DrawPickerRows(b);
            else
                DrawSlotRows(b);

            _scrollUp.draw(b, _scroll > 0 || (_pickerList != null && _pickerScroll > 0) ? Color.White : Color.Gray, 1f);

            int totalRows = _pickerList != null ? _pickerList.Count : _slotCount;
            int scrollStart = _pickerList != null ? _pickerScroll : _scroll;
            _scrollDown.draw(b, (scrollStart + _rowsPerPage) < totalRows ? Color.White : Color.Gray, 1f);

            base.draw(b);
            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawSlotRows(SpriteBatch b)
        {
            for (int i = 0; i < _rowSlots.Count; i++)
            {
                int slotIndex = _scroll + i;
                if (slotIndex >= _slotCount) break;

                ClickableComponent slot = _rowSlots[i];
                bool filled = slotIndex < _meta.CraftbookRecipes.Count;
                string label = filled
                    ? RecipeDisplayName(_meta.CraftbookRecipes[slotIndex], isCooking: false)
                    : "[empty — click to add]";
                Color tint = filled ? Color.White : Color.White * 0.6f;

                IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, tint, 1f, false);

                Utility.drawTextWithShadow(b, label, Game1.dialogueFont,
                    new Vector2(slot.bounds.X + 16, slot.bounds.Y + (slot.bounds.Height - (int)Game1.dialogueFont.MeasureString(label).Y) / 2),
                    Game1.textColor);
            }
        }

        private void DrawPickerRows(SpriteBatch b)
        {
            for (int i = 0; i < _rowSlots.Count; i++)
            {
                int pickerIndex = _pickerScroll + i;
                if (pickerIndex >= _pickerList.Count) break;

                ClickableComponent slot = _rowSlots[i];
                string label = RecipeDisplayName(_pickerList[pickerIndex], isCooking: false);

                IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, Color.White, 1f, false);

                Utility.drawTextWithShadow(b, label, Game1.dialogueFont,
                    new Vector2(slot.bounds.X + 16, slot.bounds.Y + (slot.bounds.Height - (int)Game1.dialogueFont.MeasureString(label).Y) / 2),
                    Game1.textColor);
            }
        }

        private static string RecipeDisplayName(string recipeId, bool isCooking)
        {
            try
            {
                // CraftingRecipe.name is the localised display name.
                return new CraftingRecipe(recipeId, isCooking).name;
            }
            catch
            {
                // Unknown recipe id (content mod removed) — fall back to raw id.
                return recipeId;
            }
        }
    }
}
