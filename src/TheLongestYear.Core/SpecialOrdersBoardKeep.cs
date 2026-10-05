namespace TheLongestYear.Core;

/// <summary>Keep Special Orders Board (Jeff, 2026-10-05; elaineofshalott asked on Nexus,
/// 2026-10-04): the town board outside Pierre's is open from Spring 1 of every loop with its
/// normal weekly orders. Vanilla opens it on Fall 2 (<c>SpecialOrder.IsSpecialOrdersBoardUnlocked</c>:
/// <c>Game1.stats.DaysPlayed &gt;= 58</c>), and the rewind puts DaysPlayed back to 1, so without the
/// keep the board closes again every loop. Flat price, not on the Gifts ladder.</summary>
public static class SpecialOrdersBoardKeep
{
    public const string UpgradeId = "keep_special_orders_board";
    public const long Cost = 1500;

    /// <summary>The DaysPlayed on which vanilla opens the board: Fall 2 of year 1.</summary>
    public const uint VanillaOpenDaysPlayed = 58;

    /// <summary>Bare-flag reach metric: met (1) once this run reached the day the vanilla board opens.</summary>
    public const string ReachMetric = "special_orders";

    /// <summary>1 once the run reached Fall 2, else 0. DaysPlayed is still the run's own count when
    /// the perk screen opens (the morning after the Fail night, before the rewind sets it to 1), so a
    /// run that failed at the end of Summer reads 57 and does not count; one that failed at the end
    /// of Fall reads 85 and does.</summary>
    public static int ReachValue(uint daysPlayed) => daysPlayed >= VanillaOpenDaysPlayed ? 1 : 0;

    /// <summary>The board is open when the keep is owned or vanilla's own day gate has passed.</summary>
    public static bool IsBoardOpen(bool ownsKeep, uint daysPlayed) => ownsKeep || ReachValue(daysPlayed) == 1;
}
