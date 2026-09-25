using System;
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

        /// <summary>Vanilla slot top is 16px above the sprite; a Herd Book row puts the sprite 2px below its top.</summary>
        private const int VanillaSlotTopFromRow = -14;
        private const int VanillaSpriteFromSlotTop = 16;

        private const int NoFriendship = -1;
        private const int NoPetState = -1;
        private const int HeartCount = 5;
        private const int TopLineY = 6;
        private const int TopLineX = 140;
        private const int TopLineGap = 24;
        private const int TypeBelowName = 44;
        private const float FadedAlpha = 0.8f;

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
        /// (the slot kind on book rows) and an optional note after it (the missing keep).</summary>
        public static void Draw(SpriteBatch b, Rectangle area, AnimalRow row, string topLabel, string note)
        {
            AnimalPage.AnimalEntry entry = row.Entry;
            // Vanilla coordinates: the page's xPositionOnScreen sits one border width left of the slot.
            int pageX = area.X - IClickableMenu.borderWidth;
            int slotTop = area.Y + VanillaSlotTopFromRow;
            bool small = entry.TextureSourceRect.Height <= SmallSpriteHeight;

            // AnimalPage.updateSlots: sprite 16px below the slot top, 48px lower still when short.
            ClickableTextureComponent sprite = row.Sprite;
            sprite.bounds.X = pageX + IClickableMenu.borderWidth + 4 + (small ? SmallSpriteShiftX : 0);
            sprite.bounds.Y = slotTop + VanillaSpriteFromSlotTop + (small ? SmallSpriteShiftY : 0);
            sprite.draw(b);

            // AnimalPage.drawNPCSlot from here down.
            float lineHeight = Game1.smallFont.MeasureString("W").Y;
            float russianOffsetY = (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ru
                || LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko) ? (0f - lineHeight) / 2f : 0f;
            int yOffset = small ? -40 : 8;
            float nameCenterX = pageX + IClickableMenu.borderWidth * 3 / 2 + 192 - 20 + 96;
            float nameY = sprite.bounds.Y + 48 + yOffset + russianOffsetY - 20f;
            b.DrawString(Game1.dialogueFont, entry.DisplayName,
                new Vector2(nameCenterX - (int)(Game1.dialogueFont.MeasureString(entry.DisplayName).X / 2f), nameY), Game1.textColor);

            if (entry.FriendshipLevel != NoFriendship)
            {
                double loveLevel = entry.FriendshipLevel / 1000f;
                int halfHeart = (int)((loveLevel * 1000.0 % 200.0 >= 100.0) ? (loveLevel * 1000.0 / 200.0) : (-100.0));
                int heartYOffset = entry.ReceivedAnimalCracker ? -24 : 0;
                for (int hearts = 0; hearts < HeartCount; hearts++)
                {
                    var pos = new Vector2(pageX + 512 - 4 + hearts * 32, sprite.bounds.Y + heartYOffset + yOffset + 64 - 24);
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
                b.Draw(Game1.mouseCursors, new Vector2(pageX + 704 - 4, sprite.bounds.Y + yOffset + 64 - 52),
                    new Rectangle(32, 0, 10, 10), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.8f);
                b.Draw(Game1.mouseCursors_1_6, new Vector2(pageX + 704 - 4, sprite.bounds.Y + yOffset + 64 - 8),
                    new Rectangle(273 + entry.WasPetYet * 9, 253, 9, 9), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.8f);
            }
            if (entry.ReceivedAnimalCracker)
            {
                Utility.drawWithShadow(b, Game1.objectSpriteSheet_2, new Vector2(pageX + 576 - 20, sprite.bounds.Y + yOffset + 64 - 16),
                    new Rectangle(16, 242, 15, 11), Color.White, 0f, Vector2.Zero, 4f, flipped: false, 0.8f);
            }

            // Herd Book extras: the type under the name, the slot kind and keep note on the top line.
            if (!string.IsNullOrEmpty(row.TypeName))
            {
                Vector2 typeSize = Game1.smallFont.MeasureString(row.TypeName);
                Utility.drawTextWithShadow(b, row.TypeName, Game1.smallFont,
                    new Vector2(nameCenterX - (int)(typeSize.X / 2f), nameY + TypeBelowName), Game1.textColor * FadedAlpha);
            }
            float topX = area.X + TopLineX;
            if (topLabel != null)
            {
                Utility.drawTextWithShadow(b, topLabel, Game1.smallFont, new Vector2(topX, area.Y + TopLineY), Game1.textColor * FadedAlpha);
                topX += Game1.smallFont.MeasureString(topLabel).X + TopLineGap;
            }
            if (note != null)
                Utility.drawTextWithShadow(b, note, Game1.smallFont, new Vector2(topX, area.Y + TopLineY), Game1.textColor * FadedAlpha);
        }
    }
}
