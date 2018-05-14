using System;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IIncrementalHash : IDisposable
    {
        void AppendData(byte[] buffer, int start, int count);
        byte[] GetHashAndReset();
    }
}
