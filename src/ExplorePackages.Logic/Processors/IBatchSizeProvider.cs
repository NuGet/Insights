namespace Knapcode.ExplorePackages
{
    public interface IBatchSizeProvider
    {
        void Decrease();
        int Get(BatchSizeType type);
        void Increase();
        void Reset();
        void Set(int batchSize);
    }
}