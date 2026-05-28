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

        public MetaStore(IDataHelper data) => _data = data;

        /// <summary>Connect the stash service so BankToMeta fires before each save.</summary>
        public void AttachStashService(JunimoStashService service)
            => _stashService = service;

        /// <summary>Load this playthrough's banked progress and active run. Call when a save is loaded.</summary>
        public void Load()
        {
            State = _data.ReadSaveData<MetaState>(MetaDataKey) ?? new MetaState();
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
    }
}
