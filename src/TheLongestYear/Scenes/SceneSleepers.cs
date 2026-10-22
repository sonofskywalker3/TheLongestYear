using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Pathfinding;

namespace TheLongestYear.Scenes
{
    /// <summary>The household asleep while a strike scene plays in the farmhouse (spec 2026-09-21,
    /// the thief): the farmer in his bed, the spouse in hers, every child in its own bed or the
    /// crib. A scene set in the house shows the bed, and an empty bed with the farmer nowhere in it
    /// reads as a bug.
    ///
    /// THESE ARE THE REAL NPCs, not scene-drawn sprites, and that is the least invasive route here
    /// rather than the most. A sleeping villager is a pose the game itself strikes every night:
    /// <c>FarmHouse.performTenMinuteUpdate</c> walks the spouse to <c>getSpouseBedSpot</c> at 22:00
    /// and <c>spouseSleepEndFunction</c> calls <c>NPC.playSleepingAnimation</c>, and a toddler paths
    /// to <c>GetChildBedSpot</c> at 19:00. On the real overnight path they are therefore already
    /// where this class wants them and it does nothing at all. Drawing stand-ins over the top of the
    /// real ones would put two of everybody in the room.
    ///
    /// Everything touched is written down and written back by <see cref="Restore"/>, which the
    /// scene calls from its cleanup hook on every ending. The new day repositions the whole
    /// household seconds later anyway, so the restore is belt and braces, but a skipped or failed
    /// scene in the middle of an ordinary day has no new day coming.</summary>
    internal sealed class SceneSleepers
    {
        /// <summary>Facing up, which is how the game poses a character lying in a bed.</summary>
        private const int FacingUp = 0;

        /// <summary>The crib frame a crawler is put in at night, from
        /// <c>Child.setCrawlerInNewDirection</c> (Child.cs:556).</summary>
        private const int CrawlerInCribFrame = 7;

        /// <summary>Eyes shut. <c>Farmer.updateCommon</c> (Farmer.cs:7763) uses 1 for a farmer who
        /// is in bed.</summary>
        private const int EyesShut = 1;

        /// <summary>Everything the pose touches on one villager.
        ///
        /// <see cref="Controller"/> is the one that matters and it is the one that was missing: the
        /// pose clears it, and on the mid-day preview a spouse or child part way along a schedule
        /// route would have lost that route for good.
        ///
        /// The rest is what <c>NPC.Halt</c> clears (NPC.cs, over <c>Character.Halt</c>):
        /// <c>speed</c> to 2, <c>addedSpeed</c> to 0, <c>shouldPlaySpousePatioAnimation</c> to
        /// false, and the sprite's animation, which is already recorded. Its other three,
        /// <c>moveUp</c>/<c>moveDown</c>/<c>moveLeft</c>/<c>moveRight</c>, are <c>protected</c> on
        /// <c>Character</c> and cannot be read from here. They are not state worth keeping: they are
        /// this tick's "which way am I pressing", written afresh every tick by
        /// <c>PathFindController.update</c> from the controller that IS restored.
        /// <c>isPlayingSleepingAnimation</c> and <c>isCharging</c> are private too;
        /// <c>NPC.update</c> reconciles the first against <c>isSleeping</c> (NPC.cs:3144), which is
        /// recorded, and the second is blocked-path state that clears itself.</summary>
        private sealed class PosedNpc
        {
            public NPC Who;
            public Vector2 Position;
            public int Facing;
            public bool Sleeping;
            public Vector2 DrawOffset;
            public List<FarmerSprite.AnimationFrame> Animation;
            public int Frame;
            public bool Loop;
            public PathFindController Controller;
            public int Speed;
            public float AddedSpeed;
            public bool SpousePatioAnimation;
        }

        private readonly List<PosedNpc> _posed = new();
        private bool _posedFarmer;
        private Vector2 _farmerPosition;
        private int _farmerFacing;
        private int _farmerEyes;
        private int _farmerBlinkTimer;
        private bool _farmerInBed;
        private string _described = "nobody";

        /// <summary>What was actually posed, for the log.</summary>
        public string Describe() => _described;

        /// <summary>Put the household to bed for the length of the scene. Anything that cannot be
        /// posed is simply left alone: a missing bed or an unmarried farmer is not a reason to call
        /// a scene off.</summary>
        public void Pose(FarmHouse house, IMonitor monitor)
        {
            if (house == null) throw new ArgumentNullException(nameof(house));
            var done = new List<string>();
            try
            {
                if (PoseFarmer(house)) done.Add("the farmer");
                NPC spouse = FindSpouse(house);
                if (spouse != null && PoseNpc(spouse, house.getSpouseBedSpot(spouse.Name), null)) done.Add(spouse.Name);
                foreach (Child child in Children(house))
                    if (PoseChild(child, house)) done.Add(child.Name);
            }
            catch (Exception ex)
            {
                monitor?.Log($"Darkness: the thief scene could not put the household to bed, so it plays with them where they are. {ex}", LogLevel.Warn);
            }
            _described = done.Count == 0 ? "nobody" : string.Join(", ", done);
        }

