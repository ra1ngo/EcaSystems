using System.Collections.Generic;

namespace EcaSystems.Variables
{
    public sealed class EcaVariableSubtreeState
    {
        public string RootStoreId { get; }
        public IReadOnlyList<EcaVariableStoreState> Stores { get; }

        internal EcaVariableSubtreeState(string rootStoreId, IEnumerable<EcaVariableStoreState> stores)
        {
            RootStoreId = rootStoreId;
            Stores = new List<EcaVariableStoreState>(stores).AsReadOnly();
        }
    }
}
