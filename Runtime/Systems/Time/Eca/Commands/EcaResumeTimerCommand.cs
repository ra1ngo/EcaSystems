using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    public sealed class EcaResumeTimerCommand : AEcaCommand<IEcaActionContext, string>
    {
        public override string Id => EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_RESUME_ID);
        private readonly TimeSystem _time;

        public EcaResumeTimerCommand(TimeSystem time) =>
            _time = time ?? throw new ArgumentNullException(nameof(time));

        public override Task Run(IEcaActionContext context, string timerId)
        {
            _time.Resume(timerId);
            return Task.CompletedTask;
        }
    }
}
