using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Donations;

namespace TheLongestYear.UI
{
    /// <summary>The Donate tab (Randomizer spec section 7): this week's shrine goals as a row of
    /// slots, the player's inventory below. Clicking an item that fills an open goal donates it
    /// through <see cref="ShrineDonationService.Donate"/>; anything else plays "cancel" and changes
    /// nothing. No item is ever picked up onto the cursor.</summary>
    internal sealed partial class ShrinePreviewMenu
    {
        private const int GoalSlotIdBase = 6400;
        private const int GoalCell = 64;
        private const int GoalCellGap = 24;
        private const int GoalStackTextH = 32;
        private const int GoalRowGap = 8;
        private const int DonateHeaderH = 48;
        private const int DonateNoteH = 40;
        private const int CheckScale = 3;
        private const int CheckPx = 9 * CheckScale;
        private const float DoneIconAlpha = 0.4f;

        // Vanilla player inventory, as on a Community Center bundle page.
        private const int InventoryCapacity = 36;
        private const int InventoryRows = 3;
        private const int InventoryColumns = InventoryCapacity / InventoryRows;
        private const int InventorySlotPx = 64;
        private const int InventoryTopOverhang = 16;   // vanilla lifts the first row 16px
        private const int InventoryBottom = 196;       // last row's bottom, from the menu's y
        private const int BottomMargin = 40;

        private static readonly Rectangle CheckSource = new(236, 425, 9, 9);   // OptionsCheckbox, checked
        private static readonly Rectangle SlotSource = new(128, 128, 64, 64);   // menu tile 10, an inventory slot

        private readonly ShrineDonationService _donations;
        private readonly bool _showDonate;
        private readonly List<Item> _goalItems = new();
        private readonly List<ClickableComponent> _goalSlots = new();
        private InventoryMenu _inventory;
        private Item _hoveredItem;
        private int _donateHeaderY;
        private int _donateNoteY;

        private IReadOnlyList<ShrineGoal> DonateGoals => _donations?.Goals ?? Array.Empty<ShrineGoal>();

        /// <summary>Where the tab strip's down arrow lands on the Donate tab.</summary>
        private static int DonateFirstTargetId() => GoalSlotIdBase;

        /// <summary>Icons for this week's goals (rebuilt after each donation).</summary>
        private void BuildDonateGoals()
        {
            _goalItems.Clear();
            foreach (ShrineGoal goal in DonateGoals)
                _goalItems.Add(ItemRegistry.Create(goal.ItemId, 1, 0, allowNull: true));
        }

        /// <summary>Only items that can fill an open goal light up in the inventory.</summary>
        private bool CanDonate(Item item) => item != null && _donations != null && _donations.GoalIndexFor(item) >= 0;

        private void LayoutDonate()
        {
            _goalSlots.Clear();
            _inventory = null;
            _hoveredItem = null;
            if (_tab != ShrineTab.Donate) return;

            int top = yPositionOnScreen + TabsTop + TabStripH;
            _donateHeaderY = top;
            int slotsY = top + DonateHeaderH;
            int pitch = GoalCell + GoalCellGap;
            int rowPitch = GoalCell + GoalStackTextH + GoalRowGap;
            int perRow = Math.Max(1, (_listWidth + GoalCellGap) / pitch);
            int count = DonateGoals.Count;
            int goalRows = Math.Max(1, (count + perRow - 1) / perRow);
            _donateNoteY = slotsY + goalRows * rowPitch;

            int invX = xPositionOnScreen + (width - InventoryColumns * InventorySlotPx) / 2;
            int invY = Math.Min(_donateNoteY + DonateNoteH + InventoryTopOverhang,
                yPositionOnScreen + height - BottomMargin - InventoryBottom);
            _inventory = new InventoryMenu(invX, invY, playerInventory: true, actualInventory: null,
                highlightMethod: CanDonate, capacity: InventoryCapacity, rows: InventoryRows);

            int donateTabId = TabIdBase + (int)ShrineTab.Donate;
            for (int i = 0; i < count; i++)
            {
                int col = i % perRow, row = i / perRow;
                var bounds = new Rectangle(_listX + col * pitch, slotsY + row * rowPitch, GoalCell, GoalCell);
                _goalSlots.Add(new ClickableComponent(bounds, "goal-" + i)
                {
                    myID = GoalSlotIdBase + i,
                    leftNeighborID = col > 0 ? GoalSlotIdBase + i - 1 : -1,
                    rightNeighborID = col < perRow - 1 && i + 1 < count ? GoalSlotIdBase + i + 1 : -1,
                    upNeighborID = row == 0 ? donateTabId : GoalSlotIdBase + i - perRow,
                    downNeighborID = i + perRow < count ? GoalSlotIdBase + i + perRow : InventoryColumnBelow(bounds.Center.X),
                });
            }

            // The inventory's top row goes up to the nearest goal in the last goal row (or the tab).
            int lastRowStart = count == 0 ? 0 : (count - 1) / perRow * perRow;
            foreach (ClickableComponent slot in _inventory.inventory)
            {
                if (slot.myID >= InventoryColumns) continue;
                slot.upNeighborID = count == 0 ? donateTabId : NearestGoalId(slot.bounds.Center.X, lastRowStart, count);
            }
        }

