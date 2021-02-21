namespace Knapcode.ExplorePackages.Worker
{
    public enum CatalogScanDriverType
    {
        /// <summary>
        /// Implemented by <see cref="LoadPackageFile.LoadPackageFileDriver"/>. Downloads interesting parts of the .nupkg
        /// file stored in the V3 flat container and stores the data in Azure Table Storage for other drivers to use.
        /// </summary>
        LoadPackageFile,

        /// <summary>
        /// Implemented by <see cref="LoadPackageManifest.LoadPackageManifestDriver"/>. Downloads the .nuspec from the
        /// V3 flat container and stores it in Azure Table Storage for other drivers to use.
        /// </summary>
        LoadPackageManifest,

        /// <summary>
        /// Implemented by <see cref="FindPackageAssembly.FindPackageAssemblyDriver"/>. For packages that contain
        /// assemblies, downloads the entire .nupkg and extract metadata about each assembly.
        /// </summary>
        FindPackageAssembly,

        /// <summary>
        /// Implemented by <see cref="FindPackageAsset.FindPackageAssetDriver"/>. Runs NuGet restore pattern sets against
        /// all files in each packages. This essentially determines all package assets the NuGet PackageReference restore
        /// recognizes.
        /// </summary>
        FindPackageAsset,

        /// <summary>
        /// Implemented by <see cref="FindPackageSignature.FindPackageSignatureDriver"/>. Extracts information from each
        /// NuGet package signature, determining things like certificate issuers and whether the package is author signed.
        /// </summary>
        FindPackageSignature,

        /// <summary>
        /// Implemented by <see cref="FindCatalogLeafItem.FindCatalogLeafItemDriver"/>. Reads all catalog leaf items
        /// and their associated page metadata. The catalog leaf item is described here:
        /// https://docs.microsoft.com/en-us/nuget/api/catalog-resource#catalog-item-object-in-a-page
        /// </summary>
        FindCatalogLeafItem,

        /// <summary>
        /// Implemented by <see cref="FindLatestPackageLeaf.LatestPackageLeafStorage"/> and <see cref="FindLatestLeafDriver{T}"/>.
        /// This driver records the latest catalog leaf URL for each package version to Azure Table Storage.
        /// </summary>
        FindLatestPackageLeaf,

        /// <summary>
        /// Implemented by <see cref="FindLatestCatalogLeafScan.LatestCatalogLeafScanStorage"/> and <see cref="FindLatestLeafDriver{T}"/>.
        /// This is a helper catalog scan used by <see cref="CatalogIndexScanMessageProcessor"/> when the driver returns
        /// <see cref="CatalogIndexScanResult.ExpandLatestLeaves"/>. This allows another driver to only process the latest
        /// catalog leaf per version instead of duplicating effort per version which is inevitable in the NuGet.org catalog.
        /// </summary>
        FindLatestCatalogLeafScan,
    }
}
