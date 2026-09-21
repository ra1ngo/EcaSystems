using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    public sealed class EcaStopTimerCommand : AEcaCommand<IEcaRuleState, IEcaActionContext, string>
    {
        public override string Id => EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_STOP_ID);
        private readonly TimeSystem _time;

        public EcaStopTimerCommand(TimeSystem time) =>
            _time = time ?? throw new ArgumentNullException(nameof(time));

        public override Task Run(IEcaRuleState state, IEcaActionContext context, string timerId)
        {
            _time.Stop(timerId);
            return Task.CompletedTask;
        }
    }
}
