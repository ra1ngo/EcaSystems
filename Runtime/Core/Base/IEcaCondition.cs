namespace EcaSystems.Core
{
    public interface IEcaCondition<in TContext>
    {
        bool Check(TContext context);
    }
}