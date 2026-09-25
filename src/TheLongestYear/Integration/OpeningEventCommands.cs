using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace TheLongestYear.Integration
{
    /// <summary>Event commands for the opening tour's hand-overs (Jeff, 2026-09-25 playthrough: the
    /// Junimo naming each book should hold it up, and the farmer should take it from him).
    ///
    /// <c>tlyHoldUp &lt;actor&gt; &lt;qualifiedItemId&gt;</c>: the item's sprite appears above the actor's
    /// head and rides along with him (a Junimo hops on his own now and then) until <c>tlyTakeHeld</c>
    /// removes it.
    ///
    /// <c>tlyTakeHeld &lt;qualifiedItemId&gt;</c>: the held sprite goes, and the farmer holds the item up
    /// with NO "you received" box (Jeff, 2026-09-25: the box interrupted the next line). Only one item is held at a time; a
    /// second tlyHoldUp replaces the first. Both are purely visual: the books themselves are granted by
    /// BookFurniture.ReconcileInventory, not by the event.
    ///
    /// Each command never throws: a failure is logged and the command is skipped.</summary>
    internal static class OpeningEventCommands
    {
        public const string HoldUpName = "tlyHoldUp";
        public const string TakeHeldName = "tlyTakeHeld";
        // Above a Junimo's head: its sprite is drawn about three quarters of a tile tall.
        private static readonly Vector2 AboveHead = new(0f, -56f);
        private const float HeldScale = 3f;

        private static TemporaryAnimatedSprite _held;
        private static GameLocation _heldIn;

        private static NPC _holder;

        public static void Register(IMonitor monitor, IModHelper helper)
        {
            helper.Events.GameLoop.UpdateTicked += (_, _) => FollowHolder();

            Event.RegisterCommand(HoldUpName, (evt, args, context) =>
            {
                try
                {
                    if (!ArgUtility.TryGet(args, 1, out string actorName, out string error)
                        || !ArgUtility.TryGet(args, 2, out string itemId, out error))
                    {
                        monitor.Log($"{HoldUpName}: {error}; skipping.", LogLevel.Warn);
                    }
                    else
                    {
                        NPC actor = evt.getActorByName(actorName, out _);
                        ParsedItemData data = ItemRegistry.GetDataOrErrorItem(itemId);
                        if (actor == null)
                            monitor.Log($"{HoldUpName}: no actor '{actorName}'; skipping.", LogLevel.Warn);
                        else
                        {
                            RemoveHeld();
                            Rectangle src = data.GetSourceRect();
                            // Centre the item over the actor's tile.
                            Vector2 at = actor.Position + AboveHead + new Vector2((64f - src.Width * HeldScale) / 2f, 0f);
                            _held = new TemporaryAnimatedSprite(data.TextureName, src, 999999f, 1, 0, at,
                                flicker: false, flipped: false, 1f, 0f, Color.White, HeldScale, 0f, 0f, 0f);
                            _heldIn = Game1.currentLocation;
                            _holder = actor;
                            _heldIn.temporarySprites.Add(_held);
                        }
                    }
                }
                catch (Exception ex)
                {
                    monitor.Log($"{HoldUpName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
                }
                evt.CurrentCommand++;
            });

            Event.RegisterCommand(TakeHeldName, (evt, args, context) =>
            {
                RemoveHeld();
                try
                {
                    if (ArgUtility.TryGet(args, 1, out string itemId, out _))
                        Game1.player.holdUpItemThenMessage(ItemRegistry.Create(itemId), showMessage: false);
                }
                catch (Exception ex)
                {
                    monitor.Log($"{TakeHeldName}: {ex.GetType().Name}: {ex.Message}; the farmer does not hold it up.", LogLevel.Warn);
                }
                evt.CurrentCommand++;
            });
        }

        private static void FollowHolder()
        {
            if (_held == null || _holder == null) return;
            Rectangle src = _held.sourceRect;
            _held.Position = _holder.Position + AboveHead
                + new Vector2((64f - src.Width * HeldScale) / 2f, _holder.yJumpOffset);
        }

        private static void RemoveHeld()
        {
            if (_held != null && _heldIn != null)
                _heldIn.temporarySprites.Remove(_held);
            _held = null;
            _heldIn = null;
            _holder = null;
        }
    }
}
