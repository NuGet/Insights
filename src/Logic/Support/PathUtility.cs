// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Insights
{
    public static class PathUtility
    {
        private static readonly char[] DirectorySeparators = new[] { Path.DirectorySeparatorChar, '/', '\\' };

        public static string GetTopLevelFolder(string relativePath)
        {
            var firstSlashIndex = relativePath.IndexOfAny(DirectorySeparators);
            if (firstSlashIndex < 0)
            {
                return null;
            }

            return relativePath.Substring(0, firstSlashIndex);
        }
    }
}
