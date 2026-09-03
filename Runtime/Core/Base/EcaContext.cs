using System;

namespace EcaSystems.Core
{
    public class EcaContext<TEventContext> : IEcaContext<TEventContext>
    {
        public TEventContext EventContext { get; }

        public EcaContext(TEventContext eventContext)
        {
            EventContext = eventContext;
        }
    }

    internal static class EcaContextType
    {
        public static Type GetEventContextType<TContext>()
        {
            return GetEventContextType(typeof(TContext));
        }

        public static Type GetEventContextType(Type contextType)
        {
            if (contextType.IsGenericType &&
                contextType.GetGenericTypeDefinition() == typeof(IEcaContext<>))
            {
                return contextType.GetGenericArguments()[0];
            }

            var interfaces = contextType.GetInterfaces();

            for (var i = 0; i < interfaces.Length; i++)
            {
                var interfaceType = interfaces[i];

                if (!interfaceType.IsGenericType)
                    continue;

                if (interfaceType.GetGenericTypeDefinition() != typeof(IEcaContext<>))
                    continue;

                return interfaceType.GetGenericArguments()[0];
            }

            throw new InvalidOperationException(
                $"Context type '{contextType}' must implement IEcaContext<TEventContext>."
            );
        }
    }
}