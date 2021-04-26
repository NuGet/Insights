namespace Knapcode.ExplorePackages.Worker.PackageAssemblyToCsv
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
