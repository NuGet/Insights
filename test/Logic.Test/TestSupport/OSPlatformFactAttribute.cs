// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;

namespace NuGet.Insights
{
    public class OSPlatformFactAttribute : FactAttribute
    {
        public OSPlatformFactAttribute(OSPlatformType platformType)
        {
            var platforms = new List<OSPlatform>();
            foreach (var platformTypeOption in Enum.GetValues<OSPlatformType>())
            {
                if (platformType.HasFlag(platformTypeOption))
                {
                    platforms.Add(platformTypeOption switch
                    {
                        OSPlatformType.Windows => OSPlatform.Windows,
                        OSPlatformType.Linux => OSPlatform.Linux,
                        OSPlatformType.OSX => OSPlatform.OSX,
                        OSPlatformType.FreeBSD => OSPlatform.FreeBSD,
                        _ => throw new NotImplementedException(),
                    });
                }
            }

            if (!platforms.Any(RuntimeInformation.IsOSPlatform))
            {
                Skip = $"This Fact is skipped because the OS platform is {RuntimeInformation.OSDescription}, not {platformType}.";
            }
        }
    }
}
