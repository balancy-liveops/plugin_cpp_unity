using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace Balancy
{
    // Diagnostic-only instrumentation. Does not yield, move work, or change SDK scheduling.
    internal static class FreezeDiagnostics
    {
        private static readonly long Origin = Stopwatch.GetTimestamp();
        private static long previousFrame;
        private static bool paused;
        internal static long Now => Stopwatch.GetTimestamp();
        internal static double Ms(long start) => (Now - start) * 1000.0 / Stopwatch.Frequency;
        internal static void Log(string text)
        {
            UnityEngine.Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}",
                "[FREEZE182] t_ms=" + Ms(Origin).ToString("F1", CultureInfo.InvariantCulture)
                + " tid=" + Thread.CurrentThread.ManagedThreadId + " " + text);
        }
        internal static void End(string name, long start, double threshold = 20)
        {
            double elapsed = Ms(start);
            if (elapsed >= threshold)
                Log(name + " duration_ms=" + elapsed.ToString("F1", CultureInfo.InvariantCulture));
        }
        internal static void Frame()
        {
            long now = Now;
            if (!paused && previousFrame != 0 && Ms(previousFrame) >= 100)
                End("FRAME_GAP frame=" + Time.frameCount, previousFrame, 100);
            previousFrame = now;
        }
        internal static void Pause(bool value)
        {
            paused = value;
            previousFrame = 0;
            Log("APPLICATION_PAUSE " + value);
        }
        internal static string Route(string url)
        {
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri) ? uri.AbsolutePath : "relative-resource";
        }
    }
}
