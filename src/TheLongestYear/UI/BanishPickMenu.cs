using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The banish pick, opened as a CHILD of the Junimo Shrine (so closing it returns to the
    /// shrine and never fires the shrine's exitFunction, which would start the reset). Two tabs: this
    /// loop's bundles and the items the board asks for. Picking a row buys the banish through
    /// <c>onPick</c>; a successful pick closes the menu. B, Escape or right-click closes without buying.</summary>
    internal sealed class BanishPickMenu : IClickableMenu
    {
        private const int PanelWidth = 760;
        private const int PanelHeight = 720;
        private const int Pad = 32;
        private const int TabWidth = 200;
        private const int TabHeight = 56;
        private const int RowHeight = 64;
        private const int RowSpacing = 4;
        private const int IconSize = 48;
        private const int HeaderHeight = 150;

        private const int TabIdBase = 8100;
        private const int RowIdBase = 8200;
        private const int ScrollUpId = 8900;
        private const int ScrollDownId = 8901;

        private static readonly Rectangle BoxSource = new(0, 256, 60, 60);

        private sealed class Choice
        {
            public BanishKind Kind;
            public string Key;
            public string Label;
            public Item Icon;
            public bool Taken;
        }

        /// <summary>Buys a banish; returns true when it was bought (the menu then closes).</summary>
        private readonly Func<BanishKind, string, string, bool> _onPick;
        private readonly Func<long> _price;
        private readonly Func<long> _jp;
        private readonly List<Choice>[] _lists = { new(), new() };
        private int _tab;
        private int _scroll;
        private int _rowsPerPage;
        private bool _armed;

        private readonly List<ClickableComponent> _tabs = new();
        private readonly List<ClickableComponent> _rows = new();
        private ClickableTextureComponent _up;
        private ClickableTextureComponent _down;

        public BanishPickMenu(
            IReadOnlyList<BanishBundleChoice> bundles, IReadOnlyList<string> items,
            IReadOnlyList<BanishPick> alreadyBanished,
            Func<BanishKind, string, string, bool> onPick, Func<long> price, Func<long> jp)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _onPick = onPick;
            _price = price;
            _jp = jp;
            foreach (BanishBundleChoice bundle in bundles)
                _lists[0].Add(new Choice { Kind = BanishKind.Bundle, Key = bundle.Name, Label = bundle.DisplayName });
            foreach (string id in items)
            {
                Item icon = ItemRegistry.Create(id, allowNull: true);
                _lists[1].Add(new Choice { Kind = BanishKind.Item, Key = id, Label = icon?.DisplayName ?? id, Icon = icon });
            }
            _lists[1].Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase));
            foreach (List<Choice> list in _lists)
                foreach (Choice choice in list)
                    foreach (BanishPick pick in alreadyBanished)
                        if (pick.Kind == choice.Kind && pick.Key == choice.Key) choice.Taken = true;
            Layout();
        }

        private List<Choice> Active => _lists[_tab];

        private void Layout()
        {
            width = Math.Min(PanelWidth, Game1.uiViewport.Width - 64);
            height = Math.Min(PanelHeight, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int listTop = yPositionOnScreen + HeaderHeight + TabHeight + 8;
            int listHeight = yPositionOnScreen + height - Pad - listTop;
            _rowsPerPage = Math.Max(1, listHeight / (RowHeight + RowSpacing));
            int listWidth = width - Pad * 2 - 56;

            _tabs.Clear();
            for (int i = 0; i < _lists.Length; i++)
                _tabs.Add(new ClickableComponent(
                    new Rectangle(xPositionOnScreen + Pad + i * (TabWidth + 8), yPositionOnScreen + HeaderHeight, TabWidth, TabHeight), "tab-" + i)
                {
                    myID = TabIdBase + i,
                    leftNeighborID = i > 0 ? TabIdBase + i - 1 : -1,
                    rightNeighborID = i < _lists.Length - 1 ? TabIdBase + i + 1 : -1,
                    downNeighborID = RowIdBase,
                });

            _rows.Clear();
            for (int i = 0; i < _rowsPerPage; i++)
                _rows.Add(new ClickableComponent(
                    new Rectangle(xPositionOnScreen + Pad, listTop + i * (RowHeight + RowSpacing), listWidth, RowHeight), "row-" + i)
                {
                    myID = RowIdBase + i,
                    upNeighborID = i == 0 ? TabIdBase : RowIdBase + i - 1,
                    downNeighborID = i == _rowsPerPage - 1 ? -1 : RowIdBase + i + 1,
                    rightNeighborID = ScrollUpId,
                });

            int arrowX = xPositionOnScreen + width - Pad - 44;
            _up = new ClickableTextureComponent("up", new Rectangle(arrowX, listTop, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f)
            { myID = ScrollUpId, downNeighborID = ScrollDownId, leftNeighborID = RowIdBase };
            _down = new ClickableTextureComponent("down", new Rectangle(arrowX, listTop + listHeight - 48, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f)
            { myID = ScrollDownId, upNeighborID = ScrollUpId, leftNeighborID = RowIdBase };

            initializeUpperRightCloseButton();
            allClickableComponents = new List<ClickableComponent>();
            allClickableComponents.AddRange(_tabs);
            allClickableComponents.AddRange(_rows);
            allClickableComponents.Add(_up);
            allClickableComponents.Add(_down);
            if (upperRightCloseButton != null) allClickableComponents.Add(upperRightCloseButton);

            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            {
                currentlySnappedComponent = _tabs[_tab];
                snapCursorToCurrentSnappedComponent();
            }
        }

        public override void populateClickableComponentList()
        {
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            Layout();
        }

        public override bool areGamePadControlsImplemented() => true;

        public override void update(GameTime time)
        {
            base.update(time);
            if (!_armed
                && !Game1.input.GetGamePadState().IsButtonDown(Buttons.A)
                && Game1.input.GetMouseState().LeftButton == ButtonState.Released)
                _armed = true;
        }

        private void Close()
        {
            Game1.playSound("bigDeSelect");
            GetParentMenu()?.SetChildMenu(null);
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape || Game1.options.doesInputListContain(Game1.options.menuButton, key))
            {
                Close();
                return;
            }
            // Stepping past the first or last visible row scrolls the list instead of leaving it.
            int snapped = currentlySnappedComponent?.myID ?? -1;
            if (snapped == RowIdBase + _rowsPerPage - 1 && Game1.options.doesInputListContain(Game1.options.moveDownButton, key)
                && _scroll + _rowsPerPage < Active.Count)
            {
                Scroll(1);
                return;
            }
            if (snapped == RowIdBase && _scroll > 0 && Game1.options.doesInputListContain(Game1.options.moveUpButton, key))
            {
                Scroll(-1);
                return;
            }
            base.receiveKeyPress(key);
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) => Close();

        public override void receiveScrollWheelAction(int direction) => Scroll(direction > 0 ? -1 : 1);

        public override void receiveGamePadButton(Buttons b)
        {
            switch (b)
            {
                case Buttons.B:
                case Buttons.Back:
                    Close();
                    return;
                case Buttons.A:
                    if (currentlySnappedComponent != null) Activate(currentlySnappedComponent.myID);
                    return;
                case Buttons.LeftShoulder:
                    SetTab(0);
                    return;
                case Buttons.RightShoulder:
                    SetTab(1);
                    return;
            }
            base.receiveGamePadButton(b);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (upperRightCloseButton != null && upperRightCloseButton.containsPoint(x, y)) { Close(); return; }
            foreach (ClickableComponent c in allClickableComponents)
                if (c.containsPoint(x, y)) { Activate(c.myID); return; }
        }

        private void Activate(int id)
        {
            if (id >= TabIdBase && id < TabIdBase + _lists.Length) { SetTab(id - TabIdBase); return; }
            if (id == ScrollUpId) { Scroll(-1); return; }
            if (id == ScrollDownId) { Scroll(1); return; }
            if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
            {
                int index = _scroll + (id - RowIdBase);
                if (index < Active.Count) Pick(Active[index]);
            }
        }

        private void SetTab(int tab)
        {
            if (tab == _tab) return;
            _tab = tab;
            _scroll = 0;
            Game1.playSound("smallSelect");
        }

        private void Scroll(int delta)
        {
            int max = Math.Max(0, Active.Count - _rowsPerPage);
            int next = Math.Clamp(_scroll + delta, 0, max);
            if (next == _scroll) return;
            _scroll = next;
            Game1.playSound("shwip");
        }

        private void Pick(Choice choice)
        {
            if (!_armed || choice.Taken) return;
            _armed = false;   // one press, one purchase
            if (_onPick(choice.Kind, choice.Key, choice.Label))
                GetParentMenu()?.SetChildMenu(null);
            else
                Game1.playSound("cancel");
        }

        /// <summary>Debug bridge (tly_banish pick): pick by tab and index as a click would.</summary>
        internal bool DebugPick(int tab, int index)
        {
            if (tab < 0 || tab >= _lists.Length || index < 0 || index >= _lists[tab].Count) return false;
            _armed = true;
            Pick(_lists[tab][index]);
            return true;
        }

        internal IReadOnlyList<string> DebugList(int tab)
        {
            var lines = new List<string>();
            if (tab < 0 || tab >= _lists.Length) return lines;
            for (int i = 0; i < _lists[tab].Count; i++)
                lines.Add($"{i}: {_lists[tab][i].Key} ({_lists[tab][i].Label}){(_lists[tab][i].Taken ? " taken" : "")}");
            return lines;
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
            drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            string title = Strings.Get("banish.pick.title");
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
                new Vector2(xPositionOnScreen + (width - titleSize.X) / 2f, yPositionOnScreen + Pad), Game1.textColor);

            long price = _price();
            bool affordable = _jp() >= price;
            string subtitle = Strings.Get("banish.pick.subtitle") + "  "
                + Strings.Get("menu.shrine.cost", new Dictionary<string, string> { ["cost"] = price.ToString() })
                + (affordable ? "" : Strings.Get("menu.shrine.insufficient"));
            Utility.drawTextWithShadow(b, subtitle, Game1.smallFont,
                new Vector2(xPositionOnScreen + Pad, yPositionOnScreen + Pad + titleSize.Y + 16),
                affordable ? Game1.textColor : Color.Red);

            for (int i = 0; i < _tabs.Count; i++)
            {
                Rectangle r = _tabs[i].bounds;
                drawTextureBox(b, Game1.menuTexture, BoxSource, r.X, r.Y, r.Width, r.Height,
                    i == _tab ? Color.White : Color.White * 0.6f, 1f, false);
                string label = Strings.Get(i == 0 ? "banish.pick.tab.bundles" : "banish.pick.tab.items");
                Vector2 size = Game1.smallFont.MeasureString(label);
                Utility.drawTextWithShadow(b, label, Game1.smallFont,
                    new Vector2(r.X + (r.Width - size.X) / 2f, r.Y + (r.Height - size.Y) / 2f), Game1.textColor);
            }

            if (Active.Count == 0)
            {
                Rectangle first = _rows[0].bounds;
                Utility.drawTextWithShadow(b, Strings.Get("banish.pick.empty"), Game1.smallFont,
                    new Vector2(first.X + 16, first.Y + 16), Game1.textColor);
            }
            bool gamepad = Game1.options.snappyMenus && Game1.options.gamepadControls;
            for (int i = 0; i < _rowsPerPage && _scroll + i < Active.Count; i++)
            {
                Choice choice = Active[_scroll + i];
                Rectangle r = _rows[i].bounds;
                bool hovered = r.Contains(Game1.getMouseX(), Game1.getMouseY())
                    || gamepad && currentlySnappedComponent == _rows[i];
                drawTextureBox(b, Game1.menuTexture, BoxSource, r.X, r.Y, r.Width, r.Height,
                    choice.Taken ? Color.White * 0.5f : Color.White, 1f, false);
                if (hovered && !choice.Taken)
                    b.Draw(Game1.staminaRect, new Rectangle(r.X + 8, r.Y + 8, r.Width - 16, r.Height - 16), Color.Wheat * 0.25f);
                int textX = r.X + 16;
                if (choice.Icon != null)
                {
                    choice.Icon.drawInMenu(b, new Vector2(r.X + 8, r.Y + (r.Height - 64) / 2f), IconSize / 64f);
                    textX = r.X + 16 + IconSize + 8;
                }
                string text = choice.Taken ? choice.Label + "  (" + Strings.Get("banish.pick.taken") + ")" : choice.Label;
                Vector2 size = Game1.smallFont.MeasureString(text);
                Utility.drawTextWithShadow(b, text, Game1.smallFont,
                    new Vector2(textX, r.Y + (r.Height - size.Y) / 2f),
                    choice.Taken ? Game1.textColor * 0.6f : Game1.textColor);
            }

            _up.draw(b, _scroll > 0 ? Color.White : Color.Gray, 1f);
            _down.draw(b, _scroll + _rowsPerPage < Active.Count ? Color.White : Color.Gray, 1f);
            upperRightCloseButton?.draw(b);
            drawMouse(b);
        }
    }
}
