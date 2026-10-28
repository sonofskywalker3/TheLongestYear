using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Focus;

namespace TheLongestYear.UI
{
    /// <summary>One theme's focus card pick: up to three cards, pick one. It cannot be closed
    /// without a pick. A pick replaces this menu with the next theme's, and the last pick closes
    /// it, which lets the deferred planning hub open on the next free tick. The run is read through
    /// <c>run()</c> on every use, because a reload can replace it while the menu is up.</summary>
    internal sealed class FocusPickMenu : IClickableMenu
    {
        private const int CardWidth = 300;
        private const int CardGap = 24;
        private const int Pad = 40;
        private const int CardPad = 20;
        private const int IconSize = 64;
        private const int TitleGap = 12;
        private const int SubtitleGap = 28;
        private const int LineGap = 12;
        private const int MaxCards = 3;
        private const float HoverTint = 0.2f;

        private static readonly Rectangle CardBoxSource = new(0, 256, 60, 60);

        private sealed class Card
        {
            public string ChainId;
            public Item Icon;
            public string Name;
            public string Description;
            public string Price;
            public string Note;
            public string NameText = "";
            public string DescriptionText = "";
            public string NoteText = "";
            public ClickableComponent Bounds;
        }

        private readonly IMonitor _monitor;
        private readonly Func<RunState> _run;
        private readonly FocusService _focus;
        private readonly FocusTheme _theme;
        private readonly List<Card> _cards = new();

        private string _title;
        private string _subtitle;
        private int _cardHeight;
        private int _cardsTop;
        private Card _hovered;
        private bool _picked;

        /// <summary>False until A and the left mouse button have both been seen released since this
        /// menu opened, so the press (or a held button's repeated clicks) that picked the previous
        /// hand can never pick on this one. Picks are permanent.</summary>
        private bool _armed;

        public FocusPickMenu(IMonitor monitor, Func<RunState> run, FocusService focus, FocusTheme theme)
        {
            _monitor = monitor;
            _run = run;
            _focus = focus;
            _theme = theme;

            FocusHand hand = run()?.FocusHandFor(theme);
            if (hand?.Chains != null)
                foreach (string chainId in hand.Chains)
                    _cards.Add(BuildCard(chainId));

            Layout();
            if (_cards.Count > 0)
            {
                currentlySnappedComponent = _cards[0].Bounds;
                if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                    snapCursorToCurrentSnappedComponent();
            }
        }

        private Card BuildCard(string chainId)
        {
            FocusQuote quote = _focus.QuoteFor(chainId);
            FocusCardFace face = FocusCardFace.For(chainId, quote);
            bool priced = quote != null && quote.Kind is FocusQuoteKind.Offer or FocusQuoteKind.KeepOnly;

            return new Card
            {
                ChainId = chainId,
                Icon = face.Icon,
                Name = face.Name,
                Description = face.Description,
                Price = priced
                    ? Strings.Get("focus.card.price", new Dictionary<string, string> { ["jp"] = quote.Price.ToString() })
                    : "",
                Note = face.Note,
            };
        }

        private string ThemeTitle() => _theme switch
        {
            FocusTheme.Tools => Strings.Get("focus.pick.title.tools"),
            FocusTheme.Buildings => Strings.Get("focus.pick.title.buildings"),
            _ => Strings.Get("focus.pick.title.knacks"),
        };

        // ---------- layout ----------

