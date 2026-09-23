namespace EcaSystems.Variables
{
    public sealed class EcaVariable
    {
        private readonly EcaVariableController _controller;
        public EcaVariableData Data { get; }
        public string StoreId => Data.StoreId;
        public EcaVariableDefinition Definition => Data.Definition;
        public object CurrentValue => Data.CurrentValue;
        public object OldValue => Data.OldValue;

        internal EcaVariable(EcaVariableData data, EcaVariableStore owner)
        {
            Data = data;
            _controller = new EcaVariableController(this, owner);
        }

        public T GetValue<T>() => _controller.GetValue<T>();
        public void SetValue<T>(T value) => _controller.SetValue(value);
        public void ForceSetValue<T>(T value) => _controller.ForceSetValue(value);
        public void SetCurrentValue<T>(T value) => _controller.SetCurrentValue(value);
    }
}
