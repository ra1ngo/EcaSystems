using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    public sealed class EcaResumeTimerCommand : AEcaCommand<IEcaActionContext, string>
    {
        public const string ID = "time.timer.resume";
        public override string Id => ID;
        private readonly TimeSystem _time;

        public EcaResumeTimerCommand(TimeSystem time) =>
            _time = time ?? throw new ArgumentNullException(nameof(time));

        public override Task Run(IEcaActionContext context, string args)
        {
            _time.Resume(args);
            return Task.CompletedTask;
        }
    }
}
