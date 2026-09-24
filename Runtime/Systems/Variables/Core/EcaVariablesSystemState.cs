using System.Collections.Generic;

namespace EcaSystems.Variables
{
    public sealed class EcaVariablesSystemState
    {
        public IReadOnlyList<EcaVariableStoreState> Stores { get; }

        internal EcaVariablesSystemState(IEnumerable<EcaVariableStoreState> stores)
        {
            Stores = new List<EcaVariableStoreState>(stores).AsReadOnly();
        }
    }
}