        private int InventoryColumnBelow(int x)
            => Math.Clamp((x - (xPositionOnScreen + (width - InventoryColumns * InventorySlotPx) / 2)) / InventorySlotPx,
                0, InventoryColumns - 1);

        private int NearestGoalId(int x, int from, int to)
        {
            int best = from, bestDist = int.MaxValue;
            for (int i = from; i < to; i++)
            {
                int d = Math.Abs(_goalSlots[i].bounds.Center.X - x);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return GoalSlotIdBase + best;
        }

        private void AddDonateTargets(List<ClickableComponent> into)
        {
            into.AddRange(_goalSlots);
            if (_inventory != null)
                into.AddRange(_inventory.inventory);
        }

        /// <summary>Click (or gamepad A, which vanilla turns into a click at the snapped slot).
        /// Only <see cref="ShrineDonationService.Donate"/> removes anything, and only when it
        /// credits the goal in the same call.</summary>
        private void DonateClick(int x, int y)
        {
            Item item = _inventory?.getItemAt(x, y);
            if (item == null || _donations == null) return;
            int goal = _donations.GoalIndexFor(item);
            if (goal >= 0 && _donations.Donate(goal, item))
            {
                Game1.playSound("newArtifact");
                _hoveredItem = null;
                BuildRows();
                return;
            }
            Game1.playSound("cancel");
        }

        private void DonateHover(int x, int y)
        {
            _hoveredItem = null;
            for (int i = 0; i < _goalSlots.Count && i < DonateGoals.Count; i++)
            {
                if (!_goalSlots[i].containsPoint(x, y)) continue;
                ShrineGoal goal = DonateGoals[i];
                var tokens = new Dictionary<string, string>
                {
                    ["name"] = _goalItems.Count > i && _goalItems[i] != null ? _goalItems[i].DisplayName : goal.ItemId,
                    ["stack"] = goal.Stack.ToString(),
                };
                _hoverText = goal.Deposited
                    ? Strings.Get("shrine.donate.goal-done-hover", tokens)
                    : Strings.Get("shrine.donate.goal-hover", tokens);
                return;
            }
            _hoveredItem = _inventory?.hover(x, y, null);
        }

        private void DrawDonate(SpriteBatch b)
        {
            Utility.drawTextWithShadow(b, Strings.Get("shrine.donate.header"), Game1.dialogueFont,
                new Vector2(_listX, _donateHeaderY), Game1.textColor);

            bool allDone = true;
            for (int i = 0; i < _goalSlots.Count && i < DonateGoals.Count; i++)
            {
                ShrineGoal goal = DonateGoals[i];
                Rectangle r = _goalSlots[i].bounds;
                allDone &= goal.Deposited;
                b.Draw(Game1.menuTexture, r, SlotSource, Color.White);
                Item icon = i < _goalItems.Count ? _goalItems[i] : null;
                icon?.drawInMenu(b, new Vector2(r.X, r.Y), 1f, goal.Deposited ? DoneIconAlpha : 1f, 0.9f,
                    StackDrawType.Hide, Color.White, drawShadow: true);

                string stack = Strings.Get("shrine.donate.stack",
                    new Dictionary<string, string> { ["stack"] = goal.Stack.ToString() });
                Vector2 size = Game1.smallFont.MeasureString(stack);
                Utility.drawTextWithShadow(b, stack, Game1.smallFont,
                    new Vector2(r.X + (r.Width - size.X) / 2f, r.Bottom + 2), Game1.textColor);

                if (goal.Deposited)
                    b.Draw(Game1.mouseCursors, new Rectangle(r.Right - CheckPx, r.Y, CheckPx, CheckPx), CheckSource, Color.White);
            }

            string note = allDone ? Strings.Get("shrine.donate.all-done") : Strings.Get("shrine.donate.hint");
            Utility.drawTextWithShadow(b, note, Game1.smallFont,
                new Vector2(_listX, _donateNoteY), Game1.textColor * 0.85f);

            _inventory?.draw(b);
        }

        /// <summary>Vanilla item tooltip for the inventory slot under the cursor.</summary>
        private void DrawDonateTooltip(SpriteBatch b)
        {
            if (_tab != ShrineTab.Donate || _hoveredItem == null) return;
            IClickableMenu.drawToolTip(b, _hoveredItem.getDescription(), _hoveredItem.DisplayName, _hoveredItem);
        }
    }
}
