using System;

namespace Knapcode.ExplorePackages
{
    public interface IIncrementalHash : IDisposable
    {
        void AppendData(byte[] buffer, int start, int count);
        byte[] GetHashAndReset();
    }
}
