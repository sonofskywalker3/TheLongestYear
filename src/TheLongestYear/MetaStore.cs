using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear
{
    /// <summary>Loads and persists <see cref="MetaState"/> via SMAPI global data (outlives any save).</summary>
    internal sealed class MetaStore
    {
        private const string DataKey = "meta-state";
        private readonly IDataHelper _data;

        public MetaState State { get; private set; }

        public MetaStore(IDataHelper data)
        {
            _data = data;
            State = _data.ReadGlobalData<MetaState>(DataKey) ?? new MetaState();
        }

        public void Save() => _data.WriteGlobalData(DataKey, State);
    }
}
