// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Insights
{
    /// <summary>
    /// Sources:
    /// https://github.com/NuGet/NuGet.Client/blob/32e8a6994f0dbd2dd562dc9233c9bceec94166cc/test/TestUtilities/Test.Utility/TestDirectory.cs
    /// https://github.com/NuGet/NuGet.Client/blob/32e8a6994f0dbd2dd562dc9233c9bceec94166cc/test/TestUtilities/Test.Utility/TestFileSystemUtility.cs
    /// </summary>
    public class TestDirectory : IDisposable
    {
        private TestDirectory(string path)
        {
            FullPath = path;
        }

        public string FullPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(FullPath))
            {
                try
                {
                    Directory.Delete(FullPath, recursive: true);
                }
                catch
                {

                    // Ignore such failures.
                }
            }
        }

        public static TestDirectory Create()
        {
            return Create(path: null);
        }

        public static TestDirectory Create(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(
                    Path.GetTempPath(),
                    "NuGet.Insights",
                    Guid.NewGuid().ToString());
            }

            path = Path.GetFullPath(path);

            Directory.CreateDirectory(path);

            return new TestDirectory(path);
        }

        public static implicit operator string(TestDirectory directory) => directory.FullPath;

        public override string ToString()
        {
            return FullPath;
        }
    }
}
