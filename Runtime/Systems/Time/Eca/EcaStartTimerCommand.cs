using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    public sealed class EcaStartTimerCommand : AEcaCommand<IEcaActionContext, string>
    {
        public const string ID = "time.timer.start";
        public override string Id => ID;
        private readonly TimeSystem _time;

        public EcaStartTimerCommand(TimeSystem time) =>
            _time = time ?? throw new ArgumentNullException(nameof(time));

        public override Task Run(IEcaActionContext context, string args)
        {
            _time.Start(args);
            return Task.CompletedTask;
        }
    }
}
