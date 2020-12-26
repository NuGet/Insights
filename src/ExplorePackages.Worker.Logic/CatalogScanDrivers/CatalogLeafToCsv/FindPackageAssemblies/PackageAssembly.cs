using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssemblies
{
    public partial class PackageAssembly : PackageRecord, IEquatable<PackageAssembly>, ICsvRecord
    {
        public PackageAssembly()
        {
        }

        public PackageAssembly(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageAssemblyResultType.Deleted;
        }

        public PackageAssembly(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf, PackageAssemblyResultType resultType)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = resultType;
        }

        public PackageAssemblyResultType ResultType { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string TopLevelFolder { get; set; }

        public long? CompressedLength { get; set; }
        public long? EntryUncompressedLength { get; set; }

        public long? ActualUncompressedLength { get; set; }
        public string FileSHA256 { get; set; }

        public bool HasException { get; set; }

        public string AssemblyName { get; set; }
        public Version AssemblyVersion { get; set; }
        public string Culture { get; set; }

        public bool? AssemblyNameHasCultureNotFoundException { get; set; }
        public bool? AssemblyNameHasFileLoadException { get; set; }

        public string PublicKeyToken { get; set; }
        public bool? PublicKeyTokenHasSecurityException { get; set; }

        public string HashAlgorithm { get; set; }

        public bool? HasPublicKey { get; set; }
        public int? PublicKeyLength { get; set; }
        public string PublicKeySHA1 { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageAssembly);
        }

        public bool Equals(PackageAssembly other)
        {
            return other != null &&
                   EqualityComparer<Guid?>.Default.Equals(ScanId, other.ScanId) &&
                   EqualityComparer<DateTimeOffset?>.Default.Equals(ScanTimestamp, other.ScanTimestamp) &&
                   Id == other.Id &&
                   Version == other.Version &&
                   CatalogCommitTimestamp.Equals(other.CatalogCommitTimestamp) &&
                   EqualityComparer<DateTimeOffset?>.Default.Equals(Created, other.Created) &&
                   ResultType == other.ResultType &&
                   Path == other.Path &&
                   FileName == other.FileName &&
                   FileExtension == other.FileExtension &&
                   TopLevelFolder == other.TopLevelFolder &&
                   CompressedLength == other.CompressedLength &&
                   EntryUncompressedLength == other.EntryUncompressedLength &&
                   ActualUncompressedLength == other.ActualUncompressedLength &&
                   FileSHA256 == other.FileSHA256 &&
                   HasException == other.HasException &&
                   AssemblyName == other.AssemblyName &&
                   EqualityComparer<Version>.Default.Equals(AssemblyVersion, other.AssemblyVersion) &&
                   Culture == other.Culture &&
                   AssemblyNameHasCultureNotFoundException == other.AssemblyNameHasCultureNotFoundException &&
                   AssemblyNameHasFileLoadException == other.AssemblyNameHasFileLoadException &&
                   PublicKeyToken == other.PublicKeyToken &&
                   PublicKeyTokenHasSecurityException == other.PublicKeyTokenHasSecurityException &&
                   HashAlgorithm == other.HashAlgorithm &&
                   HasPublicKey == other.HasPublicKey &&
                   PublicKeyLength == other.PublicKeyLength &&
                   PublicKeySHA1 == other.PublicKeySHA1;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(ScanId);
            hash.Add(ScanTimestamp);
            hash.Add(Id);
            hash.Add(Version);
            hash.Add(CatalogCommitTimestamp);
            hash.Add(Created);
            hash.Add(ResultType);
            hash.Add(Path);
            hash.Add(FileName);
            hash.Add(FileExtension);
            hash.Add(TopLevelFolder);
            hash.Add(CompressedLength);
            hash.Add(EntryUncompressedLength);
            hash.Add(ActualUncompressedLength);
            hash.Add(FileSHA256);
            hash.Add(HasException);
            hash.Add(AssemblyName);
            hash.Add(AssemblyVersion);
            hash.Add(Culture);
            hash.Add(AssemblyNameHasCultureNotFoundException);
            hash.Add(AssemblyNameHasFileLoadException);
            hash.Add(PublicKeyToken);
            hash.Add(PublicKeyTokenHasSecurityException);
            hash.Add(HashAlgorithm);
            hash.Add(HasPublicKey);
            hash.Add(PublicKeyLength);
            hash.Add(PublicKeySHA1);
            return hash.ToHashCode();
        }
    }
}
