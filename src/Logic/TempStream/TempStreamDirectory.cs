using System;

namespace Knapcode.ExplorePackages
{
    public class TempStreamDirectory
    {
        /// <summary>
        /// This is the default buffer size for <see cref="System.IO.Stream.CopyToAsync(System.IO.Stream)"/> (80 KiB)
        /// rounded up to the nearest power of 2. This adheres to the default array pool behavior.
        /// </summary>
        public const int DefaultBufferSize = 128 * 1024;

        public string Path { get; set; }
        public int? MaxConcurrentWriters { get; set; }
        public TimeSpan SemaphoreTimeout { get; set; } = TimeSpan.Zero;
        public bool PreallocateFile { get; set; } = true;
        public int BufferSize { get; set; } = DefaultBufferSize;

        public static implicit operator TempStreamDirectory(string Path) => new TempStreamDirectory { Path = Path };

        public static implicit operator string(TempStreamDirectory dir) => dir.Path;

        public override string ToString()
        {
            return Path;
        }
    }
}
