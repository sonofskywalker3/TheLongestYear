using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Pathfinding;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending's farm animals going home (Jeff, 2026-09-25: "send them whatever signal
    /// happens in the evening to make them all go inside"). Real FarmAnimals on the farm, each housed
    /// in one of the scene's coops or barns, driven by the game's own code:
    ///
    /// - Their update is vanilla's <c>FarmAnimal.updateWhenCurrentLocation</c> (FarmAnimal.cs 1771),
    ///   the same call GameLocation.UpdateWhenCurrentLocation makes each tick (GameLocation.cs 4109).
    ///   During an event it returns at once because <c>Game1.shouldTimePass()</c> is false while
    ///   <c>eventUp</c> (Game1.cs 8828), so this class makes that same call itself for the scene's
    ///   animals with shouldTimePass answering true for the duration of the call only (and, after 8pm,
    ///   the clock read as 7:50pm for the call only, or SleepIfNecessary, FarmAnimal.cs 1737, would
    ///   freeze them). Wandering, walking, the door check and the "dwoop" all come from that code.
    /// - The evening signal is the go-home branch of <c>FarmAnimal.behaviors</c> (FarmAnimal.cs
    ///   1548-1566): at 5pm or later, with the home's animal door open, the animal gets
    ///   <c>new PathFindController(animal, location, isAtEndPoint, 0, null, 200, animal door tile)</c>.
    ///   Vanilla rolls a 0.2% chance a tick for it; the scene gives it directly, animal by animal,
    ///   once the building is on screen, a little staggered.
    /// - The animal door opens the way the player opens it: <c>Building.ToggleAnimalDoor</c>
    ///   (Building.cs 899), with its creak; Building.Update slides it open (Building.cs 1345).
    /// - An animal walks in the vanilla way: at the door, updateWhenCurrentLocation moves it into the
    ///   building's interior (FarmAnimal.cs 1840-1856). Until its door opens, its noWarpTimer is held
    ///   up so a wandering animal cannot slip in early (the door rect lets its own animals through
    ///   even while closed, GameLocation.cs 2456).
    /// IN MEMORY ONLY, like the rest of the bad ending's farm.</summary>
    internal static class JojaFarmAnimals
    {
        private sealed class State
        {
            public Building Home;
            public bool Signalled, Sent;
            public float DueMs, IdleMs;
            public int Tries;
        }

        private static readonly Dictionary<long, State> Animals = new();
        private static readonly Queue<Building> DoorsToOpen = new();
        private static readonly HashSet<Building> DoorsQueued = new();
        private static bool _whenSeen;
        private static bool _forceTimePass;
        private static float _clockMs;
        private static Random _rng = new(Seed);

        private const int Seed = 925;
        private const int PathfindsPerTick = 1;          // FarmAnimal.MaxPathfindingPerTick
        private const int GoHomeLimit = 200;             // the limit vanilla's evening branch passes
        private const int RetryLimit = 1000;             // a wanderer too far for 200 gets a longer search on a retry
        private const float DoorLeadMs = 700f;           // the door is sliding open before anyone moves
        private const float SpreadMs = 2200f;            // not all at once, like a real evening
        private const float RetryIdleMs = 1500f;         // stopped short of the door: send again
        private const int MaxTries = 3;
        private const int HoldWarpMs = 1000;
        private const int SeenInsetTiles = 2;
        private const int SleepHour = 2000, AwakeHour = 1950;

        internal static int Count => Animals.Count;

        internal static void Add(FarmAnimal animal, Building home)
            => Animals[animal.myID.Value] = new State { Home = home };

        /// <summary>From now on, each building's animals are sent home once its animal door is on screen.</summary>
        internal static void ArmWhenSeen() => _whenSeen = true;

        /// <summary>The scene's animals still outside on the farm.</summary>
        internal static int Outside(Farm farm) => Animals.Keys.Count(id => farm.animals.ContainsKey(id));

        internal static void Reset()
        {
            Animals.Clear();
            DoorsToOpen.Clear();
            DoorsQueued.Clear();
            _whenSeen = false;
            _forceTimePass = false;
            _clockMs = 0f;
            _rng = new Random(Seed);
        }

        /// <summary>One tick of the scene's animals: signal what came into view, then run vanilla's
        /// own animal update for each of them.</summary>
        internal static void Tick(Farm farm)
        {
            if (Animals.Count == 0) return;
            float dt = Game1.currentGameTime.ElapsedGameTime.Milliseconds;
            _clockMs += dt;
            if (_whenSeen) SignalWhatIsSeen();
            OpenOneDoor();

            int pathfinds = 0;
            foreach (var (id, state) in Animals)
            {
                if (!farm.animals.TryGetValue(id, out FarmAnimal animal)) continue;   // inside already
                if (!state.Signalled)
                {
                    animal.noWarpTimer = Math.Max(animal.noWarpTimer, HoldWarpMs);
                    continue;
                }
                if (!state.Sent)
                {
                    if (_clockMs < state.DueMs || pathfinds >= PathfindsPerTick) continue;
                    SendHome(farm, animal, state.Home);
                    state.Sent = true;
                    state.Tries = 1;
                    pathfinds++;
                    continue;
                }
                if (animal.controller != null) { state.IdleMs = 0f; continue; }
                state.IdleMs += dt;
                if (state.IdleMs < RetryIdleMs || pathfinds >= PathfindsPerTick) continue;
                state.IdleMs = 0f;
                pathfinds++;
                if (state.Tries >= MaxTries) EnterNow(farm, animal, state.Home);
                else { SendHome(farm, animal, state.Home, RetryLimit); state.Tries++; }
            }

            int clock = Game1.timeOfDay;
            bool night = clock >= SleepHour;
            _forceTimePass = true;
            if (night) Game1.timeOfDay = AwakeHour;
            try
            {
                var view = new Rectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height);
                foreach (var pair in farm.animals.Pairs.ToList())
                {
                    if (!Animals.TryGetValue(pair.Key, out State state)) continue;
                    // An animal nobody can see and nobody has called home yet stands still: left to
                    // wander for the whole pan, some strayed 50 tiles from their own door (live
                    // 2026-09-25). Once on screen it wanders the vanilla way until its door opens.
                    if (!state.Signalled && (!_whenSeen || !pair.Value.GetBoundingBox().Intersects(view))) continue;
                    if (pair.Value.updateWhenCurrentLocation(Game1.currentGameTime, farm))
                        farm.animals.Remove(pair.Key);   // what GameLocation.UpdateWhenCurrentLocation does with a true
                }
            }
            finally
            {
                _forceTimePass = false;
                if (night) Game1.timeOfDay = clock;
            }
        }

        /// <summary>Time is up: whoever is still out goes in the way vanilla moves an animal home when
        /// no player is watching (FarmAnimal.cs 1550-1558). Returns how many.</summary>
        internal static List<string> SendStragglersInside(Farm farm)
        {
            var notes = new List<string>();
            foreach (var (id, state) in Animals)
            {
                if (!farm.animals.TryGetValue(id, out FarmAnimal animal)) continue;
                notes.Add($"{animal.type.Value} at {animal.TilePoint.X},{animal.TilePoint.Y} for door {state.Home.tileX.Value + state.Home.animalDoor.X},{state.Home.tileY.Value + state.Home.animalDoor.Y}"
                          + $" (signalled {state.Signalled}, sent {state.Sent}, tries {state.Tries}, walking {animal.controller != null})");
                EnterNow(farm, animal, state.Home);
            }
            return notes;
        }

        private static void SignalWhatIsSeen()
        {
            var view = new Rectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height);
            view.Inflate(-SeenInsetTiles * Game1.tileSize, -SeenInsetTiles * Game1.tileSize);
            foreach (State state in Animals.Values)
            {
                if (state.Signalled || !state.Home.getRectForAnimalDoor().Intersects(view)) continue;
                state.Signalled = true;
                state.DueMs = _clockMs + DoorLeadMs + (float)_rng.NextDouble() * SpreadMs;
                if (DoorsQueued.Add(state.Home)) DoorsToOpen.Enqueue(state.Home);
            }
        }

        /// <summary>One door a tick, so several doors in view creak one after another.</summary>
        private static void OpenOneDoor()
        {
            if (DoorsToOpen.Count == 0) return;
            Building door = DoorsToOpen.Dequeue();
            if (!door.animalDoorOpen.Value) door.ToggleAnimalDoor(Game1.player);
        }

        private static void SendHome(Farm farm, FarmAnimal animal, Building home, int limit = GoHomeLimit)
        {
            animal.noWarpTimer = 0;
            animal.controller = new PathFindController(animal, farm, PathFindController.isAtEndPoint, 0, null, limit,
                new Point(home.tileX.Value + home.animalDoor.X, home.tileY.Value + home.animalDoor.Y));
        }

        private static void EnterNow(Farm farm, FarmAnimal animal, Building home)
        {
            GameLocation inside = home.GetIndoors();
            if (inside == null) return;
            farm.animals.Remove(animal.myID.Value);
            inside.animals[animal.myID.Value] = animal;
            animal.setRandomPosition(inside);
            animal.faceDirection(Game1.random.Next(4));
            animal.controller = null;
        }

        /// <summary>shouldTimePass answers true only inside <see cref="Tick"/>'s own animal update.</summary>
        [HarmonyPatch(typeof(Game1), nameof(Game1.shouldTimePass))]
        internal static class LetSceneAnimalsMove
        {
            private static void Postfix(ref bool __result)
            {
                if (_forceTimePass) __result = true;
            }
        }
    }
}
