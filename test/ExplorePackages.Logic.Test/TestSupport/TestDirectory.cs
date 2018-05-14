using System;
using System.IO;

namespace Knapcode.ExplorePackages.Logic.TestSupport
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
                    "Knapcode.MiniZip.Test",
                    Guid.NewGuid().ToString());
            }

            path = Path.GetFullPath(path);

            Directory.CreateDirectory(path);

            return new TestDirectory(path);
        }

        public static implicit operator string(TestDirectory directory)
        {
            return directory.FullPath;
        }

        public override string ToString()
        {
            return FullPath;
        }
    }
}
