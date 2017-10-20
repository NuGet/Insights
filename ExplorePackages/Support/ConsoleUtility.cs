using System;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Support
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
        internal static ConsoleColor? GetColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return ConsoleColor.Red;
                case LogLevel.Warning:
                    return ConsoleColor.Yellow;
            }

            return null;
        }
    }
}
