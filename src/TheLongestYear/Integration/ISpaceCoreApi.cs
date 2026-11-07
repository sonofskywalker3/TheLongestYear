using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// The slice of SpaceCore's public API the rewind uses (SpaceCore Api.cs, IApi). Declared
    /// locally so TLY builds and runs without SpaceCore; SMAPI maps it at runtime through
    /// Helper.ModRegistry.GetApi.
    /// </summary>
    public interface ISpaceCoreApi
    {
        string[] GetCustomSkills();

        int GetExperienceForCustomSkill(Farmer farmer, string skill);

        /// <summary>Adds without clamping, so a negative amount lowers the XP. It queues
        /// level-up menus only for a level gained, and adds mastery XP only while the skill is
        /// at 10 and the five vanilla skills sum to 50 or more.</summary>
        void AddExperienceForCustomSkill(Farmer farmer, string skill, int amt);

        /// <summary>The item in another mod's equipment slot, by the slot's global id; null if empty.</summary>
        Item GetItemInEquipmentSlot(Farmer farmer, string globalId);

        /// <summary>Puts an item in a slot, only if the slot's validator accepts it.</summary>
        void SetItemInEquipmentSlot(Farmer farmer, string globalId, Item item);

        /// <summary>Adds an equipment slot (debug only here: tly_modslot register).</summary>
        void RegisterEquipmentSlot(IManifest modManifest, string globalId, Func<Item, bool> slotValidator, Func<string> slotDisplayName, Texture2D bgTex, Rectangle? bgRect = null);
    }
}
