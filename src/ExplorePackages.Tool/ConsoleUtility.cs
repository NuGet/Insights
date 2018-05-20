using System;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool
{
    public static class ConsoleUtility
    {
        private static readonly object _consoleLock = new object();
        
        /// <summary>
        /// Log a message to the console.
        /// </summary>
        internal static void LogToConsole(LogLevel level, string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var color = GetColor(level);

            lock (_consoleLock)
            {
                // Colorize
                if (color.HasValue)
                {
                    Console.ForegroundColor = color.Value;
                }

                // Write message
                Console.WriteLine(message);

                if (color.HasValue)
                {
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// Colorize warnings and errors.
        /// </summary>
        private static ConsoleColor? GetColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    return ConsoleColor.DarkGray;
                case LogLevel.Warning:
                    return ConsoleColor.Yellow;
                case LogLevel.Error:
                case LogLevel.Critical:
                    return ConsoleColor.Red;
            }

            return null;
        }
    }
}
