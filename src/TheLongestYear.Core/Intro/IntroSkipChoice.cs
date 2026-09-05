namespace TheLongestYear.Core.Intro
{
    /// <summary>The "Skip intro" checkbox choice from character creation, carried across the
    /// new-game load. Recorded when the game commits the new character, consumed exactly once by
    /// the first save-load so a later new game starts clean. Skipping plants the cc-seen flag on
    /// the fresh morning, which sends <see cref="IntroSequenceDecider"/> straight to the theme
    /// picker (the cutscene is otherwise not skippable; see IntroEventInjector).</summary>
    public sealed class IntroSkipChoice
    {
        public bool Pending { get; private set; }

        public void Record(bool skip) => Pending = skip;

        public bool Consume()
        {
            bool p = Pending;
            Pending = false;
            return p;
        }
    }
}
