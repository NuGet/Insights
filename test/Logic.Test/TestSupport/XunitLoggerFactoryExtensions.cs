// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
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
    }
}
