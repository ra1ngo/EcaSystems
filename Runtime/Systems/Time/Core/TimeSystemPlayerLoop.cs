using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;

namespace EcaSystems.Time
{
    internal static class TimeSystemPlayerLoop
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => TimeTicker.Instance.Reset();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Install()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            RemoveHooks(ref loop);
            if (!InsertHook(ref loop)) throw new InvalidOperationException("Unity Update phase was not found.");
            PlayerLoop.SetPlayerLoop(loop);
            TimeTicker.Instance.UpdateTime(UnityEngine.Time.timeAsDouble, UnityEngine.Time.unscaledTimeAsDouble);
        }

        private static void Update()
        {
            var scaled = UnityEngine.Time.timeAsDouble;
            var unscaled = UnityEngine.Time.unscaledTimeAsDouble;
            TimeTicker.Instance.Publish(scaled, unscaled);
        }

        private static void RemoveHooks(ref PlayerLoopSystem loop)
        {
            if (loop.subSystemList == null) return;
            var children = new List<PlayerLoopSystem>();
            foreach (var original in loop.subSystemList)
            {
                if (original.type == typeof(TimeSystemPlayerLoop)) continue;
                var child = original;
                RemoveHooks(ref child);
                children.Add(child);
            }
            loop.subSystemList = children.ToArray();
        }

        private static bool InsertHook(ref PlayerLoopSystem loop)
        {
            if (loop.type == typeof(UnityEngine.PlayerLoop.Update))
            {
                var children = new List<PlayerLoopSystem>(loop.subSystemList ?? Array.Empty<PlayerLoopSystem>());
                children.Add(new PlayerLoopSystem { type = typeof(TimeSystemPlayerLoop), updateDelegate = Update });
                loop.subSystemList = children.ToArray();
                return true;
            }
            if (loop.subSystemList == null) return false;
            for (var i = 0; i < loop.subSystemList.Length; i++)
                if (InsertHook(ref loop.subSystemList[i])) return true;
            return false;
        }
    }
}
