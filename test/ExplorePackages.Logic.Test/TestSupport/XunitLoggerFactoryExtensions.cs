using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages
{
    /// <summary>
    /// Source:
    /// https://raw.githubusercontent.com/NuGet/NuGet.Jobs/ac0e8b67b94893180848ba485d661e56edcac3d1/tests/Validation.PackageSigning.Core.Tests/Support/XunitLoggerFactoryExtensions.cs
    /// </summary>
    public static class XunitLoggerFactoryExtensions
    {
        public static ILoggerFactory AddXunit(this ILoggerFactory loggerFactory, ITestOutputHelper output)
        {
            loggerFactory.AddProvider(new XunitLoggerProvider(output));
            return loggerFactory;
        }

        public static ILoggerFactory AddXunit(this ILoggerFactory loggerFactory, ITestOutputHelper output, LogLevel minLevel)
        {
            loggerFactory.AddProvider(new XunitLoggerProvider(output, minLevel));
            return loggerFactory;
        }
    }
}
