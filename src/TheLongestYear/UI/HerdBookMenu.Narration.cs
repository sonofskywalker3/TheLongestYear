using System.Collections.Generic;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The Herd Book page's row text, and the page read aloud for a screen reader through
    /// the book that hosts it (<see cref="OneBookMenu"/>).</summary>
    internal sealed partial class HerdBookMenu
    {
        // ---- row text (drawn by the page, spoken by the screen reader) ----

        /// <summary>The title drawn now: the slot count, or the picker's prompt.</summary>
        private string TitleText()
            => _pickerList != null
                ? Strings.Get("menu.herdbook.choose")
                : Strings.Get("menu.herdbook.title", new Dictionary<string, string>
                    {
                        ["used"] = HerdBookRules.Used(_meta.HerdBook, _slots.Count).ToString(),
                        ["total"] = _slots.Count.ToString(),
                    });

        /// <summary>A slot whose building is not kept takes no animal. One filled before that rule
        /// still says what it is waiting for.</summary>
        private string SlotNote(HerdSlotKind kind, HerdEntry entry)
            => entry == null || HerdBookRules.BuildingKept(kind, _meta.HasUpgrade)
                ? null
                : Strings.Get("menu.herdbook.needs-keep", new Dictionary<string, string> { ["keep"] = KeepName(kind) });

        /// <summary>A slot row as one line of text: empty, waiting for its keep, or the animal in it.</summary>
        private string SlotLabel(HerdSlotKind kind, HerdEntry entry)
        {
            string kindName = HerdSlotRules.DisplayName(kind);
            if (entry != null)
                return Strings.Get("menu.herdbook.filled-slot", new Dictionary<string, string>
                {
                    ["kind"] = kindName,
                    ["name"] = entry.Name,
                    ["hearts"] = HerdBookRules.Hearts(entry.Friendship).ToString(),
                });
            return HerdBookRules.BuildingKept(kind, _meta.HasUpgrade)
                ? Strings.Get("menu.herdbook.empty-slot", new Dictionary<string, string> { ["kind"] = kindName })
                : Strings.Get("menu.herdbook.locked-slot", new Dictionary<string, string> { ["kind"] = kindName, ["keep"] = KeepName(kind) });
        }

        /// <summary>A picker row as one line of text.</summary>
        private static string PickerLabel(FarmAnimal animal)
            => Strings.Get("menu.herdbook.picker-row", new Dictionary<string, string>
            {
                ["name"] = animal.Name,
                ["species"] = animal.displayType,
                ["hearts"] = HerdBookRules.Hearts(animal.friendshipTowardFarmer.Value).ToString(),
            });

        // ---- screen reader ----

        public string NarrationHeader()
            => NarrationText.Sentences(
                TitleText(),
                _subtitle,
                _pickerList != null ? Strings.Get("narrate.book.picker-help") : null);

        public string NarrationSection() => null;

        public string NarrateFocus()
        {
            ClickableComponent focus = currentlySnappedComponent;
            if (focus == null) return "";
            int id = focus.myID;
            bool picking = _pickerList != null;
            int total = picking ? _pickerList.Count : _slots.Count;
            int start = picking ? _pickerScroll : _scroll;

            if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
            {
                int index = start + (id - RowIdBase);
                if (index >= total) return "";
                if (picking)
                    return NarrationText.Sentences(PickerLabel(_pickerList[index]), NarrationParts.Position(index, total));
                HerdSlotKind kind = _slots[index];
                HerdEntry entry = HerdBookRules.EntryAt(_meta.HerdBook, index);
                return NarrationText.Sentences(SlotLabel(kind, entry), SlotNote(kind, entry), NarrationParts.Position(index, total));
            }
            if (id == ScrollUpId) return NarrationParts.Scroll(true, start, _rowsPerPage, total);
            if (id == ScrollDownId) return NarrationParts.Scroll(false, start, _rowsPerPage, total);
            if (focus == upperRightCloseButton)
                return picking ? Strings.Get("narrate.book.back") : Strings.Get("narrate.shrine.close");
            return "";
        }
    }
}
