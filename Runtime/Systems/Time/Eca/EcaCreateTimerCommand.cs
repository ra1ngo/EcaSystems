using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    public sealed class EcaCreateTimerCommand : AEcaCommand<IEcaActionContext, TimerCreateOptions>
    {
        public const string ID = "time.timer.create";
        public override string Id => ID;
        private readonly TimeSystem _time;

        public EcaCreateTimerCommand(TimeSystem time) =>
            _time = time ?? throw new ArgumentNullException(nameof(time));

        public override Task Run(IEcaActionContext context, TimerCreateOptions args)
        {
            _time.CreateTimer(args);
            return Task.CompletedTask;
        }
    }
}
