using System;
using System.Collections.Generic;
using System.IO;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ModSaveDataCarryTests : IDisposable
{
    private const string OldName = "None_451054918";
    private const string NewName = "None_452543727";
    private static readonly SaveKeyRename Rename = new(OldName, "451054918", NewName, "452543727");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tly-carry-" + Guid.NewGuid().ToString("N"));

    public ModSaveDataCarryTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData("None_451054918.json", "None_452543727.json")]       // UI Info Suite 2: data/<SaveFolderName>.json
    [InlineData("451054918.json", "452543727.json")]                 // keyed on the id alone
    [InlineData("settings-451054918", "settings-452543727")]
    [InlineData("None_451054918", "None_452543727")]                 // a folder named for the save
    public void Names_keyed_on_the_old_save_move_to_the_new_key(string name, string expected)
    {
        Assert.Equal(expected, Rename.Rename(name));
    }

    [Theory]
    [InlineData("config.json")]
    [InlineData("1451054918.json")]                                  // a longer number is not the id
    [InlineData("4510549180.json")]
    [InlineData("Other_123456789.json")]                             // another save
    public void Other_names_are_left_alone(string name)
    {
        Assert.Null(Rename.Rename(name));
    }

    [Fact]
    public void A_non_canonical_old_folder_name_maps_to_the_canonical_new_one()
    {
        // The old folder can carry a de-dup prefix; the game writes the new save canonically.
        var rename = new SaveKeyRename("None2_451054918", "451054918", NewName, "452543727");
        Assert.Equal("None_452543727.json", rename.Rename("None2_451054918.json"));
        Assert.Equal("None_452543727.json", rename.Rename("None_451054918.json"));
    }

    [Fact]
    public void A_short_custom_seed_only_matches_after_an_underscore()
    {
        var rename = new SaveKeyRename(null, "42", null, "987654321");
        Assert.Null(rename.Rename("level42.json"));
        Assert.Null(rename.Rename("42.json"));
        Assert.Equal("None_987654321.json", rename.Rename("None_42.json"));
    }

    [Theory]
    [InlineData("None_451054918", true)]
    [InlineData("None_451054918_old", true)]
    [InlineData("SaveGameInfo", true)]
    [InlineData("SaveGameInfo_old", true)]
    [InlineData("None_451054918_STARDEWVALLEYSAVETMP", true)]
    [InlineData("None2_451054918", true)]
    [InlineData("None_451054918.json", false)]
    [InlineData("SomeMod.xml", false)]
    [InlineData("mod-data", false)]
    public void Vanilla_save_files_are_told_apart_from_mod_files(string name, bool vanilla)
    {
        Assert.Equal(vanilla, SaveKeyRename.IsVanillaSaveFile(name, "451054918"));
    }

    [Fact]
    public void Keyed_files_and_folders_in_mod_folders_are_copied_and_old_ones_deleted_later()
    {
        string uiis = Dir("Mods", "UIInfoSuite2", "data");
        File.WriteAllText(Path.Combine(uiis, OldName + ".json"), "{\"ShowLuck\":false}");
        File.WriteAllText(Path.Combine(uiis, "Other_123456789.json"), "other save");
        string perSaveDir = Dir("Mods", "SomeMod", "saves", OldName);
        File.WriteAllText(Path.Combine(perSaveDir, "state.json"), "state");
        string global = Dir("mod-data", "some.mod");
        File.WriteAllText(Path.Combine(global, "451054918.json"), "global");
        string own = Dir("Mods", "TheLongestYear", "backups");
        File.WriteAllText(Path.Combine(own, OldName + ".json"), "own backup");

        var result = new SaveDataCarryResult();
        ModSaveDataCopier.CarryKeyedEntries(
            new[] { P("Mods", "UIInfoSuite2"), P("Mods", "SomeMod"), P("Mods", "TheLongestYear"), P("mod-data") },
            Rename, new[] { P("Mods", "TheLongestYear") }, result);

        Assert.Empty(result.Errors);
        Assert.Equal("{\"ShowLuck\":false}", File.ReadAllText(Path.Combine(uiis, NewName + ".json")));
        Assert.Equal("state", File.ReadAllText(P("Mods", "SomeMod", "saves", NewName, "state.json")));
        Assert.Equal("global", File.ReadAllText(Path.Combine(global, "452543727.json")));
        Assert.False(File.Exists(Path.Combine(own, NewName + ".json")));
        Assert.Equal(3, result.CopiedSources.Count);

        // Old copies stay until the new save is confirmed, then go.
        Assert.True(File.Exists(Path.Combine(uiis, OldName + ".json")));
        var errors = new List<string>();
        Assert.Equal(3, ModSaveDataCopier.DeleteSources(result.CopiedSources, errors));
        Assert.Empty(errors);
        Assert.False(File.Exists(Path.Combine(uiis, OldName + ".json")));
        Assert.False(Directory.Exists(perSaveDir));
        Assert.True(File.Exists(Path.Combine(uiis, "Other_123456789.json")));
        Assert.True(File.Exists(Path.Combine(own, OldName + ".json")));
    }

    [Fact]
    public void An_existing_new_key_is_not_overwritten()
    {
        string data = Dir("Mods", "UIInfoSuite2", "data");
        File.WriteAllText(Path.Combine(data, OldName + ".json"), "old");
        File.WriteAllText(Path.Combine(data, NewName + ".json"), "already new");

        var result = new SaveDataCarryResult();
        ModSaveDataCopier.CarryKeyedEntries(new[] { P("Mods") }, Rename, Array.Empty<string>(), result);

        Assert.Equal("already new", File.ReadAllText(Path.Combine(data, NewName + ".json")));
        Assert.Empty(result.CopiedSources);
    }

    [Fact]
    public void Mod_files_in_the_old_save_folder_follow_the_save_and_vanilla_files_do_not()
    {
        string oldSave = Dir("Saves", OldName);
        File.WriteAllText(Path.Combine(oldSave, OldName), "<SaveGame/>");
        File.WriteAllText(Path.Combine(oldSave, OldName + "_old"), "<SaveGame/>");
        File.WriteAllText(Path.Combine(oldSave, "SaveGameInfo"), "<Farmer/>");
        File.WriteAllText(Path.Combine(oldSave, "SomeMod.json"), "mod");
        File.WriteAllText(Path.Combine(oldSave, OldName + "_extra.json"), "keyed");
        string sub = Dir("Saves", OldName, "modfolder");
        File.WriteAllText(Path.Combine(sub, "a.txt"), "a");
        string newSave = P("Saves", NewName);

        var result = new SaveDataCarryResult();
        ModSaveDataCopier.CarrySaveFolderExtras(oldSave, newSave, Rename, result);

        Assert.Empty(result.Errors);
        Assert.Equal("mod", File.ReadAllText(Path.Combine(newSave, "SomeMod.json")));
        Assert.Equal("keyed", File.ReadAllText(Path.Combine(newSave, NewName + "_extra.json")));
        Assert.Equal("a", File.ReadAllText(Path.Combine(newSave, "modfolder", "a.txt")));
        Assert.False(File.Exists(Path.Combine(newSave, NewName)));
        Assert.False(File.Exists(Path.Combine(newSave, OldName)));
        Assert.False(File.Exists(Path.Combine(newSave, "SaveGameInfo")));
        Assert.Empty(result.CopiedSources);   // they go with the old folder, not one by one
    }

    [Fact]
    public void Same_folder_for_old_and_new_save_copies_nothing()
    {
        string save = Dir("Saves", OldName);
        File.WriteAllText(Path.Combine(save, "SomeMod.json"), "mod");
        var result = new SaveDataCarryResult();
        ModSaveDataCopier.CarrySaveFolderExtras(save, save, Rename, result);
        Assert.Empty(result.Copies);
    }

    private string P(params string[] parts) => Path.Combine(_root, Path.Combine(parts));

    private string Dir(params string[] parts)
    {
        string path = P(parts);
        Directory.CreateDirectory(path);
        return path;
    }
}
