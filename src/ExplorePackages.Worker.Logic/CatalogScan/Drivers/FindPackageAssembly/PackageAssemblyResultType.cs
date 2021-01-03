namespace Knapcode.ExplorePackages.Worker.FindPackageAssembly
{
    public enum PackageAssemblyResultType
    {
        NoAssemblies,
        Deleted,
        ValidAssembly,
        NotManagedAssembly,
        NoManagedMetadata,
        DoesNotContainAssembly,
        InvalidZipEntry,
    }
}
