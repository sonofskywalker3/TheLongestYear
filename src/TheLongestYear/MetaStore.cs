using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear
{
    /// <summary>
    /// Loads and persists <see cref="MetaState"/> as per-save data, so banked progress is
    /// scoped to one playthrough and commits as part of the game's own save (never eagerly).
    /// </summary>
    internal sealed class MetaStore
    {
        private const string DataKey = "meta-state";
        private readonly IDataHelper _data;

        public MetaState State { get; private set; } = new MetaState();

        public MetaStore(IDataHelper data) => _data = data;

        /// <summary>Load this playthrough's banked progress. Call when a save is loaded.</summary>
        public void Load() => State = _data.ReadSaveData<MetaState>(DataKey) ?? new MetaState();

        /// <summary>Commit banked progress into the save. Call from the game's Saving event.</summary>
        public void Save() => _data.WriteSaveData(DataKey, State);
    }
}
