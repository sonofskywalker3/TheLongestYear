namespace TheLongestYear.Core;

/// <summary>
/// Captured state of the player's pet for the <c>keep_pet</c> upgrade. Persisted in
/// <see cref="MetaState.PetState"/> so the pet survives loop resets — the pet kind, breed,
/// player-given name, and accumulated friendship are all restored on the post-reset Spring 1
/// so a long-tenured pet stays maxed out between runs.
///
/// 2026-05-29 spec: barn/coop animals intentionally do NOT use this pattern — they're
/// re-instantiated fresh (0 hearts) every reset by <c>WorldResetService.ApplyStartingAnimals</c>
/// per user direction "the 'keep 1 cow' should still start over with 0 hearts." Pets are
/// the exception because the upgrade is sentimental rather than progression-gating
/// (no Large Milk equivalent for friendship-leveling a pet).
/// </summary>
/// <param name="PetType">Vanilla pet-data key: "Cat", "Dog", "Turtle". Drives sprite + sounds.</param>
/// <param name="WhichBreed">String index into the pet's breeds list (vanilla uses 0/1/2/…).</param>
/// <param name="Name">Player-given name (e.g. "Mochi"). Restored verbatim.</param>
/// <param name="Friendship">Friendship value 0..1000 (200/heart, so 1000 = 5 hearts max).</param>
public sealed record PetSnapshot(
    string PetType,
    string WhichBreed,
    string Name,
    int Friendship);
