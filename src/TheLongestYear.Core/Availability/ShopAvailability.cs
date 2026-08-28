namespace TheLongestYear.Core.Availability;

/// <summary>Items whose first week is a fact about a shop or a reward board rather than a
/// spawn table: Pierre's staples and the Saloon's menu (week 1), Adventurer's Guild rewards (by
/// the monster's mine area) and Help Wanted rewards. Tables live in AvailabilityWeeks.</summary>
public static class ShopAvailability
{
    private const int StapleEffort = 1;
    private const int GuildEffort = 5;
    private const int QuestEffort = 3;

    public static ItemEffort? Derive(string qualifiedId)
    {
        if (qualifiedId == null) return null;
        if (AvailabilityWeeks.ShopStaples.TryGetValue(qualifiedId, out string? staple))
            return new ItemEffort(StapleEffort, $"shop, {staple}, week 1, effort {StapleEffort}", 1, Season.Spring);
        if (AvailabilityWeeks.GuildRewardWeeks.TryGetValue(qualifiedId, out (int Week, Season Gate, string Note) guild))
            return new ItemEffort(GuildEffort, $"guild reward, {guild.Note}, week {guild.Week}, effort {GuildEffort}", guild.Week, guild.Gate);
        if (AvailabilityWeeks.QuestRewardWeeks.TryGetValue(qualifiedId, out (int Week, string Note) quest))
            return new ItemEffort(QuestEffort, $"reward, {quest.Note}, week {quest.Week}, effort {QuestEffort}",
                quest.Week, AvailabilityWeeks.SeasonOf(quest.Week));
        return null;
    }
}
