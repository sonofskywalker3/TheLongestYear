using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.FarmAnimals;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>One Herd Book row's animal, resolved once: the vanilla
    /// <see cref="AnimalPage.AnimalEntry"/> (sprite crop, name, hearts, cracker, petted) plus the
    /// animal's type name. <see cref="Entry"/> is null when no sprite could be built; the row is
    /// then drawn as text.</summary>
    internal sealed class AnimalRow
    {
        public AnimalPage.AnimalEntry Entry { get; init; }
        public string TypeName { get; init; }

        /// <summary>False for a stored entry: petted-today is daily state the book does not keep.</summary>
        public bool ShowPetted { get; init; }

        /// <summary>The sprite as vanilla builds it in AnimalPage.CreateSpriteComponent; bounds move per draw.</summary>
        public ClickableTextureComponent Sprite { get; init; }
    }

    /// <summary>Draws a Herd Book row exactly like the game's Animals tab row
    /// (AnimalPage.drawNPCSlot and CreateSpriteComponent, PC 1.6): same source rectangles, scale 4
    /// and offsets, anchored to the row instead of the page. Used by picker rows and filled slots.</summary>
    internal static class AnimalRowRenderer
    {
        private const float SpriteScale = 4f;
        private const int SmallSpriteHeight = 16;
        private const int SmallSpriteShiftX = 24;
        private const int SmallSpriteShiftY = 48;
        private const int SpriteSlotHeight = 64;

        /// <summary>The tallest vanilla-drawn element: AnimalEntry always crops a large (non-chicken/
        /// duck) sprite to a 28px-tall source rect (see AnimalPage.AnimalEntry), scaled 4x. Everything
        /// else in the group (name, hearts, pet icons, cracker) lands inside this span, so centering
        /// against it centers the whole group.</summary>
        private const int VanillaContentHeight = 112;

        /// <summary>Extra room left of a large sprite (cow, pig, etc.) so its widest frame does not
        /// touch the row's left border; small sprites already get <see cref="SmallSpriteShiftX"/>.</summary>
        private const int LargeSpriteLeftPadding = 8;

        /// <summary>HerdBookMenu's row content width before RowGrowth (900px panel minus its padding
        /// and scrollbar gutter). The vanilla column offsets below (name block, hearts start) were
        /// tuned against this width; widening the row spreads them by the same ratio instead of
        /// leaving them clumped on the left.</summary>
        private const int BaseRowWidth = 784;

        /// <summary>Vanilla drawNPCSlot's name-block X offset (192 - 20 + 96) from the sprite column,
        /// scaled by the row's width growth over <see cref="BaseRowWidth"/>.</summary>
        private const int NameBaseOffset = 268;

        /// <summary>Vanilla drawNPCSlot's hearts-start X offset (512) from the sprite column, scaled
        /// the same way as <see cref="NameBaseOffset"/>.</summary>
        private const int HeartsBaseOffset = 512;

        /// <summary>Gap kept from the row's right edge to the pet icon/checkbox column, so it tracks
        /// the border directly instead of scaling with row width (matches its vanilla distance from
        /// the border at <see cref="BaseRowWidth"/>).</summary>
        private const int PetIconRightPadding = 124;

        /// <summary>Vanilla drawNPCSlot's gap between the cracker badge and the pet icon column
        /// ((704 - 4) - (576 - 20)).</summary>
        private const int CrackerToPetIconGap = 144;

        private const int NoFriendship = -1;
        private const int NoPetState = -1;
        private const int HeartCount = 5;
        private const int TopLineY = 14;
        private const int TopLineX = 140;
        private const int TopLineGap = 24;
        private const int TypeBelowName = 44;
        private const float FadedAlpha = 0.8f;

        // Vanilla drawNPCSlot icon sizes, in screen pixels (source size x 4).
        private const int HeartStep = 32;
        private const int HeartHeight = 24;
        private const int PetIconWidth = 40;
        private const int PetIconsHeight = 80;   // hand icon (40) plus the petted/not-petted mark below it
        private const int CrackerWidth = 60;
        private const int CrackerHeight = 44;

        /// <summary>Room kept between the note and anything else drawn in the row (covers the text shadow).</summary>
        private const int NotePadding = 8;
        /// <summary>Gap between the note's bottom line and the row's bottom edge.</summary>
        private const int NoteBottomInset = 6;
        /// <summary>Gap between the note and the row's right edge.</summary>
        private const int NoteRightInset = 16;

        /// <summary>A live animal, straight through the vanilla entry. Text-only row when that fails.</summary>
        public static AnimalRow ForLive(FarmAnimal animal, IMonitor monitor)
        {
            try
            {
                var entry = new AnimalPage.AnimalEntry(animal);
                return new AnimalRow { Entry = entry, TypeName = animal.displayType, ShowPetted = true, Sprite = CreateSprite(entry) };
            }
            catch (Exception ex) when (ex is ContentLoadException or InvalidOperationException or ArgumentException or NullReferenceException)
            {
                monitor.LogOnce($"HerdBookMenu: could not build a row for '{animal.Name}' ({animal.type.Value}); drawn as text. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                return new AnimalRow { TypeName = animal.type.Value };
            }
        }

        /// <summary>A registered animal that is not on the farm: a temporary adult <see cref="FarmAnimal"/>
        /// of the entry's type and skin supplies the sprite. It is never added to any location.
        /// Returns a text-only row (null <see cref="AnimalRow.Entry"/>) when that fails.</summary>
        public static AnimalRow ForStored(HerdEntry stored, IMonitor monitor)
        {
            try
            {
                var animal = new FarmAnimal(stored.Type, stored.AnimalId, Game1.player.UniqueMultiplayerID);
                FarmAnimalData data = animal.GetAnimalData();
                if (data == null)
                {
                    monitor.LogOnce($"HerdBookMenu: no Data/FarmAnimals entry for '{stored.Type}'; '{stored.Name}' is drawn as text.", LogLevel.Warn);
                    return new AnimalRow { TypeName = stored.Type };
                }
                animal.Name = stored.Name;
                animal.displayName = stored.Name;
                animal.skinID.Value = stored.SkinId;
                animal.age.Value = HerdBookRules.AdultAge(stored.Age, data.DaysToMature);
                animal.friendshipTowardFarmer.Value = HerdBookRules.ClampFriendship(stored.Friendship);
                animal.hasEatenAnimalCracker.Value = stored.HasEatenAnimalCracker;
                // As HerdBookService.Rebuild: an adult sheep has its wool, so it draws unsheared. A
                // local Random seeded from the id keeps this draw path off Game1.random.
                if (data.ProduceOnMature)
                    animal.currentProduce.Value = animal.GetProduceID(new Random(stored.AnimalId.GetHashCode()));
                animal.ReloadTextureIfNeeded();
                var entry = new AnimalPage.AnimalEntry(animal);
                return new AnimalRow { Entry = entry, TypeName = animal.displayType, ShowPetted = false, Sprite = CreateSprite(entry) };
            }
            catch (Exception ex) when (ex is ContentLoadException or InvalidOperationException or ArgumentException or NullReferenceException)
            {
                monitor.LogOnce($"HerdBookMenu: could not build a sprite for '{stored.Name}' ({stored.Type}); drawn as text. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                return new AnimalRow { TypeName = stored.Type };
            }
        }

        /// <summary>Vanilla CreateSpriteComponent: a 16px-tall sprite gets one pixel less height and moves 24px right.</summary>
        private static ClickableTextureComponent CreateSprite(AnimalPage.AnimalEntry entry)
        {
            var bounds = new Rectangle(0, 0, 0, SpriteSlotHeight);
            if (entry.TextureSourceRect.Height <= SmallSpriteHeight)
                bounds.Height--;
            return new ClickableTextureComponent("herd-sprite", bounds, null, "", entry.Texture, entry.TextureSourceRect, SpriteScale);
        }

        /// <summary>Draw <paramref name="row"/> into <paramref name="area"/>, with an optional faded top line
        /// (the slot kind on book rows) and an optional note (the missing keep), placed where it
        /// overlaps nothing else in the row.</summary>
        public static void Draw(SpriteBatch b, Rectangle area, AnimalRow row, string topLabel, string note)
        {
            AnimalPage.AnimalEntry entry = row.Entry;
            // Vanilla coordinates: the page's xPositionOnScreen sits one border width left of the slot.
            int pageX = area.X - IClickableMenu.borderWidth;
            bool small = entry.TextureSourceRect.Height <= SmallSpriteHeight;

            // Center the vanilla group vertically in the row: VanillaContentHeight is the tallest
            // element (the large sprite), so padding it evenly top/bottom centers everything anchored
            // to the sprite, including the small-sprite rows (which are shorter and get extra headroom).
            int contentTop = area.Y + (area.Height - VanillaContentHeight) / 2;
            // Spread the row proportionally to how much wider it grew than the width the vanilla
            // column offsets (hearts start, name block) were tuned against, so they don't clump on
            // the left of a wider row. The pet icon/checkbox and cracker badge anchor to the row's
            // right edge instead, at a fixed padding, so they track the border directly.
            float widthGrowth = Math.Max(1f, area.Width / (float)BaseRowWidth);
            int petIconX = area.Right - PetIconRightPadding;
            int crackerX = petIconX - CrackerToPetIconGap;

            // AnimalPage.updateSlots: sprite 16px below the slot top, 48px lower still when short.
            ClickableTextureComponent sprite = row.Sprite;
            sprite.bounds.X = pageX + IClickableMenu.borderWidth + 4 + (small ? SmallSpriteShiftX : LargeSpriteLeftPadding);
            sprite.bounds.Y = contentTop + (small ? SmallSpriteShiftY : 0);
            sprite.draw(b);
            // Everything drawn in the row, so the keep note can be placed clear of all of it.
            var drawn = new List<PixelBox>
            {
                new PixelBox(sprite.bounds.X, sprite.bounds.Y,
                    (int)(entry.TextureSourceRect.Width * SpriteScale), (int)(entry.TextureSourceRect.Height * SpriteScale)),
            };

            // AnimalPage.drawNPCSlot from here down.
            float lineHeight = Game1.smallFont.MeasureString("W").Y;
            float russianOffsetY = (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ru
                || LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko) ? (0f - lineHeight) / 2f : 0f;
            int yOffset = small ? -40 : 8;
            float nameCenterX = pageX + IClickableMenu.borderWidth * 3 / 2 + NameBaseOffset * widthGrowth;
            float nameY = sprite.bounds.Y + 48 + yOffset + russianOffsetY - 20f;
            Vector2 nameSize = Game1.dialogueFont.MeasureString(entry.DisplayName);
            b.DrawString(Game1.dialogueFont, entry.DisplayName,
                new Vector2(nameCenterX - (int)(nameSize.X / 2f), nameY), Game1.textColor);
            drawn.Add(Box(nameCenterX - (int)(nameSize.X / 2f), nameY, nameSize));

            if (entry.FriendshipLevel != NoFriendship)
            {
                double loveLevel = entry.FriendshipLevel / 1000f;
                int halfHeart = (int)((loveLevel * 1000.0 % 200.0 >= 100.0) ? (loveLevel * 1000.0 / 200.0) : (-100.0));
                int heartYOffset = entry.ReceivedAnimalCracker ? -24 : 0;
                int heartsX = pageX + (int)(HeartsBaseOffset * widthGrowth) - 4;
                drawn.Add(new PixelBox(heartsX, sprite.bounds.Y + heartYOffset + yOffset + 64 - 24, HeartCount * HeartStep, HeartHeight));
                for (int hearts = 0; hearts < HeartCount; hearts++)
                {
                    var pos = new Vector2(heartsX + hearts * 32, sprite.bounds.Y + heartYOffset + yOffset + 64 - 24);
                    b.Draw(Game1.mouseCursors, pos,
                        new Rectangle(211 + ((loveLevel * 1000.0 <= (hearts + 1) * 195) ? 7 : 0), 428, 7, 6),
                        Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
                    if (halfHeart == hearts)
                        b.Draw(Game1.mouseCursors, pos, new Rectangle(211, 428, 4, 6),
                            Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.891f);
                }
            }
            if (row.ShowPetted && entry.WasPetYet != NoPetState)
            {
                drawn.Add(new PixelBox(petIconX, sprite.bounds.Y + yOffset + 64 - 52, PetIconWidth, PetIconsHeight));
                b.Draw(Game1.mouseCursors, new Vector2(petIconX, sprite.bounds.Y + yOffset + 64 - 52),
                    new Rectangle(32, 0, 10, 10), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.8f);
                b.Draw(Game1.mouseCursors_1_6, new Vector2(petIconX, sprite.bounds.Y + yOffset + 64 - 8),
                    new Rectangle(273 + entry.WasPetYet * 9, 253, 9, 9), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.8f);
            }
            if (entry.ReceivedAnimalCracker)
            {
                drawn.Add(new PixelBox(crackerX, sprite.bounds.Y + yOffset + 64 - 16, CrackerWidth, CrackerHeight));
                Utility.drawWithShadow(b, Game1.objectSpriteSheet_2, new Vector2(crackerX, sprite.bounds.Y + yOffset + 64 - 16),
                    new Rectangle(16, 242, 15, 11), Color.White, 0f, Vector2.Zero, 4f, flipped: false, 0.8f);
            }

            // Herd Book extras: the type under the name, the slot kind on the top line, and the keep
            // note wherever the row has room for it.
            if (!string.IsNullOrEmpty(row.TypeName))
            {
                Vector2 typeSize = Game1.smallFont.MeasureString(row.TypeName);
                var typePos = new Vector2(nameCenterX - (int)(typeSize.X / 2f), nameY + TypeBelowName);
                Utility.drawTextWithShadow(b, row.TypeName, Game1.smallFont, typePos, Game1.textColor * FadedAlpha);
                drawn.Add(Box(typePos.X, typePos.Y, typeSize));
            }
            if (topLabel != null)
            {
                var labelPos = new Vector2(area.X + TopLineX, area.Y + TopLineY);
                Utility.drawTextWithShadow(b, topLabel, Game1.smallFont, labelPos, Game1.textColor * FadedAlpha);
                // Widened so a note that follows it on the same line keeps the old gap.
                PixelBox label = Box(labelPos.X, labelPos.Y, Game1.smallFont.MeasureString(topLabel));
                drawn.Add(label with { Width = label.Width + TopLineGap - NotePadding });
            }
            if (note != null)
                DrawNote(b, area, note, drawn);
        }

        /// <summary>The keep note, placed clear of everything in <paramref name="drawn"/>: on the top
        /// line after the slot kind when it fits there (the hearts and pet icon rise into that line
        /// on some rows), else right-aligned on the row's bottom line, else shrunk into the widest
        /// free stretch of either line.</summary>
        private static void DrawNote(SpriteBatch b, Rectangle area, string note, List<PixelBox> drawn)
        {
            Vector2 size = Game1.smallFont.MeasureString(note);
            int noteW = (int)Math.Ceiling(size.X);
            int noteH = (int)Math.Ceiling(size.Y);
            int xMin = area.X + TopLineX;
            int xMax = area.Right - NoteRightInset;

            int topY = area.Y + TopLineY;
            List<FreeSpan> topSpans = RowTextFit.FreeSpans(drawn, topY, topY + noteH, xMin, xMax, NotePadding);
            FreeSpan? top = RowTextFit.FirstFitting(topSpans, noteW);
            if (top != null)
            {
                DrawFaded(b, note, top.Value.Start, topY, 1f);
                return;
            }

            int bottomY = area.Bottom - NoteBottomInset - noteH;
            List<FreeSpan> bottomSpans = RowTextFit.FreeSpans(drawn, bottomY, bottomY + noteH, xMin, xMax, NotePadding);
            FreeSpan? bottom = RowTextFit.LastFitting(bottomSpans, noteW);
            if (bottom != null)
            {
                DrawFaded(b, note, bottom.Value.End - noteW, bottomY, 1f);
                return;
            }

            // Neither line has room at full size: shrink it into the widest gap. Checked at full
            // height, so the smaller text stays clear too.
            FreeSpan? widestTop = RowTextFit.Widest(topSpans);
            FreeSpan? widestBottom = RowTextFit.Widest(bottomSpans);
            bool useTop = widestTop != null && (widestBottom == null || widestTop.Value.Width >= widestBottom.Value.Width);
            FreeSpan? gap = useTop ? widestTop : widestBottom;
            if (gap == null) return;
            float scale = gap.Value.Width / (float)noteW;
            DrawFaded(b, note, gap.Value.Start, useTop ? topY : bottomY + (int)(noteH * (1f - scale)), scale);
        }

        private static void DrawFaded(SpriteBatch b, string text, float x, float y, float scale)
            => Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(x, y), Game1.textColor * FadedAlpha, scale);

        private static PixelBox Box(float x, float y, Vector2 size)
            => new PixelBox((int)x, (int)y, (int)Math.Ceiling(size.X), (int)Math.Ceiling(size.Y));
    }
}
