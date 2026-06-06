using StardewModdingAPI;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear
{
    /// <summary>
    /// Loads and persists both banked meta-state and the active run-state as per-save data, scoped to
    /// one playthrough and committed as part of the game's own save (never eagerly) — so neither can be
    /// save-scummed.
    /// </summary>
    internal sealed class MetaStore
    {
        private const string MetaDataKey = "meta-state";
        private const string RunDataKey = "run-state";
        private readonly IDataHelper _data;
        private JunimoStashService _stashService;

        public MetaState State { get; private set; } = new MetaState();
        public RunState Run { get; private set; } = new RunState();

        /// <summary>True when the last <see cref="Load"/> read real on-disk TLY data rather than
        /// coalescing to fresh defaults. Lets ModEntry back-fill the run marker on pre-existing TLY
        /// saves (created before <see cref="MetaState.IsLongestYearRun"/> existed) without falsely
        /// claiming a brand-new non-TLY save as a run.</summary>
        public bool LoadedExistingData { get; private set; }

        public MetaStore(IDataHelper data) => _data = data;

        /// <summary>Connect the stash service so BankToMeta fires before each save.</summary>
        public void AttachStashService(JunimoStashService service)
            => _stashService = service;

        /// <summary>Load this playthrough's banked progress and active run. Call when a save is loaded.</summary>
        public void Load()
        {
            MetaState meta = _data.ReadSaveData<MetaState>(MetaDataKey);
            LoadedExistingData = meta != null;
            State = meta ?? new MetaState();
            Run = _data.ReadSaveData<RunState>(RunDataKey) ?? new RunState();
        }

        /// <summary>Commit banked progress and run-state into the save. Call from the game's Saving event.</summary>
        public void Save()
        {
            // Capture chest contents into MetaState before serialising.
            _stashService?.BankToMeta();
            _data.WriteSaveData(MetaDataKey, State);
            _data.WriteSaveData(RunDataKey, Run);
        }

        /// <summary>
        /// Reset <see cref="State"/> to a fresh <see cref="MetaState"/> and persist immediately
        /// (so a save reload picks up the wipe). Used by the <c>tly_wipemeta</c> debug command
        /// — lets the user test a true clean-slate run without deleting the underlying save file.
        /// <para>
        /// Side effect: external consumers that captured a direct reference to the old
        /// <see cref="State"/> instance (JunimoStashCapPatch, etc.) hold stale pointers after
        /// this call. The caller is expected to log a "reload the save" instruction so the
        /// user gets fresh service wiring.
        /// </para>
        /// </summary>
        public void WipeMeta()
        {
            State = new MetaState();
            Save();
        }
    }
}
