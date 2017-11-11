using System;

namespace Knapcode.ExplorePackages.Support
{
    public interface IIncrementalHash : IDisposable
    {
        void AppendData(byte[] buffer, int start, int count);
        byte[] GetHashAndReset();
    }
}
