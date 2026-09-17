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
        private static readonly Stack<List<TimerRunner>> Snapshots = new();

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
            // Keep nested invocations separate from the snapshot still in flight.
            var snapshot = Snapshots.Count == 0 ? new List<TimerRunner>() : Snapshots.Pop();
            try
            {
                foreach (var runner in Active) snapshot.Add(runner);
                foreach (var runner in snapshot)
                {
                    if (!Active.Contains(runner)) continue;
                    try { runner.Tick(scaled, unscaled); }
                    catch (Exception error) { Debug.LogException(error); }
                }
            }
            finally
            {
                // Release runner references, retaining only reusable capacity.
                snapshot.Clear();
                Snapshots.Push(snapshot);
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
