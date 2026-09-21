using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    public sealed class EcaWaitCommand : AEcaCommand<IEcaRuleState, IEcaActionContext, TimeWaitArgs>
    {
        public override string Id => EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_WAIT_ID);
        private readonly TimeSystem _time;

        public EcaWaitCommand(TimeSystem time) =>
            _time = time ?? throw new ArgumentNullException(nameof(time));

        public override async Task Run(IEcaRuleState state, IEcaActionContext context, TimeWaitArgs args)
        {
            await _time.Wait(args.Duration, args.ScaleMode);
        }
    }
}
