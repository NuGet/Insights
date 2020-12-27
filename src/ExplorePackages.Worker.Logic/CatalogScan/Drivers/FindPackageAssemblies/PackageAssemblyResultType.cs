namespace Knapcode.ExplorePackages.Worker.FindPackageAssemblies
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
