using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    public sealed class EcaPauseTimerCommand : AEcaCommand<IEcaRuleState, IEcaActionContext, string>
    {
        public override string Id => EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_PAUSE_ID);
        private readonly TimeSystem _time;

        public EcaPauseTimerCommand(TimeSystem time) =>
            _time = time ?? throw new ArgumentNullException(nameof(time));

        public override Task Run(IEcaRuleState state, IEcaActionContext context, string timerId)
        {
            _time.Pause(timerId);
            return Task.CompletedTask;
        }
    }
}
