using System.Collections.Generic;

namespace EcaSystems.Variables
{
    public sealed class EcaVariableStoreState
    {
        public string StoreId { get; }
        public string ParentId { get; }
        public bool IsRoot => ParentId == null;
        public IReadOnlyList<EcaVariableSnapshot> Variables { get; }

        internal EcaVariableStoreState(string storeId, string parentId, IEnumerable<EcaVariableSnapshot> variables)
        {
            StoreId = storeId;
            ParentId = parentId;
            Variables = new List<EcaVariableSnapshot>(variables).AsReadOnly();
        }
    }
}
