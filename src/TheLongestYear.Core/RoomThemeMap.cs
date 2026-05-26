namespace TheLongestYear.Core;

/// <summary>Maps a Community Center item-room name to its contract <see cref="Theme"/>.
/// Vault (gold) and Abandoned Joja Mart are not item rooms and are rejected.</summary>
public static class RoomThemeMap
{
    public static bool TryGetTheme(string room, out Theme theme)
    {
        switch ((room ?? "").Replace(" ", ""))
        {
            case "Pantry": theme = Theme.Farming; return true;
            case "CraftsRoom": theme = Theme.Foraging; return true;
            case "FishTank": theme = Theme.Fishing; return true;
            case "BoilerRoom": theme = Theme.Mining; return true;
            case "Bulletin":
            case "BulletinBoard": theme = Theme.Mixed; return true;
            default: theme = default; return false;
        }
    }
}
