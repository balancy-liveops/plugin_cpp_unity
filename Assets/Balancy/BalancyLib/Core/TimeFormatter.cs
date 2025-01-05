using System;

namespace Balancy
{
    public static class TimeFormatter
    {
        public static string FormatUnixTime(int seconds)
        {
            if (seconds < 0 || seconds >= int.MaxValue)
                return "N/A";
            
            if (seconds == 0)
                return "0s";

            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

            
            // Extract time components
            int days = (int)(timeSpan.TotalDays);
            int hours = (int)(timeSpan.TotalHours % 24);
            int minutes = (int)(timeSpan.TotalMinutes % 60);
            int secs = (int)(timeSpan.TotalSeconds % 60);

            // Build result string with no more than 2 components
            if (days > 0)
            {
                if (hours > 0) return $"{days}d {hours}h";
                return $"{days}d";
            }

            if (hours > 0)
            {
                if (minutes > 0) return $"{hours}h {minutes}m";
                return $"{hours}h";
            }

            if (minutes > 0)
            {
                if (secs > 0) return $"{minutes}m {secs}s";
                return $"{minutes}m";
            }

            return $"{secs}s";
        }
    }
}