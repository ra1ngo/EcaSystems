using System;
using System.Threading.Tasks;

// Только для консольного запуска: предметный код и smoke-тесты берутся из Runtime.
namespace UnityEngine
{
    public class MonoBehaviour { }

    public static class Debug
    {
        public static int Errors { get; private set; }
        public static void Log(object message) => Console.WriteLine(message);
        public static void LogError(object message)
        {
            Errors++;
            Console.Error.WriteLine(message);
        }
    }

    public static class Awaitable
    {
        public static Task NextFrameAsync() => Task.Delay(1);
    }
}
