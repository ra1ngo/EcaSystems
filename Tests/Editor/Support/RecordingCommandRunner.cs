using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;

namespace EcaSystems.Tests.Support
{
    internal sealed class RecordingCommandRunner : IEcaCommandRunner
    {
        private readonly IEcaCommandRunner _runner;
        private readonly List<string> _log;
        public int BindCount { get; private set; }
        public Action<IEcaActionContext> OnBind { get; set; }
        public RecordingCommandRunner(IEcaCommandRunner runner, List<string> log) { _runner = runner; _log = log; }
        public IEcaCommands Bind(IEcaActionContext context)
        {
            BindCount++;
            _log.Add("bind");
            OnBind?.Invoke(context);
            return _runner.Bind(context);
        }
    }
}
