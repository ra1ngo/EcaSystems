using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    public sealed class EcaWaitCommand : AEcaCommand<IEcaActionContext, TimeWaitArgs>
    {
        public const string ID = "time.wait";
        public override string Id => ID;
        private readonly TimeSystem _time;

        public EcaWaitCommand(TimeSystem time) =>
            _time = time ?? throw new ArgumentNullException(nameof(time));

        public override async Task Run(IEcaActionContext context, TimeWaitArgs args)
        {
            await _time.Wait(args.Duration, args.ScaleMode);
        }
    }
}
