using System;
using System.Collections.Generic;
using Balancy.Models.LiveOps.Tasks;
namespace Balancy
{
    internal static class CustomTasks
    {
        private sealed class Entry { public TaskCustom Model; public CustomTaskContext Context; }
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        private static readonly LibraryMethods.API.TaskLifecycleCallback Callback = OnLifecycle;
        private static int _generation;
        private static bool _registered;
        internal static void Register()
        {
            RegisterCore(() => LibraryMethods.API.balancyTasks_SetLifecycleCallback(Callback));
        }
        private static void RegisterCore(Action register)
        {
            if (_registered) return;
            System.Threading.Interlocked.Increment(ref _generation);
            register();
            _registered = true;
        }
        internal static void Unregister()
        {
            UnregisterCore(() => LibraryMethods.API.balancyTasks_SetLifecycleCallback(null));
        }
        private static void UnregisterCore(Action unregister)
        {
            System.Threading.Interlocked.Increment(ref _generation);
            try { if (_registered) unregister(); }
            finally {
                _registered = false;
                var old = new List<Entry>(Entries.Values);
                Entries.Clear();
                foreach (var entry in old) Stop(entry);
            }
        }
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.API.TaskLifecycleCallback))]
        private static void OnLifecycle(string id, string run, bool active)
        {
            var generation = System.Threading.Volatile.Read(ref _generation);
            // Always enqueue: game callbacks must never run inside a native mutation/lock.
            UnityMainThreadDispatcher.EnqueueFromAnyThread(() => {
                if (generation == System.Threading.Volatile.Read(ref _generation)) Dispatch(id, run, active);
            });
        }
        private static void Dispatch(string id, string run, bool active)
        {
            DispatchCore(id, run, active, context => {
                var info = context.Info;
                return info != null && info.Status == Models.LiveOps.TaskStatus.InProgress ? info.Task as TaskCustom : null;
            });
        }
        private static void DispatchCore(string id, string run, bool active, Func<CustomTaskContext, TaskCustom> resolve)
        {
            try {
                if (!active) {
                    if (Entries.TryGetValue(id, out var old) && old.Context.RunId == run) {
                        Entries.Remove(id);
                        Stop(old);
                    }
                    return;
                }
                var context = new CustomTaskContext(id, run);
                var model = resolve(context);
                if (model == null) return;
                if (Entries.TryGetValue(id, out var previous)) {
                    if (previous.Context.RunId == run) return;
                    Entries.Remove(id);
                    Stop(previous);
                }
                var entry = new Entry { Model = model, Context = context };
                Entries[id] = entry;
                try { model.OnStart(context); }
                catch {
                    Entries.Remove(id);
                    Stop(entry);
                    throw;
                }
            } catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
        }
        private static void Stop(Entry entry)
        {
            entry.Context.Invalidate();
            try { entry.Model.OnStop(entry.Context); }
            catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
        }
    }
}
