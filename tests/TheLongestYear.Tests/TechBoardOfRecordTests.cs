using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>TLY is the board of record on every load when Tech's Cross-Mod Bundles is loaded
/// (spec 2026-10-08-custom-board-vanilla-only, addendum 3).</summary>
public class TechBoardOfRecordTests
{
    private sealed class FakeBoard : ILiveBundleBoard
    {
        public Dictionary<string, string> Live { get; } = new(StringComparer.Ordinal);
        public List<IReadOnlyDictionary<string, string>> Writes { get; } = new();

        // Like NetWorldState.BundleData: every value comes back resized to seven fields.
        public IReadOnlyDictionary<string, string> Read()
            => Live.ToDictionary(p => p.Key, p => AsGameReadsIt(p.Value, null));

        public void Write(IReadOnlyDictionary<string, string> updates)
        {
            Writes.Add(new Dictionary<string, string>(updates));
            foreach (var pair in updates)
                Live[pair.Key] = pair.Value; // SetBundleData merges, never removes
        }
    }

    private readonly List<string> _info = new();

    // An Engine board as WrittenBoard stores it (BundleDataWriter: seven fields).
    private static Dictionary<string, string> EngineBoard() => new(StringComparer.Ordinal)
    {
        ["Pantry/0"] = "Spring Crops/O 465 20/24 1 0 188 1 0 190 1 0 192 1 0/0/4//Spring Crops",
        ["Fish Tank/6"] = "River Fish/O 685 30/145 1 0 143 1 0 706 1 0/6/2//River Fish",
        ["Vault/23"] = "2,500g/O 220 3/-1 2500 2500/4/1//2,500g",
    };

    // Tech's raw board in the same vanilla key space.
    private static Dictionary<string, string> TechRawBoard() => new(StringComparer.Ordinal)
    {
        ["Pantry/0"] = "Spring Crops/O 465 20/FlashShifter.StardewValleyExpandedCP_Ancient_Fern_Seed 1 0 24 1 0/0/2",
        ["Fish Tank/6"] = "River Fish/O 685 30/Rafseazz.RSVCP_Fish 1 0 145 1 0/6/2",
        ["Vault/23"] = "2,500g/O 220 3/-1 2500 2500/4",
    };

    // A Normal/Remixed board as stored from live BundleData (display name in field 6), after
    // TLY's difficulty pass doubled a stack.
    private static Dictionary<string, string> VanillaPostPassBoard() => new(StringComparer.Ordinal)
    {
        ["Pantry/0"] = "Spring Crops/O 465 20/FlashShifter.StardewValleyExpandedCP_Ancient_Fern_Seed 2 0 24 2 0/0/2//Spring Crops",
        ["Fish Tank/6"] = "River Fish/O 685 30/Rafseazz.RSVCP_Fish 2 0 145 2 0/6/2//River Fish",
        ["Vault/23"] = "2,500g/O 220 3/-1 2500 2500/4///2,500g",
    };

    private int Restore(bool techLoaded, Dictionary<string, string>? stored, FakeBoard board, bool host = true)
        => TechBoardOfRecord.RestoreOnLoad(techLoaded, host, stored, board, _info.Add);

    // NetWorldState.UpdateBundleDisplayNames: resize to seven fields, field 6 = display name.
    private static string AsGameReadsIt(string value, string? displayName)
    {
        string[] fields = value.Split('/');
        if (fields.Length < 7)
            System.Array.Resize(ref fields, 7);
        fields[6] = displayName ?? fields[6] ?? fields[0];
        return string.Join("/", fields);
    }

    private static FakeBoard LiveWith(Dictionary<string, string> data)
    {
        var board = new FakeBoard();
        foreach (var pair in data)
            board.Live[pair.Key] = pair.Value;
        return board;
    }

    [Fact]
    public void Engine_board_overwritten_by_tech_is_restored_for_every_key()
    {
        var stored = EngineBoard();
        var board = LiveWith(TechRawBoard());

        Assert.Equal(3, Restore(true, stored, board));

        Assert.Single(board.Writes);
        Assert.True(EngineManifestCheck.MatchesIgnoringDisplayName(stored, board.Read()));
        Assert.Single(_info);
        Assert.StartsWith(TechBoardOfRecord.RestoredInfo, _info[0]);
    }

