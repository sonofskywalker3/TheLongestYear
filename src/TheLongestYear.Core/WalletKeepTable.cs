using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

public enum WalletKeepKind { Mail, Event, Stardrop }

/// <summary>One wallet item, event-granted power, or Stardrop source the shrine can keep.</summary>
public sealed record WalletKeep(
    string UpgradeId, WalletKeepKind Kind, string Reach,
    IReadOnlyList<string> MailFlags, string? EventId, long Cost, string? PrerequisiteId);

/// <summary>
/// Single source of truth for the Keep wallet / Stardrop rows (spec 2026-08-27
/// keep-wallet-stardrops). Feeds the catalog generators (what is sold) and RunBaselineBuilder
/// (what is re-granted). Wallet items are Farmer.mailReceived flags (decompile Farmer.cs
/// 1278..1400); Bear's Knowledge and Spring Onion Mastery are Data/Powers SEEN_EVENT grants;
/// each Stardrop source marks itself claimed with a CF_* mail (Utility.cs 5834..5872), the
/// museum with "museumComplete". A kept Stardrop re-adds that marker so the source stays shut.
/// </summary>
public static class WalletKeepTable
{
    public const string WalletIdPrefix = "keep_wallet_";
    public const string StardropIdPrefix = "keep_stardrop_";
    public const string MailMetric = "mail";
    public const string EventMetric = "event";
    public const string StardropMinesMetric = "stardrop_mines";
    public const string BearEventId = "2120303";
    public const string SpringOnionEventId = "3910979";
    public const int BaseStamina = 270;
    public const int StardropStamina = 34;

    private const long Convenience = 150;
    private const long Yield = 350;
    private const long SkullKey = 750;
    private const long Stardrop = 500;

    private static string Mail(string flag) => $"{MailMetric}:{flag}";
    private static string Event(string id) => $"{EventMetric}:{id}";

    private static WalletKeep Wallet(string slug, string flag, long cost, string? prereqSlug = null, params string[] extraFlags)
    {
        var flags = new List<string> { flag };
        flags.AddRange(extraFlags);
        return new WalletKeep(WalletIdPrefix + slug, WalletKeepKind.Mail, Mail(flag), flags, null, cost,
            prereqSlug == null ? null : WalletIdPrefix + prereqSlug);
    }

    private static WalletKeep Power(string slug, string eventId) =>
        new(WalletIdPrefix + slug, WalletKeepKind.Event, Event(eventId), new List<string>(), eventId, Convenience, null);

    private static WalletKeep Drop(string source, string flag, string? reach = null) =>
        new(StardropIdPrefix + source, WalletKeepKind.Stardrop, reach ?? Mail(flag), new List<string> { flag }, null, Stardrop, null);

    public static IReadOnlyList<WalletKeep> Entries { get; } = new List<WalletKeep>
    {
        // Convenience.
        Wallet("dwarvish", "HasDwarvishTranslationGuide", Convenience),
        Wallet("magnifyingglass", "HasMagnifyingGlass", Convenience),
        Power("bearsknowledge", BearEventId),
        Power("springonion", SpringOnionEventId),
        // Yield.
        Wallet("specialcharm", "HasSpecialCharm", Yield),
        Wallet("rustykey", "HasRustyKey", Yield),
        Wallet("clubcard", "HasClubCard", Yield),
        Wallet("darktalisman", "HasDarkTalisman", Yield, "rustykey"),
        Wallet("magicink", "HasMagicInk", Yield, "darktalisman"),
        Wallet("townkey", "HasTownKey", Yield),
        // Power.
        Wallet("skullkey", "HasSkullKey", SkullKey, null, "HasUnlockedSkullDoor"),
        Drop("fair", "CF_Fair"),
        Drop("fish", "CF_Fish"),
        Drop("mines", "CF_Mines", StardropMinesMetric),   // vanilla accepts CF_Mines OR the level-100 chest
        Drop("sewer", "CF_Sewer"),
        Drop("spouse", "CF_Spouse"),
        Drop("statue", "CF_Statue"),
        Drop("museum", "museumComplete"),
    };

    public static WalletKeep? TryGet(string upgradeId) => Entries.FirstOrDefault(e => e.UpgradeId == upgradeId);
}
