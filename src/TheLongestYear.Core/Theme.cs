namespace TheLongestYear.Core;

/// <summary>The eight weekly themes. The first five are room themes (goals from one Community
/// Center room); the last three are activity themes (goals matched by item kind anywhere on the
/// board, activity-themes spec 2026-08-28). Values are persisted in RunState, so new members are
/// only ever APPENDED.</summary>
public enum Theme
{
    Foraging,   // Crafts Room
    Farming,    // Pantry
    Fishing,    // Fish Tank
    Mining,     // Boiler Room
    Mixed,      // Bulletin Board room; goals from anything on the board
    Spelunking, // gems, minerals, monster loot, artifacts
    Artisan,    // artisan goods
    Kitchen     // cooked dishes, animal products
}
