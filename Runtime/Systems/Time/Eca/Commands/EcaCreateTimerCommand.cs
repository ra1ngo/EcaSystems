using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    public sealed class EcaCreateTimerCommand : AEcaCommand<IEcaRuleState, IEcaActionContext, TimerCreateOptions>
    {
        public override string Id => EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_CREATE_ID);
        private readonly TimeSystem _time;

        public EcaCreateTimerCommand(TimeSystem time) =>
            _time = time ?? throw new ArgumentNullException(nameof(time));

        public override Task Run(IEcaRuleState state, IEcaActionContext context, TimerCreateOptions args)
        {
            _time.CreateTimer(args);
            return Task.CompletedTask;
        }
    }
}
