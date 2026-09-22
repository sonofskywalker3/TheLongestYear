using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace TheLongestYear.Scenes
{
    /// <summary>A character an overnight strike scene draws ITSELF, from a character sheet, with no
    /// NPC behind it (spec 2026-09-21). Linus in the crows scene, and the same way for the thief and
    /// the hall.
    ///
    /// WHY NOT THE REAL NPC. Moving a real villager at night fights two things at once: his schedule
    /// (the game re-places every NPC at the day's start from the schedule, so anything the scene did
    /// to him is either undone or, worse, kept) and the new-day pass that runs immediately after the
    /// scene. A scene-drawn actor is a texture and a frame number. It owns nothing, it is gone when
    /// the scene ends, and it cannot leave a villager standing in a field at 6am.
    ///
    /// FRAMES, and why they are FRAME INDEXES and not sheet rows. Every character in the game
    /// animates through <c>Character.animateInFacingDirection</c> (Character.cs:1597), which calls
    /// <c>AnimatedSprite.AnimateUp/Right/Down/Left</c>. Those walk fixed blocks of
    /// <c>framesPerAnimation</c> frames, which is 4 for everything except a Big Slime
    /// (AnimatedSprite.cs:31 and 351 to 441): frames 0 to 3 face down, 4 to 7 right, 8 to 11 up,
    /// 12 to 15 left, and frame 0 of a block is the standing pose. That is a FRAME INDEX rule, not a
    /// row rule, and <c>AnimatedSprite.GetSourceRect</c> turns an index into a rectangle using the
    /// sheet's own width, so it holds at any width.
    ///
    /// <c>AnimatedSprite.faceDirection</c> does compute a row from <c>textureWidth / SpriteWidth</c>
    /// instead, and it maps the directions differently again. It is NOT the one to copy: the only
    /// callers are Horse, Pet, Bug and the profile menu, whose sheets put one direction per row.
    /// Characters use <c>faceDirectionStandard</c> (direction times 4), which agrees with the block
    /// rule above.
    ///
    /// The two happen to agree anyway for every sheet this mod draws, because they are all four
    /// columns wide: verified by dumping them out of the running game, Linus 64x224, Shadow Brute
    /// 64x256, Shane 64x416, all 16 px columns. So a monster sheet is laid out exactly like a
    /// villager's here, and only the sprite HEIGHT differs (Linus and Shane 32, Shadow Brute 24).</summary>
    internal sealed class SceneActor
    {
        public const int FacingDown = 0;
        public const int FacingRight = 1;
        public const int FacingUp = 2;
        public const int FacingLeft = 3;

        private const int FramesPerDirection = 4;
        private const int TileSize = 64;
        private const float DrawScale = 4f;

        /// <summary>How long one walking frame is held. Vanilla's default sprite interval is 175 ms and
        /// a scene walker reads better a little quicker.</summary>
        private const int StepMs = 150;

        private readonly AnimatedSprite _sprite;
        private readonly int _spriteHeight;

        /// <summary>The top-left corner of the tile the actor is standing on, in world pixels. It is
        /// a float so a walk between two tiles can be drawn mid-step.</summary>
        public Vector2 Position;

        /// <summary>One of the four Facing constants.</summary>
        public int Facing = FacingDown;

        /// <summary>Is he taking steps right now? A standing actor holds his block's first frame.</summary>
        public bool Walking;

        /// <summary>Shoved sideways in screen pixels, for a shudder that is not a step.</summary>
        public float Shake;

        /// <summary>Lifted off the ground in screen pixels, for a jump. The shadow stays where the
        /// feet were, which is what makes the lift read as a jump rather than as a figure sliding
        /// up the screen.</summary>
        public float Lift;

        /// <param name="textureName">A character sheet, for example <c>Characters\Linus</c>.</param>
        public SceneActor(string textureName, int spriteWidth = 16, int spriteHeight = 32)
        {
            if (string.IsNullOrWhiteSpace(textureName)) throw new ArgumentNullException(nameof(textureName));
            _sprite = new AnimatedSprite(textureName, 0, spriteWidth, spriteHeight);
            _spriteHeight = spriteHeight;
        }

        /// <summary>Choose the frame for this moment. <paramref name="elapsedMs"/> is the scene's own
        /// clock, so the walk cycle runs at the same rate however the game is ticking.</summary>
        public void Animate(int elapsedMs)
        {
            int step = Walking ? Math.Abs(elapsedMs / StepMs) % FramesPerDirection : 0;
            _sprite.currentFrame = Facing * FramesPerDirection + step;
            _sprite.UpdateSourceRect();
        }

        /// <summary>The sheet as the engine sees it, for the log. Tasks 8 and 9 draw a Shadow Brute
        /// and Shane through this class, and a sheet that is not four columns of the expected size
        /// would put every facing in the wrong place, so it is worth one Trace line.</summary>
        public string Describe()
        {
            Texture2D sheet = _sprite?.Texture;
            if (sheet == null) return "no texture";
            return $"{sheet.Width}x{sheet.Height}, {_sprite.SpriteWidth}x{_sprite.SpriteHeight} frames, {sheet.Width / Math.Max(1, _sprite.SpriteWidth)} column(s)";
        }

        /// <summary>Face whichever way <paramref name="target"/> lies, preferring left and right:
        /// a side-on villager reads as looking at something, a back or a front does not.</summary>
        public void Face(Vector2 target)
        {
            float dx = target.X - Position.X;
            float dy = target.Y - Position.Y;
            if (Math.Abs(dx) >= Math.Abs(dy)) Facing = dx >= 0 ? FacingRight : FacingLeft;
            else Facing = dy >= 0 ? FacingDown : FacingUp;
        }

        /// <summary>Draw him at the world layer, with a shadow under his feet.</summary>
        public void Draw(SpriteBatch b, Color tint, float layerDepth = 0.9f)
        {
            // The sheet is taller than a tile, so the sprite's top-left sits that much above the
            // tile it stands on and his feet land on the tile itself.
            float lift = _spriteHeight * DrawScale - TileSize;
            Vector2 screen = SceneCamera.ToScreen(Position + new Vector2(Shake, -lift - Lift));
            if (Game1.shadowTexture != null)
            {
                b.Draw(
                    Game1.shadowTexture,
                    SceneCamera.ToScreen(Position + new Vector2(TileSize / 2f + Shake, TileSize - 8f)),
                    Game1.shadowTexture.Bounds,
                    Color.White * 0.6f,
                    0f,
                    new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y),
                    DrawScale,
                    SpriteEffects.None,
                    layerDepth - 0.001f);
            }
            _sprite.draw(b, screen, layerDepth, 0, 0, tint, flip: false, DrawScale);
        }
    }
}
