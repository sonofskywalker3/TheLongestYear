namespace TheLongestYear.Core.Availability;

/// <summary>Mine areas as the game numbers them (MineShaft.getMineArea): 0 and 10 are floors 1
/// to 39, 40 is 41 to 79, 80 is 81 to 119, 121 is the Skull Cavern. Effort per area is the scale
/// MetalsAvailability already uses (copper 1, iron 3, gold 5, iridium 7).</summary>
public static class MineAreas
{
    public const int Area0 = 0;
    public const int Area10 = 10;
    public const int Area40 = 40;
    public const int Area80 = 80;
    public const int SkullCavern = 121;

    private const int ShallowEffort = 1;
    private const int MidEffort = 3;
    private const int DeepEffort = 5;
    private const int SkullEffort = 7;

    public static int Effort(int area) => area switch
    {
        Area0 or Area10 => ShallowEffort,
        Area40 => MidEffort,
        Area80 => DeepEffort,
        _ => SkullEffort,
    };

    public static string Label(int area) => area switch
    {
        Area0 or Area10 => "mine floors 1 to 39",
        Area40 => "mine floors 41 to 79",
        Area80 => "mine floors 81 to 119",
        _ => "Skull Cavern",
    };
}