        /// <summary>The farmer is already lying in his bed on the real overnight path, so this only
        /// has anything to do when the scene is being watched from the debug command in the middle
        /// of an ordinary day.</summary>
        private bool PoseFarmer(FarmHouse house)
        {
            Farmer who = Game1.player;
            if (who == null) return false;
            Point bed = house.GetPlayerBedSpot();
            if (bed.X <= 0 || bed.Y <= 0) return false;
            _farmerPosition = who.Position;
            _farmerFacing = who.FacingDirection;
            _farmerEyes = who.currentEyes;
            _farmerBlinkTimer = who.blinkTimer;
            _farmerInBed = who.isInBed.Value;
            _posedFarmer = true;
            who.Position = new Vector2(bed.X, bed.Y) * 64f;
            who.faceDirection(FacingUp);
            who.currentEyes = EyesShut;
            who.blinkTimer = -10;
            who.isInBed.Value = true;
            return true;
        }

        private static NPC FindSpouse(FarmHouse house)
        {
            // A roommate counts as the spouse here: Krobus sleeps in the house and the scene should
            // show him in his bed like anyone else.
            string name = Game1.player?.spouse;
            if (string.IsNullOrEmpty(name)) return null;
            foreach (NPC npc in house.characters)
                if (npc != null && npc.Name == name) return npc;
            return null;
        }

        private static IEnumerable<Child> Children(FarmHouse house)
        {
            foreach (NPC npc in house.characters)
                if (npc is Child child) yield return child;
        }

        private bool PoseChild(Child child, FarmHouse house)
        {
            // A toddler has a bed of its own. Anything younger is in the crib, and the crib is only
            // in the house once it has been upgraded far enough to have a child's room at all.
            if (child.Age >= 3)
                return PoseNpc(child, house.GetChildBedSpot(child.GetChildIndex()), 0);
            Rectangle? crib = house.GetCribBounds();
            if (!crib.HasValue) return false;
            var spot = new Point(crib.Value.X + 1, crib.Value.Y + 1);
            return PoseNpc(child, spot, child.Age == 2 ? CrawlerInCribFrame : (int?)null);
        }

        /// <summary>Lay one character in a bed. <paramref name="frame"/> is the sprite frame to
        /// hold, for the children, whose sheets have no sleeping animation to play.</summary>
        private bool PoseNpc(NPC who, Point spot, int? frame)
        {
            if (who?.Sprite == null) return false;
            // (-1000,-1000) is the game's own "there is no such spot", and Point.Zero is what
            // GetChildBedSpot returns when the child has no bed.
            if (spot.X <= 0 || spot.Y <= 0) return false;
            _posed.Add(new PosedNpc
            {
                Who = who,
                Position = who.Position,
                Facing = who.FacingDirection,
                Sleeping = who.isSleeping.Value,
                DrawOffset = who.drawOffset,
                Animation = who.Sprite.CurrentAnimation,
                Frame = who.Sprite.currentFrame,
                Loop = who.Sprite.loop,
                Controller = who.controller,
                Speed = who.speed,
                AddedSpeed = who.addedSpeed,
                SpousePatioAnimation = who.shouldPlaySpousePatioAnimation.Value,
            });
            who.controller = null;
            who.Halt();
            who.Position = new Vector2(spot.X, spot.Y) * 64f;
            who.faceDirection(FacingUp);
            if (frame.HasValue)
            {
                who.Sprite.CurrentAnimation = null;
                who.Sprite.currentFrame = frame.Value;
                who.Sprite.UpdateSourceRect();
            }
            else
            {
                who.playSleepingAnimation();
            }
            return true;
        }

        /// <summary>Put everybody back exactly as they were. Safe to call twice and safe to call
        /// when nothing was ever posed.</summary>
        public void Restore()
        {
            foreach (PosedNpc posed in _posed)
            {
                try
                {
                    posed.Who.Position = posed.Position;
                    posed.Who.faceDirection(posed.Facing);
                    posed.Who.isSleeping.Value = posed.Sleeping;
                    posed.Who.drawOffset = posed.DrawOffset;
                    posed.Who.Sprite.CurrentAnimation = posed.Animation;
                    posed.Who.Sprite.currentFrame = posed.Frame;
                    posed.Who.Sprite.loop = posed.Loop;
                    posed.Who.Sprite.UpdateSourceRect();
                    posed.Who.controller = posed.Controller;
                    posed.Who.speed = posed.Speed;
                    posed.Who.addedSpeed = posed.AddedSpeed;
                    posed.Who.shouldPlaySpousePatioAnimation.Value = posed.SpousePatioAnimation;
                }
                catch (Exception) { /* one villager left mid-pose is better than a held camera. */ }
            }
            _posed.Clear();
            if (!_posedFarmer) return;
            _posedFarmer = false;
            try
            {
                Farmer who = Game1.player;
                if (who == null) return;
                who.Position = _farmerPosition;
                who.faceDirection(_farmerFacing);
                who.currentEyes = _farmerEyes;
                who.blinkTimer = _farmerBlinkTimer;
                who.isInBed.Value = _farmerInBed;
            }
            catch (Exception) { /* same. */ }
        }
    }
}
