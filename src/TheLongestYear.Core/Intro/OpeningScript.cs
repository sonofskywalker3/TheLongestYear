using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Intro;

/// <summary>The arrival event (spec 2026-09-16-expanded-opening-design.md, sections 1 and 2.3): one
/// script under vanilla's own key that carries itself from the bus stop through the farmhouse, the
/// Community Center and back to the porch, and ends the way vanilla's arrival ends (end beginGame:
/// Event.cs 4637 puts the farmer to bed and starts Spring 1). Pure: <paramref name="text"/> supplies
/// every spoken line already sanitised for the script (no '"' or '/').</summary>
public static class OpeningScript
{
    public const string VanillaKey = "60367/u 0";
    /// <summary>The event id the game gives this script (the key's id part).</summary>
    public const string EventId = "60367";
    public static readonly string[] LocationOrder = { "BusStop", "Farm", "CommunityCenter", "Farm" };
    private const string Off = "-100 -100";
    private const string Prefix = "event.opening.";

    /// <summary>Every line key the script speaks, in order.</summary>
    public static readonly string[] LineKeys =
    {
        Prefix + "robin-1", Prefix + "robin-2", Prefix + "morris-bus-1", Prefix + "robin-3",
        Prefix + "robin-walk-1", Prefix + "robin-walk-2",
        Prefix + "lewis-1", Prefix + "morris-farm-1", Prefix + "morris-farm-2", Prefix + "morris-farm-3",
        Prefix + "lewis-2", Prefix + "morris-farm-4", Prefix + "morris-farm-5", Prefix + "lewis-3",
        Prefix + "lewis-hall-1", Prefix + "lewis-hall-2",
        Prefix + "junimo-1", Prefix + "junimo-2", Prefix + "junimo-3", Prefix + "junimo-4",
        Prefix + "junimo-5", Prefix + "junimo-6", Prefix + "junimo-7", Prefix + "junimo-8",
        Prefix + "tour-1", Prefix + "tour-2", Prefix + "tour-3", Prefix + "tour-4", Prefix + "tour-5", Prefix + "tour-6",
        Prefix + "tour-herd", Prefix + "tour-7", Prefix + "tour-8", Prefix + "tour-9",
    };

