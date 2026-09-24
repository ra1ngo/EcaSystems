using System;

namespace EcaSystems.Variables.Eca
{
    public sealed class EcaVariablesStatePayload
    {
        public string StoreId { get; }

        public EcaVariablesStatePayload(string storeId)
        {
            if (string.IsNullOrWhiteSpace(storeId)) throw new ArgumentException("StoreId cannot be empty.", nameof(storeId));
            StoreId = storeId;
        }
    }
}
