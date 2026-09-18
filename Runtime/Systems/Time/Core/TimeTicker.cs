using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;

namespace EcaSystems.Time
{
    /// <summary>Main-thread PlayerLoop clock source shared by TimeSystem instances.</summary>
    public sealed class TimeTicker
    {
        public static TimeTicker Instance { get; } = new();
        public double CurrentTime { get; private set; }
        public double CurrentUnscaledTime { get; private set; }
        private Action<double, double> _tick;
        private Delegate[] _subscribers = Array.Empty<Delegate>();

        public event Action<double, double> TimeTick
        {
            add { _tick += value; _subscribers = _tick?.GetInvocationList() ?? Array.Empty<Delegate>(); }
            remove { _tick -= value; _subscribers = _tick?.GetInvocationList() ?? Array.Empty<Delegate>(); }
        }

        internal TimeTicker() { }
        internal int SubscriberCount => _subscribers.Length;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            Instance._tick = null;
            Instance._subscribers = Array.Empty<Delegate>();
            Instance.CurrentTime = Instance.CurrentUnscaledTime = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Install()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            RemoveHooks(ref loop);
            if (!InsertHook(ref loop)) throw new InvalidOperationException("Unity Update phase was not found.");
            PlayerLoop.SetPlayerLoop(loop);
            Instance.CurrentTime = UnityEngine.Time.timeAsDouble;
            Instance.CurrentUnscaledTime = UnityEngine.Time.unscaledTimeAsDouble;
        }

        private static void Update() => Instance.Publish(UnityEngine.Time.timeAsDouble, UnityEngine.Time.unscaledTimeAsDouble);

        internal void Publish(double scaled, double unscaled)
        {
            UpdateTime(scaled, unscaled);
            // Cached invocation snapshot changes only on subscribe/unsubscribe.
            // A callback may alter subscriptions without modifying this frame's snapshot.
            var subscribers = _subscribers;
            foreach (var subscriber in subscribers)
            {
                try { ((Action<double, double>)subscriber)(scaled, unscaled); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }

        internal void UpdateTime(double scaled, double unscaled)
        {
            CurrentTime = scaled;
            CurrentUnscaledTime = unscaled;
        }
        private static void RemoveHooks(ref PlayerLoopSystem loop)
        {
            if (loop.subSystemList == null) return;
            var children = new List<PlayerLoopSystem>();
            foreach (var original in loop.subSystemList)
            {
                if (original.type == typeof(TimeTicker)) continue;
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
                children.Add(new PlayerLoopSystem { type = typeof(TimeTicker), updateDelegate = Update });
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
