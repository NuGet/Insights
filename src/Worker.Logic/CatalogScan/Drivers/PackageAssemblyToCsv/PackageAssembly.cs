using System;
using System.Reflection;

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    public partial record PackageAssembly : PackageRecord, ICsvRecord
    {
        public PackageAssembly()
        {
        }

        public PackageAssembly(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageAssemblyResultType.Deleted;
        }

        public PackageAssembly(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, PackageAssemblyResultType resultType)
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

        public AssemblyHashAlgorithm? HashAlgorithm { get; set; }

        public bool? HasPublicKey { get; set; }
        public int? PublicKeyLength { get; set; }
        public string PublicKeySHA1 { get; set; }
    }
}
