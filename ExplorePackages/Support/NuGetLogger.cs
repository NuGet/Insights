using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using INuGetLogger = NuGet.Common.ILogger;
using INuGetLogMessage = NuGet.Common.ILogMessage;
using NuGetLogLevel = NuGet.Common.LogLevel;

namespace Knapcode.ExplorePackages.Support
{
    public class NuGetLogger : INuGetLogger
    {
        private readonly ILogger<NuGetLogger> _logger;

        public NuGetLogger(ILogger<NuGetLogger> logger)
        {
            _logger = logger;
        }

        public void LogDebug(string data)
        {
            _logger.LogTrace(data);
        }

        public void LogVerbose(string data)
        {
            _logger.LogDebug(data);
        }

        public void LogInformation(string data)
        {
            _logger.LogInformation(data);
        }

        public void LogMinimal(string data)
        {
            _logger.LogInformation(data);
        }

        public void LogWarning(string data)
        {
            _logger.LogWarning(data);
        }

        public void LogError(string data)
        {
            _logger.LogError(data);
        }

        public void LogInformationSummary(string data)
        {
            _logger.LogInformation(data);
        }

        public void LogErrorSummary(string data)
        {
            _logger.LogError(data);
        }

        public void Log(NuGetLogLevel level, string data)
        {
            _logger.Log(
                logLevel: GetLogLevel(level),
                eventId: 0,
                state: data,
                exception: null,
                formatter: (s, e) => s);
        }

        public Task LogAsync(NuGetLogLevel level, string data)
        {
            Log(level, data);
            return Task.CompletedTask;
        }

        public void Log(INuGetLogMessage message)
        {
            _logger.Log(
                logLevel: GetLogLevel(message.Level),
                eventId: new EventId((int)message.Code),
                state: message,
                exception: null,
                formatter: (s, e) => s.Message);
        }

        public Task LogAsync(INuGetLogMessage message)
        {
            Log(message);
            return Task.CompletedTask;
        }

        private static LogLevel GetLogLevel(NuGetLogLevel logLevel)
        {
            switch (logLevel)
            {
                case NuGetLogLevel.Debug:
                    return LogLevel.Trace;
                case NuGetLogLevel.Verbose:
                    return LogLevel.Debug;
                case NuGetLogLevel.Information:
                    return LogLevel.Information;
                case NuGetLogLevel.Minimal:
                    return LogLevel.Information;
                case NuGetLogLevel.Warning:
                    return LogLevel.Warning;
                case NuGetLogLevel.Error:
                    return LogLevel.Error;
                default:
                    return LogLevel.Trace;
            }
        }
    }
}
