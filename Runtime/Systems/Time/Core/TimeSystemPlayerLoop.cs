using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;

namespace EcaSystems.Time
{
    internal static class TimeSystemPlayerLoop
    {
        // Only active work is rooted; idle systems/registries are never globally held.
        private static readonly HashSet<TimerRunner> Active = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => Active.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Install()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            RemoveHooks(ref loop);
            if (!InsertHook(ref loop)) throw new InvalidOperationException("Unity Update phase was not found.");
            PlayerLoop.SetPlayerLoop(loop);
        }

        internal static void SetActive(TimerRunner runner, bool active)
        {
            if (active)
            {
                if (Active.Count == 0) Install();
                Active.Add(runner);
            }
            else Active.Remove(runner);
        }

        private static void Update()
        {
            if (Active.Count == 0) return;
            var scaled = UnityEngine.Time.timeAsDouble;
            var unscaled = UnityEngine.Time.unscaledTimeAsDouble;
            var snapshot = new TimerRunner[Active.Count];
            Active.CopyTo(snapshot);
            foreach (var runner in snapshot)
            {
                if (!Active.Contains(runner)) continue;
                try { runner.Tick(scaled, unscaled); }
                catch (Exception error) { Debug.LogException(error); }
            }
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
