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

        private static List<CancellationTokenSource> _activeTasks = new List<CancellationTokenSource>();
        public static CancellationTokenSource Wait(float delaySeconds, Action callback)
        {
            if (delaySeconds <= 0.00001f)
            {
                callback?.Invoke();
                return null;
            }

            var token = new CancellationTokenSource();
            WaitForTime(delaySeconds, callback, token);
            _activeTasks.Add(token);
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

                        throw;
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
                await Delay(delay, token);
                callback?.Invoke();
            }
            catch (Exception e)
            {
                if (!(e is OperationCanceledException || e.InnerException is TaskCanceledException || e is ObjectDisposedException))
                    Controller.LogMessage(Controller.Level.Error, "**Exception, WaitForTime: " + e);

                throw e;
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
            Periodic(duration, period, callback, doneCallback, token);
            return token;
        }

        private static async Task Periodic(float duration, float period, Action<float> callback, Action doneCallback, CancellationTokenSource token)
        {
            try
            {
                _activeTasks.Add(token);
                float t = 0;
                callback?.Invoke(0);
                while (duration <= 0 || t < duration)
                {
                    await Delay(period, token);
                    t += period;
                    callback?.Invoke(t);
                    if (token.IsCancellationRequested)
                        break;
                }

                _activeTasks.Remove(token);
                doneCallback?.Invoke();
            }
            catch (Exception e)
            {
                if (!(e is OperationCanceledException || e.InnerException is TaskCanceledException))
                    Controller.LogMessage(Controller.Level.Error, "**Exception, Periodic: " + e);

                throw e;
            }
        }

        internal static void StopAllTasks()
        {
            foreach (var token in _activeTasks)
                StopTaskRemotely(token);
            _activeTasks.Clear();
        }
    }
}