    public static string Build(Func<string, string> text, string ccSeenMail)
    {
        string Say(string who, string key) => $"speak {who} \"{text(Prefix + key)}\"";
        // The lead Junimo holds a book up as he names it (no scripted hop while he holds it); the
        // farmer then takes it and holds it up. The 3000 ms pause after each is the hold-up pose
        // (2500 + 500 ms), so the next line never opens over it.
        // Visual only: the books are granted by the mod's inventory reconcile, not by the event.
        string HoldUp(string bookId) => $"tlyHoldUp Junimo0 (F){bookId}";
        string HandOver(string bookId) => $"tlyTakeHeld (F){bookId}/playSound coin";

        var s = new List<string>
        {
            // ---- Bus stop (vanilla 60367 opening, Morris added) ----
            "none",
            "-1000 -1000",
            "farmer 22 10 2 Robin 22 13 0 Lewis -100 -100 2",
            "pause 500",
            "playSound busDoorOpen",
            "pause 5000",
            "viewport 23 10 clamp true",
            "move farmer 0 2 2",
            "playMusic SettlingIn",
            Say("Robin", "robin-1"),
            "pause 300",
            Say("Robin", "robin-2"),
            "pause 400",
            "playSound busDoorOpen",
            "addTemporaryActor Morris 16 32 22 8 2 true Character",
            "move Morris 0 1 2",
            "pause 400",
            "faceDirection Robin 0",
            Say("Morris", "morris-bus-1"),
            "pause 300",
            "faceDirection Robin 2",
            Say("Robin", "robin-3"),
            "pause 400",
            "viewport move 0 2 800",
            "move Robin 0 5 2 true",
            "pause 800",
            "move farmer 0 4 2 true",
            "fade",
            "speed farmer 2",
            "viewport -200 -200",

            // ---- Farm: the walk (vanilla tiles, Morris one tile behind) ----
            "changeLocation Farm",
            "halt",
            "warp Robin 78 17",
            "faceDirection Robin 3",
            "warp farmer 79 17",
            "faceDirection farmer 3",
            "warp Morris 81 17",
            "faceDirection Morris 3",
            "viewport 70 16 clamp",
            "viewport move -1 0 4000",
            "move Robin -8 0 3 farmer -8 0 3 Morris -8 0 3",
            "pause 700",
            "faceDirection Robin 2",
            Say("Robin", "robin-walk-1"),
            "pause 500",
            "move Robin -7 0 0 farmer -7 0 0 Morris -7 0 0",
            "pause 400",
            "faceDirection Robin 0",
            Say("Robin", "robin-walk-2"),
            "pause 300",
            "faceDirection farmer 0",
            "pause 500",

            // ---- Farmhouse door: Lewis, then Morris's business ----
            "playSound doorClose",
            "warp Lewis 64 15",
            "pause 1500",
            "move Lewis 0 1 2",
            "move Lewis 1 0 2",
            "move Lewis 0 1 3",
            // Everyone turns to Lewis at once. He ends up between the farmer and Morris, so Morris
            // turns left to him while the farmer and Robin turn right (Jeff, 2026-09-25).
            "faceDirection farmer 1",
            "faceDirection Robin 1",
            "faceDirection Morris 3",
            "pause 600",
            Say("Lewis", "lewis-1"),
            "pause 400",
            "faceDirection Lewis 1",
            "faceDirection Morris 3",
            Say("Morris", "morris-farm-1"),
            "pause 200",
            Say("Morris", "morris-farm-2"),
            "playSound shwip",
            "pause 500",
            Say("Morris", "morris-farm-3"),
            "pause 600",
            "jump Lewis",
            Say("Lewis", "lewis-2"),
            "pause 500",
            Say("Morris", "morris-farm-4"),
            "pause 400",
            "faceDirection Morris 2",
            "faceDirection farmer 1",
            Say("Morris", "morris-farm-5"),
            "pause 400",
            // Morris leaves while the scene carries on: two or three steps, then Lewis turns
            // back (Jeff, 2026-09-25: nobody waits for him to clear the screen). He is taken off
            // with everyone else by the scene change to the hall, which also drops his walk.
            "move Morris 12 0 1 true",
            "pause 1200",
            "faceDirection farmer 1",
            "faceDirection Lewis 3",
            "pause 400",
            "emote farmer 8",
            "pause 1200",
            Say("Lewis", "lewis-3"),
            "pause 600",

            // ---- Community Center: Lewis's piece, then the Junimos ----
            // The mod's own scene change: everyone on the farm fades out together (vanilla's
            // changeLocation left Morris on the black screen, and Robin was taken off before the
            // fade, Jeff 2026-09-25). It clears every actor and any walk still running, so Lewis
            // is added again in the hall.
            "tlyChangeLocation CommunityCenter 32 16",
            "warp farmer 32 16 true",
            "addTemporaryActor Lewis 16 32 30 16 1 true Character",
            "faceDirection farmer 3",
            "viewport 32 14 clamp",
            "tlyFadeIn",
            "pause 500",
            Say("Lewis", "lewis-hall-1"),
            "playSound coin",
            "pause 300",
            Say("Lewis", "lewis-hall-2"),
            "pause 600",
            // Lewis walks out and the farmer turns to watch him go (Jeff, 2026-09-25). Jeff's route:
            // down one, right two, then down the open aisle to the door. Straight down from his
            // spot walks him through the wall. One advancedMove (relative legs) so he walks it
            // without stopping between legs; separate moves paused at each corner.
            "advancedMove Lewis false 0 1 2 0 0 6",
            "pause 300",
            "faceDirection farmer 2",
            // Nine tiles at walking speed is about 4.8 s; cap the wait so a walk that stops short
            // of the door can never freeze the scene.
            "tlyWaitWalk Lewis 5500",
            "playSound doorClose",
            $"warp Lewis {Off}",
            "pause 700",
            // Tiles checked against a gridded screenshot of the unrepaired hall (2026-09-25): all
            // six stand on clear planks; 35,14 was inside the rubble patch and 32,14 read as
            // sitting on the farmer's head.
            // One Junimo pops up behind the farmer, who turns with a "!". Then the rest pop in,
            // each in a puff of its own colour, and the farmer does the surprised emote (the
            // emote menu's: frame 94, a jump, the bat screech) before anyone speaks.
            "tlyJunimo Junimo0 32 13 0 pop",
            "pause 500",
            "faceDirection farmer 0",
            "emote farmer 16",
            "pause 700",
            "tlyJunimo Junimo1 29 14 1 pop",
            "pause 250",
            "tlyJunimo Junimo2 35 12 2 pop",
            "pause 250",
            "tlyJunimo Junimo3 30 12 3 pop",
            "pause 250",
            "tlyJunimo Junimo4 34 11 4 pop",
            "pause 250",
            "tlyJunimo Junimo5 32 11 5 pop",
            "pause 600",
            "playSound batScreech",
            "showFrame farmer 94",
            "jump farmer 4",
            "emote farmer 16",
            "pause 1500",
            "faceDirection farmer 0",
            "pause 300",
            // The lines pass between them; the speaker hops first so the eye finds him.
            "jump Junimo0 6",
            Say("Junimo0", "junimo-1"),
            "pause 200",
            "jump Junimo1 6",
            Say("Junimo1", "junimo-2"),
            "pause 200",
            "jump Junimo2 6",
            Say("Junimo2", "junimo-3"),
            "pause 300",
            "jump Junimo0 6",
            Say("Junimo0", "junimo-4"),
            "pause 300",
            "jump Junimo3 6",
            Say("Junimo3", "junimo-5"),
            "pause 300",
            "jump Junimo4 6",
            Say("Junimo4", "junimo-6"),
            "pause 300",
            "jump Junimo5 6",
            Say("Junimo5", "junimo-7"),
            "pause 600",
            "jump Junimo0 6",
            Say("Junimo0", "junimo-8"),
            "pause 600",
            "playSound junimoMeep1",

            // ---- Farm again: the tour on the porch (Standard-farm tiles, offset per farm type) ----
            // The mod's own scene change (EndingEventCommands): fades the whole screen with the
            // hall still drawn, clears the hall's actors and loads the farm under black. Vanilla's
            // changeLocation left Junimo sprites showing on the black screen (Jeff, 2026-09-25).
            // It clears every actor, so the two tour Junimos are added again below.
            "tlyChangeLocation Farm 66 18",
            // Place the stash chest and planning shrine now, before the tour speaks about them
            // (finding 1, 2026-09-16 review): OnSaveCreating fires too late on a brand-new game
            // to guarantee this, so the script places them itself via this custom command
            // (ModEntry.Entry registers "tlyPlaceGifts", mirroring EndingEventCommands's pattern).
            "tlyPlaceGifts",
            "warp farmer 66 18 true",
            "faceDirection farmer 1",
            // Two of the hall's six come along: the lead at the farmer's side, the next by the
            // shrine. tlyJunimo places on raw tiles; the vanilla warp after it applies the farm
            // type's tile offset.
            "tlyJunimo Junimo0 67 18 0",
            "warp Junimo0 67 18",
            "tlyJunimo Junimo1 62 18 1",
            "warp Junimo1 62 18",
            // "clamp" keeps the camera inside the farm; without it the view ran past the right
            // edge and drew a black bar (Jeff, 2026-09-25).
            "viewport 66 18 clamp",
            "tlyFadeIn",
            "pause 500",
            "playSound junimoMeep1",
            "jump Junimo0",
            Say("Junimo0", "tour-1"),
            "pause 400",
            "jump Junimo0",
            Say("Junimo0", "tour-2"),
            "pause 300",
            "faceDirection farmer 3",
            "jump Junimo1",
            Say("Junimo1", "tour-3"),
            "pause 300",
            "faceDirection farmer 1",
            HoldUp(Interactables.BookKit.CookbookId),
            Say("Junimo0", "tour-4"),
            HandOver(Interactables.BookKit.CookbookId),
            "pause 3000",
            "faceDirection farmer 1",   // back to the Junimo once the hold-up ends (Jeff, 2026-09-25)
            HoldUp(Interactables.BookKit.CraftbookId),
            Say("Junimo0", "tour-5"),
            HandOver(Interactables.BookKit.CraftbookId),
            "pause 3000",
            "faceDirection farmer 1",   // back to the Junimo once the hold-up ends (Jeff, 2026-09-25)
            HoldUp(Interactables.BookKit.BundleLogId),
            Say("Junimo0", "tour-6"),
            HandOver(Interactables.BookKit.BundleLogId),
            "pause 3000",
            "faceDirection farmer 1",   // back to the Junimo once the hold-up ends (Jeff, 2026-09-25)
            HoldUp(Interactables.BookKit.HerdBookId),
            Say("Junimo0", "tour-herd"),
            HandOver(Interactables.BookKit.HerdBookId),
            "pause 3000",
            "faceDirection farmer 1",   // back to the Junimo once the hold-up ends (Jeff, 2026-09-25)
            Say("Junimo0", "tour-7"),
            "pause 200",
            Say("Junimo0", "tour-8"),
            "pause 400",
            Say("Junimo0", "tour-9"),
            "pause 600",
            "playSound junimoMeep1",
            "pause 800",
            "globalFade",
            $"viewport {Off}",
            "playMusic none",
            "pause 1500",
            "playSound rooster",
            "pause 800",
            $"addMailReceived {ccSeenMail}",
            "end beginGame",
        };
        return string.Join("/", s);
    }
}
