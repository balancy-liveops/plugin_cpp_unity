using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Balancy
{
    public class Tasks
    {
        private const float ONE_FRAME = 1 / 60f;

        private static readonly List<CancellationTokenSource> _activeTasks = new List<CancellationTokenSource>();
        private static readonly object _activeTasksLock = new object();
        public static CancellationTokenSource Wait(float delaySeconds, Action callback)
        {
            if (delaySeconds <= 0.00001f)
            {
                callback?.Invoke();
                return null;
            }

            var token = new CancellationTokenSource();
            lock (_activeTasksLock)
                _activeTasks.Add(token);
            _ = WaitForTime(delaySeconds, callback, token);
            return token;
        }

        public static CancellationTokenSource WaitOneFrame(Action callback)
        {
            return Wait(ONE_FRAME, callback);
        }
        
        private static IEnumerator DoLogic(IEnumerator method, Action callback)
        {
            yield return method;
            callback?.Invoke();
        }

        public static void StopTaskRemotely(CancellationTokenSource token)
        {
            if (token != null)
            {
                lock (_activeTasksLock)
                    _activeTasks.Remove(token);
                try
                {
                    token.Cancel();
                    token.Dispose();
                }
                catch (Exception e)
                {
                    // handles ObjectDisposedException: The CancellationTokenSource has been disposed.
                    // System.Threading.CancellationTokenSource.ThrowObjectDisposedException
                    // Problem can appears, for example, in Wait call, when we pass token,
                    // so _activeTasks will hold this token until the end,
                    // but the caller would cancel this token before that.
                    // TODO: StopTaskRemotely => we need to handle canceling token in this case in the place where it was created
                    if (!(e is ObjectDisposedException))
                    {
                        Controller.LogMessage(Controller.Level.Error, "**Exception, StopTaskRemotely: " + e);
                    }
                }
            }
        }

        internal static async Task Delay(float delay, CancellationTokenSource token = null)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WebGLPlayer:
                    float startTime = Time.time;
                    while (Time.time < startTime + delay)
                    {
                        // TODO: not optimal, could be a low context switches
                        await Task.Yield();
                        token?.Token.ThrowIfCancellationRequested();
                    }
                    break;
                default:
                    await Task.Delay((int) (delay * 1000f), token?.Token ?? CancellationToken.None);
                    break;
            }
        }

        private static async Task WaitForTime(float delay, Action callback, CancellationTokenSource token)
        {
            try
            {
                var cancellation = token.Token;
                await Delay(delay, token);
                // The await normally captures Unity's synchronization context,
                // but the public API may also be called from a worker thread.
                if (callback != null)
                    UnityMainThreadDispatcher.RunOnMainThreadSafe(() =>
                    {
                        if (!cancellation.IsCancellationRequested)
                            callback();
                    });
            }
            catch (Exception e)
            {
                if (!(e is OperationCanceledException || e.InnerException is TaskCanceledException || e is ObjectDisposedException))
                    Controller.LogMessage(Controller.Level.Error, "**Exception, WaitForTime: " + e);

            }
            finally
            {
                lock (_activeTasksLock)
                    _activeTasks.Remove(token);
                // The source is returned to the caller, so normal completion must
                // not invalidate it behind their back. The caller owns disposal;
                // StopTaskRemotely/StopAllTasks consume and dispose active sources.
            }
        }

        public static CancellationTokenSource EveryFrame(float duration, Action<float> callback, Action doneCallback)
        {
            return Periodic(duration, ONE_FRAME, callback, doneCallback);
        }
        
        public static CancellationTokenSource Periodic(float period, Action<float> callback)
        {
            return Periodic(-1, period, callback, null);
        }
        
        public static CancellationTokenSource Periodic(float duration, float period, Action<float> callback, Action doneCallback)
        {
            var token = new CancellationTokenSource();
            lock (_activeTasksLock)
                _activeTasks.Add(token);
            _ = Periodic(duration, period, callback, doneCallback, token);
            return token;
        }

        private static async Task Periodic(float duration, float period, Action<float> callback, Action doneCallback, CancellationTokenSource token)
        {
            try
            {
                float t = 0;
                // The first tick runs synchronously on the caller (main) thread.
                callback?.Invoke(0);
                while (duration <= 0 || t < duration)
                {
                    await Delay(period, token);
                    t += period;
                    if (token.IsCancellationRequested)
                        break;
                    // A worker-thread caller may resume here off the main thread.
                    // Always use the safe dispatcher and skip callbacks cancelled
                    // while queued.
                    if (callback != null)
                    {
                        var elapsed = t;
                        // Capture the CancellationToken struct (not the source):
                        // it stays readable even after the source is disposed by
                        // StopTaskRemotely, so the queued callback can safely
                        // check it before firing.
                        var cancellation = token.Token;
                        UnityMainThreadDispatcher.RunOnMainThreadSafe(() =>
                        {
                            if (!cancellation.IsCancellationRequested)
                                callback(elapsed);
                        });
                    }
                }

                if (doneCallback != null)
                    UnityMainThreadDispatcher.RunOnMainThreadSafe(doneCallback);
            }
            catch (Exception e)
            {
                if (!(e is OperationCanceledException || e.InnerException is TaskCanceledException || e is ObjectDisposedException))
                    Controller.LogMessage(Controller.Level.Error, "**Exception, Periodic: " + e);
            }
            finally
            {
                lock (_activeTasksLock)
                    _activeTasks.Remove(token);
                // Keep a normally completed source usable by the caller. See
                // WaitForTime: only explicit remote/SDK shutdown consumes it.
            }
        }

        internal static void StopAllTasks()
        {
            CancellationTokenSource[] tokens;
            lock (_activeTasksLock)
            {
                tokens = _activeTasks.ToArray();
                _activeTasks.Clear();
            }

            foreach (var token in tokens)
                StopTaskRemotely(token);
        }
    }
}
