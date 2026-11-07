using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// The picker's source tabs: with mods that add recipes, a row of tabs above the list shows one
    /// source at a time, the base game's first, then each supported mod's, then other mods' and all
    /// (<see cref="RecipeSources"/>). Every tab is sorted A to Z. With the base game's recipes only, no
    /// tab row is drawn and the list is just sorted. LB and RB, left and right on a row, or a click
    /// switch tabs; the tab last chosen is where the picker opens next while the book stays open.
    /// </summary>
    internal abstract partial class RecipeBookMenu
    {
        /// <summary>The game's neighbour id meaning "ask customSnapBehavior".</summary>
        private const int CustomSnap = -7777;
        private const int SourceTabBand = 64;
        private const int SourceTabHeight = 48;
        private const int SourceTabPad = 20;
        private const int SourceTabGap = 8;
        private const int DirectionRight = 1;
        private const int DirectionLeft = 3;

        /// <summary>Which installed supported mod adds each item, published when the lexicon loads
        /// (<see cref="TheLongestYear.Loop.LoadedLayers.ItemOwners"/>). Empty until then: every mod
        /// recipe reads as another mod's.</summary>
        internal static IReadOnlyDictionary<string, string> ItemOwners { get; set; } = new Dictionary<string, string>();

        /// <summary>A loaded mod's name by UniqueID, published with <see cref="ItemOwners"/>.</summary>
        internal static Func<string, string> ModNameOf { get; set; } = id => id;

        private IReadOnlyList<RecipeSourceTab> _sourceTabs;
        private int _sourceTab;
        private string _lastSourceKey;
        private readonly List<(Rectangle Bounds, int Tab)> _sourceTabBounds = new List<(Rectangle, int)>();
        private Rectangle _sourcePrev, _sourceNext;   // the overflow arrows, empty when not drawn
        private const int MaxLabelShare = 2;           // one tab's label takes at most half the row
        private const string Ellipsis = "...";

        /// <summary>Whether the open picker shows its tab row (more than one source).</summary>
        public bool HasSourceTabs => _pickerList != null && _sourceTabs != null && _sourceTabs.Count > 1;

        private int SourceBand => HasSourceTabs ? SourceTabBand : 0;

        /// <summary>Sort the bankable recipes into tabs and return the opening tab's list.</summary>
        private List<string> OpenSources(List<string> recipes)
        {
            _sourceTabs = RecipeSources.Tabs(
                recipes.Select(id => (id, RecipeDisplayName(id, _isCooking), SourceOf(id))), SourceLabel);
            if (_sourceTabs.Count == 0) { _sourceTabs = null; return recipes; }
            _sourceTab = RecipeSources.StartTab(_sourceTabs, _lastSourceKey);
            return _sourceTabs[_sourceTab].RecipeIds.ToList();
        }

        private void StepSource(int delta)
        {
            if (!HasSourceTabs) return;
            ShowSource((_sourceTab + delta + _sourceTabs.Count) % _sourceTabs.Count);
        }

        private void ShowSource(int tab)
        {
            if (tab == _sourceTab) return;
            _sourceTab = tab;
            _lastSourceKey = _sourceTabs[tab].Key;
            _pickerList = _sourceTabs[tab].RecipeIds.ToList();
            _pickerScroll = 0;
            Game1.playSound("shwip");
            SnapToRow(0);
        }

        /// <summary>A click on a tab shows it; true when the click was on one.</summary>
        private bool ClickSourceTab(int x, int y)
        {
            if (!HasSourceTabs) return false;
            if (_sourcePrev.Contains(x, y)) { StepSource(-1); return true; }
            if (_sourceNext.Contains(x, y)) { StepSource(+1); return true; }
            foreach ((Rectangle bounds, int tab) in _sourceTabBounds)
                if (bounds.Contains(x, y)) { ShowSource(tab); return true; }
            return false;
        }

        /// <summary>Left and right on a picker row switch tabs instead of leaving the list.</summary>
        private void ApplySourceNeighbours()
        {
            if (!HasSourceTabs) return;
            foreach (ClickableComponent row in _rowSlots)
            {
                row.leftNeighborID = CustomSnap;
                row.rightNeighborID = CustomSnap;
                // Otherwise the game rewrites them to wherever the snap came from.
                row.leftNeighborImmutable = true;
                row.rightNeighborImmutable = true;
            }
        }

        protected override void customSnapBehavior(int direction, int oldRegion, int oldID)
        {
            if (direction == DirectionRight) StepSource(+1);
            else if (direction == DirectionLeft) StepSource(-1);
            else base.customSnapBehavior(direction, oldRegion, oldID);
        }

        private string SourceOf(string recipeId)
            => RecipeSources.SourceOf(recipeId, OutputOf(recipeId),
                _isCooking ? VanillaRecipeKeys.Cooking : VanillaRecipeKeys.Crafting, ItemOwners);

        /// <summary>The qualified id of the item a recipe makes, or null when it cannot be read.</summary>
        private string OutputOf(string recipeId)
        {
            try
            {
                var recipe = new CraftingRecipe(recipeId, _isCooking);
                string id = recipe.itemToProduce?.FirstOrDefault();
                if (string.IsNullOrEmpty(id)) return null;
                return id.StartsWith("(") ? id : (recipe.bigCraftable ? "(BC)" : "(O)") + id;
            }
            catch (Exception ex)
            {
                TheLongestYear.Loop.PatchLog.TraceOnce("recipe-output:" + recipeId,
                    $"Recipe '{recipeId}' output could not be read ({ex.GetType().Name}); it is listed under other mods.");
                return null;
            }
        }

        private static string SourceLabel(string key) => key switch
        {
            RecipeSources.Vanilla => Strings.Get("menu.books.source.vanilla"),
            RecipeSources.OtherMods => Strings.Get("menu.books.source.other"),
            RecipeSources.All => Strings.Get("menu.books.source.all"),
            _ => RecipeSources.TabName(ModNameOf(key) ?? key),
        };

        /// <summary>The tab row, left to right from the list's left edge. When the tabs are wider than
        /// the list, the row starts late enough that the chosen tab shows, with an arrow on a cut side.</summary>
        private void DrawSourceTabs(SpriteBatch b)
        {
            _sourceTabBounds.Clear();
            _sourcePrev = _sourceNext = Rectangle.Empty;
            if (!HasSourceTabs || _rowSlots.Count == 0) return;
            Rectangle list = _rowSlots[0].bounds;
            int top = list.Y - SourceTabBand + (SourceTabBand - SourceTabHeight) / 2 - RowSpacing / 2;
            int room = list.Width;
            int arrow = (int)Game1.smallFont.MeasureString("<").X + SourceTabGap * 2;
            int maxLabel = Math.Max(1, room / MaxLabelShare - SourceTabPad * 2);
            string[] labels = _sourceTabs.Select(t => FitLabel(t.Label, maxLabel)).ToArray();
            int[] widths = labels.Select(l => (int)Game1.smallFont.MeasureString(l).X + SourceTabPad * 2).ToArray();

            int first = 0;
            while (first < _sourceTab && Span(widths, first, _sourceTab) + (first > 0 ? arrow : 0) > room)
                first++;
            int x = list.X;
            if (first > 0)
            {
                _sourcePrev = new Rectangle(x, top, arrow, SourceTabHeight);
                Utility.drawTextWithShadow(b, "<", Game1.smallFont, new Vector2(x + SourceTabGap / 2, top + 10), Game1.textColor);
                x += arrow;
            }
            int i = first;
            for (; i < _sourceTabs.Count; i++)
            {
                bool more = i < _sourceTabs.Count - 1;
                if (x + widths[i] + (more ? arrow : 0) > list.X + room && i > _sourceTab) break;
                var bounds = new Rectangle(x, top, widths[i], SourceTabHeight);
                bool chosen = i == _sourceTab;
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    bounds.X, bounds.Y, bounds.Width, bounds.Height, chosen ? Color.White : Color.White * 0.45f, 1f, false);
                string label = labels[i];
                Utility.drawTextWithShadow(b, label, Game1.smallFont,
                    new Vector2(bounds.X + SourceTabPad, TextInk.CentredY(Game1.smallFont, label, bounds.Y, bounds.Height)),
                    chosen ? Game1.textColor : Game1.textColor * 0.7f);
                _sourceTabBounds.Add((bounds, i));
                x += widths[i] + SourceTabGap;
            }
            if (i < _sourceTabs.Count)
            {
                _sourceNext = new Rectangle(x, top, arrow, SourceTabHeight);
                Utility.drawTextWithShadow(b, ">", Game1.smallFont, new Vector2(x + SourceTabGap / 2, top + 10), Game1.textColor);
            }
        }

        /// <summary>A label cut with an ellipsis to fit <paramref name="maxWidth"/> pixels.</summary>
        private static string FitLabel(string label, int maxWidth)
        {
            if (Game1.smallFont.MeasureString(label).X <= maxWidth) return label;
            for (int n = label.Length - 1; n > 0; n--)
            {
                string cut = label.Substring(0, n).TrimEnd() + Ellipsis;
                if (Game1.smallFont.MeasureString(cut).X <= maxWidth) return cut;
            }
            return Ellipsis;
        }

        private static int Span(int[] widths, int from, int to)
        {
            int sum = 0;
            for (int i = from; i <= to; i++) sum += widths[i] + SourceTabGap;
            return sum;
        }
    }
}
