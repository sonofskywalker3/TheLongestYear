using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>Read-only "what have I unlocked / what could I buy next reset" board. No purchasing —
    /// that stays on the reset/win shrine popup. Lets the player plan ahead from loop 1.</summary>
    internal sealed class ShrinePreviewMenu : IClickableMenu
    {
        private readonly List<string> _lines = new();

        public ShrinePreviewMenu(MetaState state)
            : base(Game1.uiViewport.Width / 2 - 420, Game1.uiViewport.Height / 2 - 320, 840, 640, showUpperRightCloseButton: true)
        {
            _lines.Add($"Junimo Points banked: {state.JunimoPoints}");
            _lines.Add("");

            foreach (UpgradeCategory cat in System.Enum.GetValues(typeof(UpgradeCategory)))
            {
                var owned = new List<string>();
                foreach (UpgradeDefinition def in UpgradeCatalog.ByCategory(cat))
                    if (state.HasUpgrade(def.Id))
                        owned.Add($"   [x] {def.DisplayName}");

                var buyable = new List<string>();
                foreach (UpgradeDefinition def in
                         KeepShopFilter.BuyableInCategory(cat, state, TheLongestYear.Integration.RunReachEvaluator.Meets))
                    buyable.Add($"   [ ] {def.DisplayName}  ({def.Cost} JP)");

                if (owned.Count == 0 && buyable.Count == 0)
                    continue;
                _lines.Add($"{cat}:");
                _lines.AddRange(owned);
                _lines.AddRange(buyable);
                _lines.Add("");
            }
        }

        public override void draw(Microsoft.Xna.Framework.Graphics.SpriteBatch b)
        {
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, speaker: false, drawOnlyBox: true);
            Utility.drawTextWithShadow(b, "Junimo Shrine - Planning", Game1.dialogueFont,
                new Vector2(xPositionOnScreen + 80, yPositionOnScreen + 96), Game1.textColor);

            int y = yPositionOnScreen + 150;
            foreach (string line in _lines)
            {
                Utility.drawTextWithShadow(b, line, Game1.smallFont,
                    new Vector2(xPositionOnScreen + 80, y), Game1.textColor);
                y += 28;
                if (y > yPositionOnScreen + height - 90)
                {
                    Utility.drawTextWithShadow(b, "   …more (see the reset shrine)", Game1.smallFont,
                        new Vector2(xPositionOnScreen + 80, y), Game1.textColor);
                    break;   // planning glance; full list + purchasing is the loop-boundary shrine
                }
            }

            base.draw(b);
            drawMouse(b);
        }
    }
}
