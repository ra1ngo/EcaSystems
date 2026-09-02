using EcaSystems.Core;
using UnityEngine;

namespace EcaSystems.Unity
{
    public static class EcaDebug
    {
        public static void LogInstalled()
        {
            Debug.Log($"{EcaSystemsInfo.Name} {EcaSystemsInfo.Version} is installed.");
        }
    }
}