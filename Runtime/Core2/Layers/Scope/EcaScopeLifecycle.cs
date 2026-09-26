using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaScopeLifecycle
    {
        private readonly List<IEcaScopeLifecycleListener> _listeners = new();
        private bool _dispatching;

        public void Register(IEcaScopeLifecycleListener listener)
        {
            if (_dispatching) throw new InvalidOperationException("Lifecycle listeners cannot change during dispatch.");
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            foreach (var existing in _listeners)
                if (ReferenceEquals(existing, listener)) throw new InvalidOperationException("Listener is already registered.");
            _listeners.Add(listener);
        }

        public bool Unregister(IEcaScopeLifecycleListener listener)
        {
            if (_dispatching) throw new InvalidOperationException("Lifecycle listeners cannot change during dispatch.");
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            for (var i = 0; i < _listeners.Count; i++)
                if (ReferenceEquals(_listeners[i], listener)) { _listeners.RemoveAt(i); return true; }
            return false;
        }

        public void ConnectScope(EcaScope scope) => Dispatch(scope, listener => listener.ConnectScope(scope), listener => listener.DisconnectScope(scope));
        public void DisconnectScope(EcaScope scope) => Dispatch(scope, listener => listener.DisconnectScope(scope), listener => Restore(listener, scope));
        public void ConnectRule(EcaScope scope, IEcaRule rule) => DispatchRule(scope, rule, true);
        public void DisconnectRule(EcaScope scope, IEcaRule rule) => DispatchRule(scope, rule, false);

        private void DispatchRule(EcaScope scope, IEcaRule rule, bool connect)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            Dispatch(scope, listener => { if (connect) listener.ConnectRule(scope, rule); else listener.DisconnectRule(scope, rule); },
                listener => { if (connect) listener.DisconnectRule(scope, rule); else listener.ConnectRule(scope, rule); });
        }

        private void Dispatch(EcaScope scope, Action<IEcaScopeLifecycleListener> apply, Action<IEcaScopeLifecycleListener> undo)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (_dispatching) throw new InvalidOperationException("Reentrant lifecycle mutation is not supported.");
            _dispatching = true;
            try { Apply(_listeners.ToArray(), apply, undo); }
            finally { _dispatching = false; }
        }

        internal void RestoreScope(EcaScope scope)
        {
            ConnectScope(scope);
            try { foreach (var rule in scope.GetRulesSnapshot()) ConnectRule(scope, rule); }
            catch (Exception failure)
            {
                var undo = new Stack<Action>();
                undo.Push(() => DisconnectScope(scope));
                Rollback(undo, failure);
                throw;
            }
        }

        private static void Restore(IEcaScopeLifecycleListener listener, EcaScope scope)
        {
            listener.ConnectScope(scope);
            try { foreach (var rule in scope.GetRulesSnapshot()) listener.ConnectRule(scope, rule); }
            catch (Exception failure)
            {
                var undo = new Stack<Action>();
                undo.Push(() => listener.DisconnectScope(scope));
                Rollback(undo, failure);
                throw;
            }
        }

        internal static void Apply<T>(IEnumerable<T> items, Action<T> apply, Action<T> compensate)
        {
            var undo = new Stack<Action>();
            try
            {
                foreach (var item in items)
                {
                    apply(item);
                    var completed = item;
                    undo.Push(() => compensate(completed));
                }
            }
            catch (Exception failure) { Rollback(undo, failure); throw; }
        }

        internal static void Rollback(Stack<Action> undo, Exception failure)
        {
            List<Exception> errors = null;
            while (undo.Count != 0)
            {
                try { undo.Pop()(); }
                catch (Exception error) { (errors ??= new List<Exception> { failure }).Add(error); }
            }
            if (errors != null) throw new AggregateException("Lifecycle failed and compensation was incomplete.", errors);
        }
    }
}