    [Fact]
    public void Normal_or_remixed_post_pass_board_is_restored()
    {
        var stored = VanillaPostPassBoard();
        var board = LiveWith(TechRawBoard());

        Assert.Equal(2, Restore(true, stored, board));
        Assert.True(EngineManifestCheck.MatchesIgnoringDisplayName(stored, board.Read()));
        Assert.Single(_info);
    }

    [Fact]
    public void Held_board_stored_at_reset_is_restored_the_same_way()
    {
        // A held (Fail-night kept) board is stored from the live board after the snapshot is written back.
        Dictionary<string, string>? stored = TechBoardOfRecord.VanillaBoardToStore(true, VanillaPostPassBoard());
        var board = LiveWith(TechRawBoard());

        Assert.Equal(2, Restore(true, stored, board));
        Assert.True(EngineManifestCheck.MatchesIgnoringDisplayName(VanillaPostPassBoard(), board.Read()));
    }

    [Fact]
    public void Matching_board_is_a_no_op()
    {
        var board = LiveWith(EngineBoard());
        Assert.Equal(0, Restore(true, EngineBoard(), board));
        Assert.Empty(board.Writes);
        Assert.Empty(_info);
    }

    [Fact]
    public void Display_name_field_alone_is_not_a_difference()
    {
        // The game recomputes field 6 on every BundleData read; only the fields TLY owns count.
        var live = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in EngineBoard())
            live[pair.Key] = AsGameReadsIt(pair.Value, "Localized Name");
        var board = LiveWith(live);

        Assert.Equal(0, Restore(true, EngineBoard(), board));
        Assert.Empty(board.Writes);
    }

    [Fact]
    public void Missing_live_key_is_written()
    {
        var live = EngineBoard();
        live.Remove("Vault/23");
        var board = LiveWith(live);

        Assert.Equal(1, Restore(true, EngineBoard(), board));
        Assert.Equal(EngineBoard()["Vault/23"], board.Live["Vault/23"]);
    }

    [Fact]
    public void Tech_not_loaded_is_a_no_op_even_when_the_board_differs()
    {
        var board = LiveWith(TechRawBoard());
        Assert.Equal(0, Restore(false, EngineBoard(), board));
        Assert.Empty(board.Writes);
        Assert.Empty(_info);
    }

    [Fact]
    public void No_stored_board_is_a_no_op()
    {
        var board = LiveWith(TechRawBoard());
        Assert.Equal(0, Restore(true, null, board));
        Assert.Equal(0, Restore(true, new Dictionary<string, string>(), board));
        Assert.Empty(board.Writes);
    }

    [Fact]
    public void Farmhand_never_writes()
    {
        var board = LiveWith(TechRawBoard());
        Assert.Equal(0, Restore(true, EngineBoard(), board, host: false));
        Assert.Empty(board.Writes);
    }

    [Fact]
    public void Vanilla_board_is_stored_only_with_tech_loaded()
    {
        Assert.Null(TechBoardOfRecord.VanillaBoardToStore(false, VanillaPostPassBoard()));
        Assert.Null(TechBoardOfRecord.VanillaBoardToStore(true, null));
        Assert.Null(TechBoardOfRecord.VanillaBoardToStore(true, new Dictionary<string, string>()));

        var source = VanillaPostPassBoard();
        Dictionary<string, string>? stored = TechBoardOfRecord.VanillaBoardToStore(true, source);
        Assert.NotNull(stored);
        Assert.Equal(source, stored);
        source["Pantry/0"] = "changed";
        Assert.NotEqual("changed", stored!["Pantry/0"]); // a copy, not the live dictionary
    }

    [Fact]
    public void Owned_fields_ignore_display_name_and_field_count()
    {
        // Tech writes five fields; the game resizes every live value to seven on read.
        Assert.Equal(TechBoardOfRecord.OwnedFields("a/b/c/1/2"), TechBoardOfRecord.OwnedFields("a/b/c/1/2//Name"));
        Assert.NotEqual(TechBoardOfRecord.OwnedFields("a/b/c/1/2"), TechBoardOfRecord.OwnedFields("a/b/c/1/3//Name"));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Load_runs_late_only_with_tech_loaded(bool techLoaded, bool expectedLate)
        => Assert.Equal(expectedLate, TechBoardOfRecord.RunLoadLate(techLoaded));
}
