using System;

namespace EcaSystems.Core
{
    public sealed class DelegateEcaCondition<TContext> : IEcaCondition<TContext>
    {
        private readonly Func<TContext, bool> _check;

        public DelegateEcaCondition(Func<TContext, bool> check)
        {
            _check = check ?? throw new ArgumentNullException(nameof(check));
        }

        public bool Check(TContext context)
        {
            return _check(context);
        }
    }
}