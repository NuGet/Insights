namespace Knapcode.ExplorePackages.Logic
{
    public interface IBatchSizeProvider
    {
        int Get(BatchSizeType type);
    }
}