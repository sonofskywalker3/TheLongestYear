using Microsoft.Xna.Framework;

namespace TheLongestYear.UI
{
    /// <summary>What the headless back-out test (<c>tly_booktest</c>) needs to see inside the
    /// Cookbook and Craftbook: whether the recipe picker is up, and where an empty slot row is so
    /// the test can open the picker through the menu's real click handler.</summary>
    internal interface IRecipeBookMenu
    {
        /// <summary>True while the "pick a recipe" sub-mode is showing.</summary>
        bool PickerOpen { get; }

        /// <summary>Bounds of the first empty slot row on screen in slot mode, or null when the
        /// book is full or the picker is already up.</summary>
        Rectangle? FirstEmptySlotBounds();
    }
}
