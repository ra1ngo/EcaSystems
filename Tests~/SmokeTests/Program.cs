using System.Threading.Tasks;
using EcaSystems.Unity;
using UnityEngine;

internal static class Program
{
    private static async Task<int> Main()
    {
        await new EcaSystemsSmokeTest().RunTests();
        return Debug.Errors == 0 ? 0 : 1;
    }
}
