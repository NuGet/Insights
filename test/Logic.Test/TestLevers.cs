// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NuGet.Insights.FileSystemHttpCache;

namespace NuGet.Insights
{
    /// <summary>
    /// This class has static variables which you can easily override to change the test settings.
    /// These will override any related environment variables, per setting.
    /// This exists because it may be harder to set an environment variable than to recompile (e.g. while running inside an IDE).
    /// </summary>
    public static class TestLevers
    {
        /// <summary>
        /// This should only be on when generating new test data locally. It should never be checked in as true.
        /// </summary>
        public static readonly bool OverwriteTestData = false;

        /// <summary>
        /// Use this to overwrite existing driver docs. Do not enable this if you have uncommitted driver docs changes!
        /// </summary>
        public static readonly bool OverwriteDriverDocs = false;

        /// <summary>
        /// Override <see cref="LogicTestSettings.UseMemoryStorageEnvName"/> environment variable, used by <see cref="LogicTestSettings"/>.
        /// </summary>
        public static readonly bool? UseMemoryStorage = null;

        /// <summary>
        /// Override <see cref="LogicTestSettings.UseDevelopmentStorageEnvName"/> environment variable, used by <see cref="LogicTestSettings"/>.
        /// </summary>
        public static readonly bool? UseDevelopmentStorage = null;

        /// <summary>
        /// Override <see cref="LogicTestSettings.StorageAccountNameEnvName"/> environment variable, used by <see cref="LogicTestSettings"/>.
        /// </summary>
        public static readonly string? StorageAccountName = null;

        /// <summary>
        /// Override <see cref="LogicTestSettings.FileSystemHttpCacheModeEnvName"/> environment variable, used by <see cref="LogicTestSettings"/>.
        /// </summary>
        public static readonly FileSystemHttpCacheMode? HttpCacheMode = null;
    }
}
