namespace Knapcode.ExplorePackages.Worker
{
    public enum CatalogScanDriverType
    {
        FindPackageFile,
        FindPackageManifest,

        FindPackageAssembly,
        FindPackageAsset,
        FindPackageSignature,

        FindCatalogLeafItem,
        FindLatestPackageLeaf,

        FindLatestCatalogLeafScan,
    }
}
