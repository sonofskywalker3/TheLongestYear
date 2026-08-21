using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BoardInspectionTests
{
    private static Dictionary<string, string> Board(params (string key, string ingredients)[] bundles)
    {
        var d = new Dictionary<string, string>();
        foreach (var (key, ingredients) in bundles)
            d[key] = $"Name/O 465 1/{ingredients}/0/2/0/Name";
        return d;
    }

    [Fact]
    public void Empty_board_has_no_non_object_ingredients()
    {
        Assert.False(BoardInspection.HasNonObjectIngredients(new Dictionary<string, string>()));
    }

    [Fact]
    public void Object_only_board_is_false_for_bare_and_qualified_ids()
    {
        var board = Board(("Pantry/0", "24 1 0 (O)188 5 2"), ("Boiler Room/22", "(O)520 1 0 (O)522 1 0"));
        Assert.False(BoardInspection.HasNonObjectIngredients(board));
    }

    [Fact]
    public void Weapon_slot_is_detected()
    {
        var board = Board(("Pantry/0", "24 1 0"), ("Boiler Room/22", "(O)520 1 0 (W)13 1 0 (O)522 1 0"));
        Assert.True(BoardInspection.HasNonObjectIngredients(board));
    }

    [Fact]
    public void Hat_slot_is_detected()
    {
        var board = Board(("Boiler Room/22", "(H)8 1 0 (O)526 1 0"));
        Assert.True(BoardInspection.HasNonObjectIngredients(board));
    }

    [Fact]
    public void Category_refs_are_ignored()
    {
        // "-5" = any animal product; a category ref is not a non-Object item slot.
        var board = Board(("Pantry/4", "-5 1 0 -6 1 0"));
        Assert.False(BoardInspection.HasNonObjectIngredients(board));
    }

    [Fact]
    public void Gold_vault_bundles_with_empty_ingredients_are_ignored()
    {
        var board = new Dictionary<string, string> { ["Vault/23"] = "3,125g/O 24 1//4/1//3,125g" };
        Assert.False(BoardInspection.HasNonObjectIngredients(board));
    }
}