        private void Layout()
        {
            width = Pad * 2 + MaxCards * CardWidth + (MaxCards - 1) * CardGap;
            int textWidth = width - Pad * 2;
            int cardTextWidth = CardWidth - CardPad * 2;

            _title = Game1.parseText(ThemeTitle(), Game1.dialogueFont, textWidth);
            _subtitle = Game1.parseText(Strings.Get("focus.pick.subtitle"), Game1.smallFont, textWidth);
            int titleHeight = (int)Game1.dialogueFont.MeasureString(_title).Y;
            int subtitleHeight = (int)Game1.smallFont.MeasureString(_subtitle).Y;

            _cardHeight = 0;
            foreach (Card card in _cards)
            {
                card.NameText = Game1.parseText(card.Name, Game1.smallFont, cardTextWidth);
                card.DescriptionText = Game1.parseText(card.Description, Game1.smallFont, cardTextWidth);
                card.NoteText = card.Note.Length > 0 ? Game1.parseText(card.Note, Game1.smallFont, cardTextWidth) : "";
                _cardHeight = Math.Max(_cardHeight, CardContentHeight(card));
            }

            height = Pad + titleHeight + TitleGap + subtitleHeight + SubtitleGap + _cardHeight + Pad;
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = Math.Max(0, (Game1.uiViewport.Height - height) / 2);
            _cardsTop = yPositionOnScreen + Pad + titleHeight + TitleGap + subtitleHeight + SubtitleGap;

            int rowWidth = _cards.Count * CardWidth + Math.Max(0, _cards.Count - 1) * CardGap;
            int left = xPositionOnScreen + (width - rowWidth) / 2;
            allClickableComponents = new List<ClickableComponent>();
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Bounds = new ClickableComponent(
                    new Rectangle(left + i * (CardWidth + CardGap), _cardsTop, CardWidth, _cardHeight), _cards[i].ChainId)
                {
                    myID = i,
                    leftNeighborID = i > 0 ? i - 1 : -1,
                    rightNeighborID = i < _cards.Count - 1 ? i + 1 : -1,
                };
                allClickableComponents.Add(_cards[i].Bounds);
            }
        }

        private static int CardContentHeight(Card card)
        {
            int h = CardPad + IconSize + LineGap + TextHeight(card.NameText) + LineGap + TextHeight(card.DescriptionText);
            if (card.NoteText.Length > 0) h += LineGap + TextHeight(card.NoteText);
            if (card.Price.Length > 0) h += LineGap + TextHeight(card.Price);
            return h + CardPad;
        }

