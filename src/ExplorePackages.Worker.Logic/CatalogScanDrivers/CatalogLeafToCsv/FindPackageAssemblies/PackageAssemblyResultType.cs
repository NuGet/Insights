namespace Knapcode.ExplorePackages.Worker.FindPackageAssemblies
{
    public enum PackageAssemblyResultType
    {
        NoAssemblies,
        NotManagedAssembly,
        DoesNotHaveMetadata,
        ValidAssembly,
        Deleted,
        InvalidZipEntry,
    }
}
