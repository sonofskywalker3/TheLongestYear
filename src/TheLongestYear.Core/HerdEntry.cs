namespace TheLongestYear.Core;

/// <summary>One animal registered in the Herd Book: everything needed to rebuild it as the same
/// animal after a rewind (spec 2026-09-25). Persisted in <see cref="MetaState.HerdBook"/>.
/// Daily state (fullness, petted today, today's produce) is deliberately not kept.
/// <see cref="AnimalId"/> is the animal's myID; the restore reuses it, which also keeps the
/// gender of a MaleOrFemale animal (vanilla derives it from myID % 2).</summary>
public sealed record HerdEntry(
    int SlotIndex,
    long AnimalId,
    string Type,
    string Name,
    string? SkinId,
    int Friendship,
    int Happiness,
    int Age,
    int DaysOwned,
    bool HasEatenAnimalCracker,
    bool AllowReproduction);

/// <summary>A live farm animal as the Core rules see it (the picker and the rewind offer).</summary>
public readonly record struct HerdAnimal(long Id, string Type, string Name, int Friendship);

/// <summary>Outcome of <see cref="HerdBookRules.Register"/>.</summary>
public enum HerdRegisterResult
{
    Registered,
    SlotNotOwned,
    WrongKind,
    AlreadyRegistered,
}
