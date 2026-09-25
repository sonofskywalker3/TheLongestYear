using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear.UI
{
    /// <summary>
    /// The Herd Book (spec 2026-09-25), laid out like <see cref="CookbookMenu"/>. One row per owned
    /// slot in ladder order, labeled with its kind. An empty row opens a picker of the farm animals
    /// that slot accepts and that are not registered yet; a filled row asks to remove the animal.
    /// Animal rows (picker and filled slots) are drawn like the game's Animals tab by
    /// <see cref="AnimalRowRenderer"/>. A row whose building keep is not owned says which keep it needs.
    /// Opened from the placed book, <c>tly_openherdbook</c>, and the rewind night
    /// (RunController.OfferHerdBook, with a subtitle).
    /// </summary>
    internal sealed class HerdBookMenu : IClickableMenu
    {
        private const int PanelWidth = 900;
        private const int PanelHeight = 720;
        private const int PanelPad = 32;
        private const int RowHeight = 116;   // the Animals tab's 112px slot plus 2px above and below the sprite
        private const int RowSpacing = 4;
        private const int RowTextInset = 16;
        private const int RowIdBase = 8300;
        private const int ScrollUpId = 8950;
        private const int ScrollDownId = 8951;
        private const int HeaderPlain = 80;
        private const int HeaderWithSubtitle = 120;
        private const int TitleY = 24;
        private const int SubtitleY = 84;
        private const float EmptyRowAlpha = 0.6f;
        private const float NoteAlpha = 0.8f;

        private readonly IMonitor _monitor;
        private readonly MetaState _meta;
        private readonly IReadOnlyList<HerdSlotKind> _slots;

        /// <summary>Line under the title on the rewind night; null when opened from the book.</summary>
        private readonly string _subtitle;

        private int _pendingSlot = -1;
        private List<FarmAnimal> _pickerList;   // null = slot mode
        private List<AnimalRow> _pickerRows;
        private readonly Dictionary<HerdEntry, AnimalRow> _slotRows = new Dictionary<HerdEntry, AnimalRow>();
        private int _pickerScroll;
        private int _scroll;
        private int _rowsPerPage;
        private readonly List<ClickableComponent> _rowSlots = new List<ClickableComponent>();
        private ClickableTextureComponent _scrollUp;
        private ClickableTextureComponent _scrollDown;

        private int HeaderHeight => _subtitle == null ? HeaderPlain : HeaderWithSubtitle;

        public HerdBookMenu(IMonitor monitor, MetaState meta, string subtitle = null)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _monitor = monitor;
            _meta = meta;
            _subtitle = subtitle;
            _slots = HerdBookRules.SlotsFor(meta);
            _monitor.Log(
                $"HerdBookMenu: slots={_slots.Count}, registered={HerdBookRules.Used(meta.HerdBook, _slots.Count)}.",
                LogLevel.Info);
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
            width = Math.Min(PanelWidth, Game1.uiViewport.Width - 64);
            height = Math.Min(PanelHeight, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int listX = xPositionOnScreen + PanelPad;
            int listY = yPositionOnScreen + HeaderHeight;
            int listW = width - PanelPad * 2 - 52;
            int listH = height - HeaderHeight - PanelPad;
            _rowsPerPage = Math.Max(1, listH / (RowHeight + RowSpacing));

            _rowSlots.Clear();
            for (int i = 0; i < _rowsPerPage; i++)
            {
                int rowY = listY + i * (RowHeight + RowSpacing);
                _rowSlots.Add(new ClickableComponent(new Rectangle(listX, rowY, listW, RowHeight), "row-" + i)
                {
                    myID = RowIdBase + i,
                    upNeighborID = i == 0 ? ScrollUpId : RowIdBase + i - 1,
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
            if (_scrollUp.containsPoint(x, y)) { Scroll(-1); return; }
            if (_scrollDown.containsPoint(x, y)) { Scroll(+1); return; }

            if (_pickerList != null)
            {
                for (int i = 0; i < _rowSlots.Count; i++)
                {
                    if (!_rowSlots[i].containsPoint(x, y)) continue;
                    int pickerIndex = _pickerScroll + i;
                    if (pickerIndex >= _pickerList.Count) break;
                    RegisterAnimal(_pickerList[pickerIndex]);
                    return;
                }
                ClosePicker();   // click outside the rows cancels
                return;
            }

            for (int i = 0; i < _rowSlots.Count; i++)
            {
                if (!_rowSlots[i].containsPoint(x, y)) continue;
                int slotIndex = _scroll + i;
                if (slotIndex >= _slots.Count) break;
                if (HerdBookRules.EntryAt(_meta.HerdBook, slotIndex) != null)
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
                if (id == ScrollUpId) { Scroll(-1); return; }
                if (id == ScrollDownId) { Scroll(+1); return; }
                if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
                    receiveLeftClick(_rowSlots[id - RowIdBase].bounds.Center.X, _rowSlots[id - RowIdBase].bounds.Center.Y);
                return;
            }
            base.receiveGamePadButton(b);
        }

        private void OpenPicker(int slotIndex)
        {
            _pendingSlot = slotIndex;
            _pickerList = HerdBookService.Candidates(_slots[slotIndex], _meta);
            _pickerScroll = 0;
            _pickerRows = _pickerList.Select(a => AnimalRowRenderer.ForLive(a, _monitor)).ToList();
            if (_pickerList.Count == 0)
            {
                Game1.addHUDMessage(new HUDMessage(Strings.Get("menu.herdbook.no-animals"), HUDMessage.newQuest_type));
                ClosePicker();
            }
        }

        private void ClosePicker()
        {
            _pickerList = null;
            _pickerRows = null;
            _pendingSlot = -1;
            _pickerScroll = 0;
        }

        private void RegisterAnimal(FarmAnimal animal)
        {
            if (HerdBookService.Register(_meta, _pendingSlot, animal, _monitor) == HerdRegisterResult.Registered)
                Game1.playSound("smallSelect");
            ClosePicker();
        }

        private void PromptRemove(int slotIndex)
        {
            HerdEntry entry = HerdBookRules.EntryAt(_meta.HerdBook, slotIndex);
            Game1.activeClickableMenu = new ConfirmationDialog(
                Strings.Get("menu.herdbook.remove-confirm", new Dictionary<string, string> { ["name"] = entry.Name }),
                _ =>
                {
                    HerdBookService.Remove(_meta, slotIndex, _monitor);
                    Game1.playSound("trashcan");
                    Game1.activeClickableMenu = this;
                },
                _ => Game1.activeClickableMenu = this);
        }

        private void Scroll(int delta)
        {
            int before = _scroll;
            if (_pickerList != null)
                _pickerScroll += delta;
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
            _scroll = Math.Max(0, Math.Min(Math.Max(0, _slots.Count - _rowsPerPage), _scroll));
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.5f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            string title = _pickerList != null
                ? Strings.Get("menu.herdbook.choose")
                : Strings.Get("menu.herdbook.title", new Dictionary<string, string>
                    {
                        ["used"] = HerdBookRules.Used(_meta.HerdBook, _slots.Count).ToString(),
                        ["total"] = _slots.Count.ToString(),
                    });
            StardewValley.BellsAndWhistles.SpriteText.drawStringHorizontallyCenteredAt(
                b, title, xPositionOnScreen + width / 2, yPositionOnScreen + TitleY);
            if (_subtitle != null)
            {
                Vector2 size = Game1.smallFont.MeasureString(_subtitle);
                Utility.drawTextWithShadow(b, _subtitle, Game1.smallFont,
                    new Vector2(xPositionOnScreen + (width - size.X) / 2, yPositionOnScreen + SubtitleY), Game1.textColor);
            }

            if (_pickerList != null) DrawPickerRows(b); else DrawSlotRows(b);

            int totalRows = _pickerList != null ? _pickerList.Count : _slots.Count;
            int scrollStart = _pickerList != null ? _pickerScroll : _scroll;
            _scrollUp.draw(b, scrollStart > 0 ? Color.White : Color.Gray, 1f);
            _scrollDown.draw(b, scrollStart + _rowsPerPage < totalRows ? Color.White : Color.Gray, 1f);

            base.draw(b);
            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawSlotRows(SpriteBatch b)
        {
            for (int i = 0; i < _rowSlots.Count; i++)
            {
                int slotIndex = _scroll + i;
                if (slotIndex >= _slots.Count) break;
                Rectangle row = _rowSlots[i].bounds;
                HerdSlotKind kind = _slots[slotIndex];
                HerdEntry entry = HerdBookRules.EntryAt(_meta.HerdBook, slotIndex);
                string kindName = HerdSlotRules.DisplayName(kind);
                string note = null;
                if (!_meta.HasUpgrade(HerdSlotRules.RequiredKeepId(kind)))
                {
                    string keep = UpgradeCatalog.TryGet(HerdSlotRules.RequiredKeepId(kind))?.DisplayName ?? HerdSlotRules.RequiredHousing(kind);
                    note = Strings.Get("menu.herdbook.needs-keep", new Dictionary<string, string> { ["keep"] = keep });
                }

                AnimalRow animalRow = entry == null ? null : SlotRow(entry);
                if (animalRow?.Entry != null)
                {
                    DrawRowBox(b, row, Color.White);
                    AnimalRowRenderer.Draw(b, row, animalRow, kindName, note);
                    continue;
                }

                string label = entry == null
                    ? Strings.Get("menu.herdbook.empty-slot", new Dictionary<string, string> { ["kind"] = kindName })
                    : Strings.Get("menu.herdbook.filled-slot", new Dictionary<string, string>
                        {
                            ["kind"] = kindName,
                            ["name"] = entry.Name,
                            ["hearts"] = HerdBookRules.Hearts(entry.Friendship).ToString(),
                        });
                DrawRow(b, row, label, entry == null ? Color.White * EmptyRowAlpha : Color.White, note);
            }
        }

        /// <summary>The filled slot's row, built once per entry: from the live animal when it is on
        /// the farm, else from the stored entry.</summary>
        private AnimalRow SlotRow(HerdEntry entry)
        {
            if (_slotRows.TryGetValue(entry, out AnimalRow cached)) return cached;
            FarmAnimal live = HerdBookService.LiveAnimals().FirstOrDefault(a => a.myID.Value == entry.AnimalId);
            AnimalRow built = live != null ? AnimalRowRenderer.ForLive(live, _monitor) : AnimalRowRenderer.ForStored(entry, _monitor);
            _slotRows[entry] = built;
            return built;
        }

        private void DrawPickerRows(SpriteBatch b)
        {
            for (int i = 0; i < _rowSlots.Count; i++)
            {
                int pickerIndex = _pickerScroll + i;
                if (pickerIndex >= _pickerList.Count) break;
                if (_pickerRows[pickerIndex].Entry != null)
                {
                    DrawRowBox(b, _rowSlots[i].bounds, Color.White);
                    AnimalRowRenderer.Draw(b, _rowSlots[i].bounds, _pickerRows[pickerIndex], null, null);
                    continue;
                }
                FarmAnimal animal = _pickerList[pickerIndex];
                string label = Strings.Get("menu.herdbook.picker-row", new Dictionary<string, string>
                {
                    ["name"] = animal.Name,
                    ["species"] = animal.displayType,
                    ["hearts"] = HerdBookRules.Hearts(animal.friendshipTowardFarmer.Value).ToString(),
                });
                DrawRow(b, _rowSlots[i].bounds, label, Color.White);
            }
        }

        private static void DrawRowBox(SpriteBatch b, Rectangle row, Color tint)
            => IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                row.X, row.Y, row.Width, row.Height, tint, 1f, false);

        /// <summary>A text-only row: <paramref name="label"/> in the dialogue font, and the optional
        /// <paramref name="note"/> on its own line under it in the small font, the pair centred in
        /// the row. Each line shrinks to the row's width if it would run past it, so the two never
        /// share a line and nothing leaves the box.</summary>
        private static void DrawRow(SpriteBatch b, Rectangle row, string label, Color tint, string note = null)
        {
            DrawRowBox(b, row, tint);
            float maxW = row.Width - RowTextInset * 2;
            Vector2 labelSize = Game1.dialogueFont.MeasureString(label);
            float labelScale = FitScale(labelSize.X, maxW);
            float labelH = labelSize.Y * labelScale;
            Vector2 noteSize = note == null ? Vector2.Zero : Game1.smallFont.MeasureString(note);
            float noteScale = FitScale(noteSize.X, maxW);
            float noteH = noteSize.Y * noteScale;
            float top = row.Y + (int)((row.Height - labelH - noteH) / 2);
            Utility.drawTextWithShadow(b, label, Game1.dialogueFont,
                new Vector2(row.X + RowTextInset, top), Game1.textColor, labelScale);
            if (note != null)
                Utility.drawTextWithShadow(b, note, Game1.smallFont,
                    new Vector2(row.X + RowTextInset, top + labelH), Game1.textColor * NoteAlpha, noteScale);
        }

        private static float FitScale(float textWidth, float maxWidth)
            => textWidth <= maxWidth || textWidth <= 0f ? 1f : maxWidth / textWidth;
    }
}
