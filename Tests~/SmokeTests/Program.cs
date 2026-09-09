using System;
using System.Threading.Tasks;
using EcaSystems.Core;
using EcaSystems.Unity;
using UnityEngine;

internal static class Program
{
    private static async Task<int> Main()
    {
        await new EcaSystemsSmokeTest().RunTests();
        TestInternalBinding();
        return Debug.Errors == 0 ? 0 : 1;
    }

    private static void TestInternalBinding()
    {
        var state = new EcaRuleExecutionGroupState();
        var context = new EcaExecutionActionContext<int>(42, state);
        Expect("Конструктор без runner полностью подготовил контекст",
            context.EventContext == 42 && ReferenceEquals(context.RuleExecutionGroupState, state));
        ExpectThrows<InvalidOperationException>("До bind Commands недоступны", () => { _ = context.Commands; });
        ExpectThrows<ArgumentNullException>("Null bind отклонён", () => context.BindCommands(null));
        var runner = new EcaCommandRunner(new EcaCommandRegistry());
        var bound = runner.Bind(context);
        context.BindCommands(bound);
        Expect("Первый bind сохраняет точный bound API", ReferenceEquals(context.Commands, bound));
        ExpectThrows<InvalidOperationException>("Повторный internal bind запрещён",
            () => context.BindCommands(runner.Bind(context)));
        Expect("Повторный bind не заменяет Commands", ReferenceEquals(context.Commands, bound));
    }

    private static void Expect(string name, bool passed)
    {
        if (passed) Debug.Log("[PASS] " + name);
        else Debug.LogError("[FAIL] " + name);
    }

    private static void ExpectThrows<TException>(string name, Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { Expect(name, true); return; }
        Expect(name, false);
    }
}
