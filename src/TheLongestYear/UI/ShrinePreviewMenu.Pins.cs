using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>Pinned keeps on the Plan tab: Y or right-click on a keep row pins its chain, the same
    /// pin the rewind shrine uses (<see cref="JunimoShrineMenu"/>). Pinned rows lead their category
    /// section, a pinned locked keep included. Host only.</summary>
    internal sealed partial class ShrinePreviewMenu
    {
        /// <summary>Gap between the star and the row's name.</summary>
        private const int StarGap = 4;

        private readonly KeepPinInput _pins = new();

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            base.receiveRightClick(x, y, playSound);
            if (!KeepPinInput.IsFreshRightClick())
                return;
            for (int i = 0; i < _rowsPerPage; i++)
            {
                int idx = _scrollIndex + i;
                if (idx >= _rows.Count) break;
                if (_rowSlots[i].containsPoint(x, y)) { TogglePin(_rows[idx], moveSnap: false); return; }
            }
        }

        /// <summary>Controller Y on a snapped row. True when the press was used.</summary>
        private bool TryPinSnappedRow()
        {
            if (currentlySnappedComponent == null) return false;
            int slot = currentlySnappedComponent.myID - RowIdBase;
            if (slot < 0 || slot >= _rowsPerPage) return false;
            int idx = _scrollIndex + slot;
            if (idx >= _rows.Count) return false;
            return TogglePin(_rows[idx], moveSnap: true);
        }

        private static bool IsKeepRow(Row row) => row.Def != null && row.Kind is RowKind.Upgrade or RowKind.Locked;

        private bool TogglePin(Row row, bool moveSnap)
        {
            if (_tab != ShrineTab.Plan || !IsKeepRow(row) || !_pins.TryToggle(_state, row.Def.Id))
                return false;
            // An unpinned locked keep drops back into its category's Locked section. Open that
            // section so the row stays on screen and the focus can follow it; a collapsed section
            // would hide it and leave Y on whatever row slid into its slot.
            if (row.Kind == RowKind.Locked && !PinnedKeeps.IsPinned(_state, row.Def.Id))
                _expandedLocked.Add(row.Def.Category);
            BuildRows();
            FollowRow(row.Def.Id, moveSnap);
            return true;
        }

        /// <summary>After the rebuild, scroll so the toggled keep is still on screen and, on a
        /// controller, keep the focus on it.</summary>
        private void FollowRow(string upgradeId, bool moveSnap)
        {
            int index = _rows.FindIndex(r => IsKeepRow(r) && r.Def.Id == upgradeId);
            ClampScroll();
            if (index < 0) return;
            if (index < _scrollIndex) _scrollIndex = index;
            else if (index >= _scrollIndex + _rowsPerPage) _scrollIndex = index - _rowsPerPage + 1;
            ClampScroll();
            if (!moveSnap) return;
            currentlySnappedComponent = getComponentWithID(RowIdBase + index - _scrollIndex);
            snapCursorToCurrentSnappedComponent();
        }

        private string PinnedSuffix(Row row)
            => row.Def != null && PinnedKeeps.IsPinned(_state, row.Def.Id) ? "\n" + Strings.Get("menu.shrine.pinned") : "";

        /// <summary>The star sits just left of the row's name, on every keep row (Plan and Active).</summary>
        private void DrawPinStar(SpriteBatch b, Row row, int rowY)
        {
            if (row.Def == null || !PinnedKeeps.IsPinned(_state, row.Def.Id)) return;
            int nameX = row.Kind == RowKind.Locked ? _listX + SubRowIndent : _listX + 24;
            int size = KeepPinBadge.SizeFor(KeepPinBadge.PlanScale);
            KeepPinBadge.Draw(b, new Vector2(nameX - StarGap - size, rowY + 10), KeepPinBadge.PlanScale);
        }
    }
}
