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
    }
}
