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
using TheLongestYear.Loop;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.UI
{
    /// <summary>
    /// The weekly planning hub: champion-offer cards (1 of 2). Each card shows the theme's
    /// bundles with "donated / X" progress + a per-season gate badge, and the per-week bonus-item
    /// preview the player would activate if they picked that card. Opened automatically on
    /// DayStarted at week-start; modal (no close until a theme is picked).
    ///
    /// Plan 06+ will add weather + cart foresight rows (currently hidden via config); the layout
    /// already reserves vertical space.
    /// </summary>
    internal sealed class ContractPickMenu : IClickableMenu
    {
        // ---------- Card dimensions ----------
        private const int CardWidth = 560;
        private const int CardHeight = 460;            // taller than v1 to fit the bundle list
        private const int CardSpacing = 32;
        private const int CardInnerPad = 28;
        private const int PanelPadding = 48;

        // ---------- Inner card layout ----------
        private const int ThemeNameLineHeight = 48;
        private const int BodyLineHeight = 28;
        private const int SectionGap = 12;

        // ---------- Bundle progress section ----------
        private const int BundleRowHeight = 28;

        // ---------- Bonus item icons ----------
        // Smaller than the v1 icon grid — bonus row needs to fit up to 7 icons in CardWidth-pad.
        private const float BonusIconScale = 0.75f;
        private const int BonusIconSize = 48;
        private const int BonusIconGap = 10;

        // ---------- Preview rows (foresight, Plan 06) ----------
        private const int PreviewRowHeight = 44;
        private const int PreviewSpacing = 8;
        private const int JunimoSpriteSize = 96;

        // ---------- Component IDs ----------
        private const int CardIdLeft = 5100;
        private const int CardIdRight = 5101;
        private const int WeatherIdBase = 5200;
        private const int CartIdBase = 5300;

        private readonly IMonitor _monitor;
        private readonly RunController _runController;
        private readonly GameplayConfig _config;
        private readonly RunState _run;
        private readonly IReadOnlyList<BundleRequirement> _requirements;

        /// <summary>Season the menu's bundle progress + bonus preview reflect. May be NEXT season
        /// (Sunday-night day-28 case) — see <see cref="_isPreChampionForNextMonth"/>.</summary>
        private readonly CoreSeason _offerSeason;

        /// <summary>True when this hub is for next-month's week 1 (player is pre-picking before
        /// sleeping on day 28). The pick routes through <see cref="RunController.PreChampionForNextMonth"/>
        /// rather than the normal current-week champion path.</summary>
        private readonly bool _isPreChampionForNextMonth;

        private IReadOnlyList<Theme> _offer;

        private ClickableComponent _leftCard;
        private ClickableComponent _rightCard;
        private readonly List<ClickableComponent> _weatherRows = new List<ClickableComponent>();
        private readonly List<ClickableComponent> _cartRows = new List<ClickableComponent>();

        private readonly Texture2D _junimoTexture;

        // Per-card derived data (recomputed on construct + refresh).
        private List<BundleRequirement> _leftBundles = new List<BundleRequirement>();
        private List<BundleRequirement> _rightBundles = new List<BundleRequirement>();
        private List<Item> _leftBonus = new List<Item>();
        private List<Item> _rightBonus = new List<Item>();
        private readonly List<Rectangle> _leftBonusBounds = new List<Rectangle>();
        private readonly List<Rectangle> _rightBonusBounds = new List<Rectangle>();

        private string _hoverText = "";
        private bool _themePicked = false;

        public ContractPickMenu(IMonitor monitor, RunController runController, GameplayConfig config,
            RunState run, IReadOnlyList<BundleRequirement> requirements, IReadOnlyList<Theme> offer,
            CoreSeason? offerSeason = null, bool isPreChampionForNextMonth = false)
            : base(0, 0, 0, 0, showUpperRightCloseButton: false)
        {
            _monitor = monitor;
            _runController = runController;
            _config = config;
            _run = run;
            _requirements = requirements ?? new List<BundleRequirement>();
            _offer = offer ?? new List<Theme>();
            _offerSeason = offerSeason ?? run.Season;
            _isPreChampionForNextMonth = isPreChampionForNextMonth;

            try { _junimoTexture = Game1.content.Load<Texture2D>("Characters\\Junimo"); }
            catch (Exception) { _junimoTexture = null; }

            ResolvePerCardData();
            RecomputeBoundsAndLayout();

            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                this.snapToDefaultClickableComponent();
        }

        // ---------- modal guard ----------

        public override bool readyToClose() => _themePicked;

        public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
        {
            if (!_themePicked && key == Microsoft.Xna.Framework.Input.Keys.Escape) return;
            base.receiveKeyPress(key);
        }

        public override void receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons b)
        {
            if (b == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                if (currentlySnappedComponent == _leftCard && _offer.Count > 0) { ConfirmChampion(_offer[0]); return; }
                if (currentlySnappedComponent == _rightCard && _offer.Count > 1) { ConfirmChampion(_offer[1]); return; }
            }
            if (b == Microsoft.Xna.Framework.Input.Buttons.B && !_themePicked) return;
            base.receiveGamePadButton(b);
        }

        // ---------- per-card data ----------

        /// <summary>Fill in _leftBundles/_rightBundles + bonus-item previews for the current offer.</summary>
        private void ResolvePerCardData()
        {
            ResolveBundlesForTheme(_offer.Count > 0 ? (Theme?)_offer[0] : null, _leftBundles);
            ResolveBundlesForTheme(_offer.Count > 1 ? (Theme?)_offer[1] : null, _rightBundles);

            ResolveBonusItemsForTheme(_offer.Count > 0 ? (Theme?)_offer[0] : null, _leftBonus);
            ResolveBonusItemsForTheme(_offer.Count > 1 ? (Theme?)_offer[1] : null, _rightBonus);
        }

        private void ResolveBundlesForTheme(Theme? theme, List<BundleRequirement> dest)
        {
            dest.Clear();
            if (theme == null) return;
            foreach (var b in _requirements)
                if (b.Theme == theme.Value)
                    dest.Add(b);
        }

        private void ResolveBonusItemsForTheme(Theme? theme, List<Item> dest)
        {
            dest.Clear();
            if (theme == null) return;

            int maxCount = _runController.BonusListSizeForCurrentSeason();
            // Sample for the OFFER's season (which is next-season on day 28's Sunday-night hub).
            int week = _isPreChampionForNextMonth ? _run.WeekOfYear + 1 : _run.WeekOfYear;
            IReadOnlyList<string> sample = BonusItemSampler.SampleForTheme(
                _run.Seed, week,
                theme.Value, _offerSeason,
                _requirements,
                id => _runController.IsObtainableInSeason(id, _offerSeason),
                maxCount);

            foreach (string id in sample)
            {
                Item item = null;
                try { item = ItemRegistry.Create(id, 1, 0, allowNull: true); }
                catch (Exception) { item = null; }
                dest.Add(item);
            }
        }

        // ---------- layout ----------

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeBoundsAndLayout();
        }

        private void RecomputeBoundsAndLayout()
        {
            int previewRows = _config.DefaultWeatherPreviewSlots + _config.DefaultCartPreviewSlots;
            int previewBlock = previewRows == 0
                ? 0
                : (previewRows * PreviewRowHeight) + ((previewRows - 1) * PreviewSpacing) + PanelPadding;

            int titleBlock = 24 + (_junimoTexture != null ? JunimoSpriteSize + 12 : 0) + 48 + 20;

            width = (CardWidth * 2) + CardSpacing + (PanelPadding * 2);
            height = titleBlock + CardHeight + previewBlock + PanelPadding;

            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int cardsY = yPositionOnScreen + titleBlock;
            int cardsLeftX = xPositionOnScreen + PanelPadding;
            int cardsRightX = cardsLeftX + CardWidth + CardSpacing;

            _leftCard = new ClickableComponent(new Rectangle(cardsLeftX, cardsY, CardWidth, CardHeight),
                _offer.Count > 0 ? _offer[0].ToString() : "left-card")
            {
                myID = CardIdLeft,
                rightNeighborID = CardIdRight,
                downNeighborID = FirstRowIdBelowCards()
            };
            _rightCard = new ClickableComponent(new Rectangle(cardsRightX, cardsY, CardWidth, CardHeight),
                _offer.Count > 1 ? _offer[1].ToString() : "right-card")
            {
                myID = CardIdRight,
                leftNeighborID = CardIdLeft,
                downNeighborID = FirstRowIdBelowCards()
            };

            _weatherRows.Clear();
            _cartRows.Clear();

            int rowX = xPositionOnScreen + PanelPadding;
            int rowWidth = width - (PanelPadding * 2);
            int rowY = cardsY + CardHeight + PanelPadding;

            for (int i = 0; i < _config.DefaultWeatherPreviewSlots; i++)
            {
                var row = new ClickableComponent(new Rectangle(rowX, rowY, rowWidth, PreviewRowHeight),
                    "weather-" + i)
                {
                    myID = WeatherIdBase + i,
                    upNeighborID = i == 0 ? CardIdLeft : (WeatherIdBase + i - 1),
                    downNeighborID = i == _config.DefaultWeatherPreviewSlots - 1
                        ? (_config.DefaultCartPreviewSlots > 0 ? CartIdBase : -1)
                        : (WeatherIdBase + i + 1)
                };
                _weatherRows.Add(row);
                rowY += PreviewRowHeight + PreviewSpacing;
            }
            for (int i = 0; i < _config.DefaultCartPreviewSlots; i++)
            {
                var row = new ClickableComponent(new Rectangle(rowX, rowY, rowWidth, PreviewRowHeight),
                    "cart-" + i)
                {
                    myID = CartIdBase + i,
                    upNeighborID = i == 0
                        ? (_config.DefaultWeatherPreviewSlots > 0
                            ? (WeatherIdBase + _config.DefaultWeatherPreviewSlots - 1)
                            : CardIdLeft)
                        : (CartIdBase + i - 1),
                    downNeighborID = i == _config.DefaultCartPreviewSlots - 1 ? -1 : (CartIdBase + i + 1)
                };
                _cartRows.Add(row);
                rowY += PreviewRowHeight + PreviewSpacing;
            }

            allClickableComponents = new List<ClickableComponent>();
            allClickableComponents.Add(_leftCard);
            allClickableComponents.Add(_rightCard);
            allClickableComponents.AddRange(_weatherRows);
            allClickableComponents.AddRange(_cartRows);

            ComputeBonusIconBounds(_leftCard, _leftBonus.Count, _leftBonusBounds);
            ComputeBonusIconBounds(_rightCard, _rightBonus.Count, _rightBonusBounds);
        }

        /// <summary>Bonus icon row sits at the bottom of the card, centred horizontally.</summary>
        private void ComputeBonusIconBounds(ClickableComponent card, int count, List<Rectangle> bounds)
        {
            bounds.Clear();
            if (card == null || count == 0) return;

            int totalWidth = count * BonusIconSize + (count - 1) * BonusIconGap;
            int startX = card.bounds.X + (card.bounds.Width - totalWidth) / 2;
            int y = card.bounds.Y + card.bounds.Height - CardInnerPad - BonusIconSize;
            for (int i = 0; i < count; i++)
                bounds.Add(new Rectangle(startX + i * (BonusIconSize + BonusIconGap), y, BonusIconSize, BonusIconSize));
        }

        private int FirstRowIdBelowCards()
        {
            if (_config.DefaultWeatherPreviewSlots > 0) return WeatherIdBase;
            if (_config.DefaultCartPreviewSlots > 0) return CartIdBase;
            return -1;
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _leftCard;
            this.snapCursorToCurrentSnappedComponent();
        }

        // ---------- input ----------

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hoverText = "";

            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                _hoverText = DescribeCard(_offer[0]);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                _hoverText = DescribeCard(_offer[1]);

            CheckBonusIconHover(x, y, _leftBonus, _leftBonusBounds);
            CheckBonusIconHover(x, y, _rightBonus, _rightBonusBounds);
        }

        private void CheckBonusIconHover(int x, int y, List<Item> items, List<Rectangle> bounds)
        {
            for (int i = 0; i < items.Count && i < bounds.Count; i++)
            {
                if (items[i] != null && bounds[i].Contains(x, y))
                {
                    _hoverText = $"{items[i].DisplayName} (1.5×)";
                    return;
                }
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                ConfirmChampion(_offer[0]);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                ConfirmChampion(_offer[1]);
        }

        private void ConfirmChampion(Theme theme)
        {
            _themePicked = true;
            if (_isPreChampionForNextMonth)
                _runController.PreChampionForNextMonth(theme);
            else
                _runController.ChampionByName(theme.ToString());
            Game1.playSound("smallSelect");
            this.exitThisMenu();
        }

        // ---------- drawing ----------

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            int panelCenterX = xPositionOnScreen + width / 2;
            int drawY = yPositionOnScreen + 24;

            if (_junimoTexture != null)
            {
                b.Draw(_junimoTexture,
                    new Rectangle(panelCenterX - JunimoSpriteSize / 2, drawY, JunimoSpriteSize, JunimoSpriteSize),
                    new Rectangle(0, 0, 16, 16), Color.White);
                drawY += JunimoSpriteSize + 12;
            }

            SpriteText.drawStringHorizontallyCenteredAt(b, "Pick a theme", panelCenterX, drawY);

            DrawCard(b, _leftCard, _offer.Count > 0 ? (Theme?)_offer[0] : null, _leftBundles, _leftBonus, _leftBonusBounds);
            DrawCard(b, _rightCard, _offer.Count > 1 ? (Theme?)_offer[1] : null, _rightBundles, _rightBonus, _rightBonusBounds);

            for (int i = 0; i < _weatherRows.Count; i++)
                DrawPreviewRow(b, _weatherRows[i], $"Weather, day {i + 1}: ???   (Weather Sage tier {i + 1} reveals this)");
            for (int i = 0; i < _cartRows.Count; i++)
                DrawPreviewRow(b, _cartRows[i], $"Cart slot {i + 1}: ???   (Cart Whisperer reveals this)");

            base.draw(b);

            if (!string.IsNullOrEmpty(_hoverText))
                IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);

            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawCard(SpriteBatch b, ClickableComponent card, Theme? theme,
            List<BundleRequirement> bundles, List<Item> bonus, List<Rectangle> bonusBounds)
        {
            if (card == null) return;

            Color tint = (currentlySnappedComponent == card) ? Color.White : Color.White * 0.9f;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                card.bounds.X, card.bounds.Y, card.bounds.Width, card.bounds.Height,
                tint, 1f, false);

            if (theme == null)
            {
                Utility.drawTextWithShadow(b, "(no offer)", Game1.smallFont,
                    new Vector2(card.bounds.X + 24, card.bounds.Y + 24), Game1.textColor);
                return;
            }

            var (bonusMod, liabilityMod) = ThemeModifiers.For(theme.Value);
            string bonusName = ThemeModifiers.DisplayNameFor(bonusMod);
            string liabilityName = ThemeModifiers.DisplayNameFor(liabilityMod);

            int textX = card.bounds.X + CardInnerPad;
            int textY = card.bounds.Y + CardInnerPad;

            // Theme name (big, centred).
            string themeName = theme.Value.ToString();
            Vector2 nameSize = Game1.dialogueFont.MeasureString(themeName);
            float nameX = card.bounds.X + (card.bounds.Width - nameSize.X) / 2f;
            Utility.drawTextWithShadow(b, themeName, Game1.dialogueFont,
                new Vector2(nameX, textY), Game1.textColor);
            textY += ThemeNameLineHeight;

            // Bonus + liability lines.
            Color bonusColor = new Color(34, 110, 34);
            Color liabilityColor = new Color(160, 34, 34);
            Utility.drawTextWithShadow(b, bonusName, Game1.smallFont,
                new Vector2(textX, textY), bonusColor);
            textY += BodyLineHeight;
            Utility.drawTextWithShadow(b, liabilityName, Game1.smallFont,
                new Vector2(textX, textY), liabilityColor);
            textY += BodyLineHeight + SectionGap;

            // Bundles header.
            Utility.drawTextWithShadow(b, "Bundles:", Game1.smallFont,
                new Vector2(textX, textY), Game1.textColor);
            textY += BodyLineHeight;

            // Bundle progress rows: "<name>  N/X  [season badge]".
            ISet<string> donated = _run.DonatedSet();
            foreach (var br in bundles)
            {
                int have = br.Ingredients.Count(donated.Contains);
                int need = br.NumberOfSlots;
                string badge = BundleSeasonBadge(br, donated);

                string line = $"  {br.Name}  {have}/{need}";
                Color rowColor = br.IsSatisfiedAtSeasonEnd(_offerSeason, donated)
                    ? Game1.textColor
                    : liabilityColor;
                Utility.drawTextWithShadow(b, line, Game1.smallFont,
                    new Vector2(textX, textY), rowColor);

                if (!string.IsNullOrEmpty(badge))
                {
                    // Badge sits to the right of the line, in the liability colour for "needs more"
                    // and dimmed for "satisfied later this year."
                    Vector2 lineSize = Game1.smallFont.MeasureString(line);
                    Utility.drawTextWithShadow(b, badge, Game1.smallFont,
                        new Vector2(textX + lineSize.X + 8, textY),
                        rowColor);
                }
                textY += BundleRowHeight;
            }

            // Bonus header above the icon row (which is anchored to the card bottom).
            int bonusHeaderY = card.bounds.Y + card.bounds.Height - CardInnerPad - BonusIconSize - BodyLineHeight - 4;
            Utility.drawTextWithShadow(b, "Bonus this week (1.5×):", Game1.smallFont,
                new Vector2(textX, bonusHeaderY), Game1.textColor);

            // Bonus item icons (pre-computed bounds).
            DrawBonusIcons(b, bonus, bonusBounds);
        }

        /// <summary>Status text for a bundle at <see cref="_offerSeason"/> — e.g. "needs 1 more
        /// for Spring" or empty if the gate is already passed at that season's checkpoint.</summary>
        private string BundleSeasonBadge(BundleRequirement br, ISet<string> donated)
        {
            switch (br.Kind)
            {
                case BundleKind.Seasonal:
                    if (br.SeasonalSeason == _offerSeason)
                    {
                        int missing = br.Ingredients.Count(i => !donated.Contains(i));
                        if (missing > 0) return $"  ← needs {missing} this month";
                    }
                    return "";

                case BundleKind.PerItem:
                    int dueMissing = br.ItemSeasonPins!.Count(kv =>
                        (int)kv.Value <= (int)_offerSeason && !donated.Contains(kv.Key));
                    if (dueMissing > 0) return $"  ← needs {dueMissing} this month";
                    return "";

                case BundleKind.Percentage:
                    int required = br.CumulativeRequiredBySeason![(int)_offerSeason];
                    int have = br.Ingredients.Count(donated.Contains);
                    int delta = required - have;
                    if (delta > 0) return $"  ← needs {delta} more by {_offerSeason}";
                    return "";

                default:
                    return "";
            }
        }

        private void DrawBonusIcons(SpriteBatch b, List<Item> items, List<Rectangle> bounds)
        {
            for (int i = 0; i < items.Count && i < bounds.Count; i++)
            {
                Item item = items[i];
                Rectangle slot = bounds[i];
                Vector2 pos = new Vector2(slot.X, slot.Y);
                if (item != null)
                {
                    item.drawInMenu(b, pos, BonusIconScale, 1f, 0.86f, StackDrawType.Hide, Color.White, false);
                }
                else
                {
                    // Unresolved id — draw "?" placeholder.
                    var qSrc = new Rectangle(403, 496, 5, 7);
                    const int qScale = 4;
                    int qW = qSrc.Width * qScale;
                    int qH = qSrc.Height * qScale;
                    b.Draw(Game1.mouseCursors,
                        new Rectangle(slot.X + (slot.Width - qW) / 2, slot.Y + (slot.Height - qH) / 2, qW, qH),
                        qSrc, Color.White);
                }
            }
        }

        private void DrawPreviewRow(SpriteBatch b, ClickableComponent row, string text)
        {
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                row.bounds.X, row.bounds.Y, row.bounds.Width, row.bounds.Height,
                Color.White * 0.7f, 1f, false);
            Utility.drawTextWithShadow(b, text, Game1.smallFont,
                new Vector2(row.bounds.X + 16, row.bounds.Y + 12), Game1.textColor);
        }

        private string DescribeCard(Theme theme)
        {
            var (bonus, liability) = ThemeModifiers.For(theme);
            string bonusName = ThemeModifiers.DisplayNameFor(bonus);
            string liabilityName = ThemeModifiers.DisplayNameFor(liability);
            return $"{theme}\nBonus: {bonusName}\nLiability: {liabilityName}";
        }
    }
}