        private static int TextHeight(string text)
            => text.Length == 0 ? 0 : (int)Game1.smallFont.MeasureString(text).Y;

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            int snapped = currentlySnappedComponent?.myID ?? 0;
            Layout();
            if (snapped >= 0 && snapped < _cards.Count)
                currentlySnappedComponent = _cards[snapped].Bounds;
        }

        /// <summary>Keep the list built in <see cref="Layout"/>: the base reflects over public
        /// fields only and would find no cards.</summary>
        public override void populateClickableComponentList()
        {
        }

        // ---------- modal guard ----------

        public override bool readyToClose() => false;

        /// <summary>The menu handles A itself, so the game sends no emulated clicks for it.</summary>
        public override bool areGamePadControlsImplemented() => true;

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape) return;
            base.receiveKeyPress(key);
        }

        public override void receiveGamePadButton(Buttons b)
        {
            switch (b)
            {
                case Buttons.A:
                    Card target = GamePadTarget();
                    if (target != null) Pick(target);
                    return;
                case Buttons.B:
                case Buttons.Back:
                    return;
                case Buttons.DPadLeft:
                case Buttons.LeftThumbstickLeft:
                    applyMovementKey(3);
                    return;
                case Buttons.DPadRight:
                case Buttons.LeftThumbstickRight:
                    applyMovementKey(1);
                    return;
            }
            base.receiveGamePadButton(b);
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
        }

        /// <summary>The card A acts on: the snapped card with snappy menus on, otherwise the card
        /// under the free cursor (never a card the player is not pointing at).</summary>
        private Card GamePadTarget()
        {
            if (!Game1.options.snappyMenus)
                return CardAt(Game1.getMouseX(), Game1.getMouseY());
            int i = currentlySnappedComponent?.myID ?? -1;
            return i >= 0 && i < _cards.Count ? _cards[i] : null;
        }

        private Card CardAt(int x, int y)
        {
            foreach (Card card in _cards)
                if (card.Bounds.containsPoint(x, y)) return card;
            return null;
        }

        // ---------- picking ----------

        public override void update(GameTime time)
        {
            base.update(time);
            if (!_armed
                && !Game1.input.GetGamePadState().IsButtonDown(Buttons.A)
                && Game1.input.GetMouseState().LeftButton == ButtonState.Released)
                _armed = true;
            // Never leave an unclosable empty menu up (every card vanished from the hand).
            if (!_picked && _cards.Count == 0)
            {
                _picked = true;
                _monitor.Log($"Focus pick for {_theme} has no cards; moving on.", LogLevel.Trace);
                Advance();
            }
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hovered = null;
            foreach (Card card in _cards)
                if (card.Bounds.containsPoint(x, y)) _hovered = card;
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            Card card = CardAt(x, y);
            if (card != null) Pick(card);
        }

        /// <summary>Debug bridge: pick the card at this position (0 = left) as a click would.
        /// False if there is no such card.</summary>
        internal bool PickAt(int index)
        {
            if (index < 0 || index >= _cards.Count) return false;
            Pick(_cards[index]);
            return true;
        }

        private void Pick(Card card)
        {
            if (_picked || !_armed) return;
            _picked = true;
            bool chosen = FocusPicks.Choose(_run(), _theme, card.ChainId);
            _monitor.Log(
                chosen
                    ? $"Focus card picked for {_theme}: '{card.ChainId}'."
                    : $"Focus card '{card.ChainId}' for {_theme} was not recorded (the hand already has a pick).",
                chosen ? LogLevel.Info : LogLevel.Warn);
            Game1.playSound("coin");
            Advance();
        }

        /// <summary>Opens the next theme's pick, or closes so the planning hub can open.</summary>
        private void Advance()
        {
            RunState run = _run();
            FocusHand next = run != null ? FocusPicks.NextToPick(run) : null;
            if (next != null && next.Theme != _theme)
            {
                Game1.activeClickableMenu = new FocusPickMenu(_monitor, _run, _focus, next.Theme);
                return;
            }
            exitThisMenu(playSound: false);
        }

        // ---------- draw ----------

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
            drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            int y = yPositionOnScreen + Pad;
            Vector2 titleSize = Game1.dialogueFont.MeasureString(_title);
            Utility.drawTextWithShadow(b, _title, Game1.dialogueFont,
                new Vector2(xPositionOnScreen + (width - titleSize.X) / 2f, y), Game1.textColor);
            y += (int)titleSize.Y + TitleGap;
            Vector2 subtitleSize = Game1.smallFont.MeasureString(_subtitle);
            Utility.drawTextWithShadow(b, _subtitle, Game1.smallFont,
                new Vector2(xPositionOnScreen + (width - subtitleSize.X) / 2f, y), Game1.textColor);

            foreach (Card card in _cards)
                DrawCard(b, card, card == _hovered || Game1.options.snappyMenus && Game1.options.gamepadControls && card.Bounds == currentlySnappedComponent);

            drawMouse(b);
        }

        private static void DrawCard(SpriteBatch b, Card card, bool highlighted)
        {
            Rectangle r = card.Bounds.bounds;
            drawTextureBox(b, Game1.menuTexture, CardBoxSource, r.X, r.Y, r.Width, r.Height, Color.White, 1f, false);
            if (highlighted)
                b.Draw(Game1.staminaRect, new Rectangle(r.X + 8, r.Y + 8, r.Width - 16, r.Height - 16), Color.Wheat * HoverTint);

            int x = r.X + CardPad;
            int y = r.Y + CardPad;
            card.Icon?.drawInMenu(b, new Vector2(r.X + (r.Width - IconSize) / 2f, y), 1f);
            y += IconSize + LineGap;
            y = DrawLine(b, card.NameText, x, y, Game1.textColor);
            y = DrawLine(b, card.DescriptionText, x, y + LineGap, Game1.textColor);
            if (card.NoteText.Length > 0) y = DrawLine(b, card.NoteText, x, y + LineGap, Game1.textColor);
            if (card.Price.Length > 0) DrawLine(b, card.Price, x, y + LineGap, Game1.textColor);
        }

        private static int DrawLine(SpriteBatch b, string text, int x, int y, Color color)
        {
            if (text.Length == 0) return y;
            Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(x, y), color);
            return y + TextHeight(text);
        }
    }
}
