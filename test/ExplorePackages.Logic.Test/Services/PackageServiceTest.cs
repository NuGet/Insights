using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.MiniZip;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageServiceTest
    {
        public class AddOrUpdatePackagesAsync_PackageArchiveMetadata : BaseDatabaseTest
        {
            public AddOrUpdatePackagesAsync_PackageArchiveMetadata(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SupportsAddingEntriesToExistingArchives()
            {
                // Arrange
                var id = "Knapcode.MiniZip";
                var ver = "0.4.0";
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    var packageRegistration = new PackageRegistrationEntity { Id = id };
                    entityContext.PackageRegistrations.Add(packageRegistration);
                    var package = new PackageEntity
                    {
                        PackageRegistration = packageRegistration,
                        Version = ver,
                    };
                    package.Identity = $"{package.PackageRegistration.Id}/{package.Version}";
                    entityContext.Packages.Add(package);

                    await entityContext.SaveChangesAsync();
                }

                var commitEnumerator = new CommitEnumerator(Output.GetLogger<CommitEnumerator>());
                var packageEnumerator = new PackageCommitEnumerator(EntityContextFactory, commitEnumerator);
                var target = new PackageService(
                    packageEnumerator,
                    NullCommitCondition.Instance,
                    EntityContextFactory,
                    Output.GetLogger<PackageService>());
                
                var a = await CreateArchiveAsync(id, ver, new ZipEntry("a.txt"));
                var b = await CreateArchiveAsync(id, ver, new ZipEntry("a.txt"), new ZipEntry("b.txt"));

                // Act
                await target.AddOrUpdatePackagesAsync(new[] { a });
                await target.AddOrUpdatePackagesAsync(new[] { b });

                // Assert
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    var archives = entityContext.PackageArchives.ToList();
                    Assert.Single(archives);
                    Assert.Equal(2, archives[0].EntryCount);

                    var entries = entityContext.PackageEntries.OrderBy(x => x.PackageEntryKey).ToList();
                    Assert.Equal(2, entries.Count);
                    Assert.Equal(0ul, entries[0].Index);
                    Assert.Equal(Encoding.ASCII.GetBytes("a.txt"), entries[0].Name);
                    Assert.Equal(1ul, entries[1].Index);
                    Assert.Equal(Encoding.ASCII.GetBytes("b.txt"), entries[1].Name);
                }
            }

            [Fact]
            public async Task SupportsModifyingEntriesInExistingArchives()
            {
                // Arrange
                var id = "Knapcode.MiniZip";
                var ver = "0.4.0";
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    var packageRegistration = new PackageRegistrationEntity { Id = id };
                    entityContext.PackageRegistrations.Add(packageRegistration);
                    var package = new PackageEntity
                    {
                        PackageRegistration = packageRegistration,
                        Version = ver,
                    };
                    package.Identity = $"{package.PackageRegistration.Id}/{package.Version}";
                    entityContext.Packages.Add(package);

                    await entityContext.SaveChangesAsync();
                }

                var commitEnumerator = new CommitEnumerator(Output.GetLogger<CommitEnumerator>());
                var packageEnumerator = new PackageCommitEnumerator(EntityContextFactory, commitEnumerator);
                var target = new PackageService(
                    packageEnumerator,
                    NullCommitCondition.Instance,
                    EntityContextFactory,
                    Output.GetLogger<PackageService>());
                
                var a = await CreateArchiveAsync(id, ver, new ZipEntry("a.txt"), new ZipEntry("b.txt"));
                var b = await CreateArchiveAsync(id, ver, new ZipEntry("b.txt"), new ZipEntry("a.txt"));

                // Act
                await target.AddOrUpdatePackagesAsync(new[] { a });
                await target.AddOrUpdatePackagesAsync(new[] { b });

                // Assert
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    var archives = entityContext.PackageArchives.ToList();
                    Assert.Single(archives);
                    Assert.Equal(2, archives[0].EntryCount);

                    var entries = entityContext.PackageEntries.OrderBy(x => x.PackageEntryKey).ToList();
                    Assert.Equal(2, entries.Count);
                    Assert.Equal(0ul, entries[0].Index);
                    Assert.Equal(Encoding.ASCII.GetBytes("b.txt"), entries[0].Name);
                    Assert.Equal(1ul, entries[1].Index);
                    Assert.Equal(Encoding.ASCII.GetBytes("a.txt"), entries[1].Name);
                }
            }

            [Fact]
            public async Task SupportsRemovingEntriesFromExistingArchives()
            {
                // Arrange
                var id = "Knapcode.MiniZip";
                var ver = "0.4.0";
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    var packageRegistration = new PackageRegistrationEntity { Id = id };
                    entityContext.PackageRegistrations.Add(packageRegistration);
                    var package = new PackageEntity
                    {
                        PackageRegistration = packageRegistration,
                        Version = ver,
                    };
                    package.Identity = $"{package.PackageRegistration.Id}/{package.Version}";
                    entityContext.Packages.Add(package);

                    await entityContext.SaveChangesAsync();
                }

                var commitEnumerator = new CommitEnumerator(Output.GetLogger<CommitEnumerator>());
                var packageEnumerator = new PackageCommitEnumerator(EntityContextFactory, commitEnumerator);
                var target = new PackageService(
                    packageEnumerator,
                    NullCommitCondition.Instance,
                    EntityContextFactory,
                    Output.GetLogger<PackageService>());

                var a = await CreateArchiveAsync(id, ver, new ZipEntry("a.txt"), new ZipEntry("b.txt"));
                var b = await CreateArchiveAsync(id, ver, new ZipEntry("a.txt"));

                // Act
                await target.AddOrUpdatePackagesAsync(new[] { a });
                await target.AddOrUpdatePackagesAsync(new[] { b });

                // Assert
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    var archives = entityContext.PackageArchives.ToList();
                    Assert.Single(archives);
                    Assert.Equal(1, archives[0].EntryCount);

                    var entries = entityContext.PackageEntries.ToList();
                    Assert.Single(entries);
                    var entry = entries[0];
                    Assert.Equal(0ul, entry.Index);
                    Assert.Equal(Encoding.ASCII.GetBytes("a.txt"), entry.Name);
                }
            }
        }

        private static async Task<PackageArchiveMetadata> CreateArchiveAsync(string id, string version, params ZipEntry[] files)
        {
            using (var zipArchiveStream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(zipArchiveStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var file in files)
                    {
                        var entry = zipArchive.CreateEntry(file.Name, file.CompressionLevel);

                        if (file.ExternalAttributes.HasValue)
                        {
                            entry.ExternalAttributes = file.ExternalAttributes.Value;
                        }

                        if (file.LastWriteTime.HasValue)
                        {
                            entry.LastWriteTime = file.LastWriteTime.Value;
                        }

                        using (var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(file.Contents)))
                        using (var destinationStream = entry.Open())
                        {
                            sourceStream.CopyTo(destinationStream);
                        }
                    }
                }

                zipArchiveStream.Position = 0;

                using (var reader = new ZipDirectoryReader(zipArchiveStream))
                {
                    var zipDirectory = await reader.ReadAsync();

                    return new PackageArchiveMetadata
                    {
                        Id = id,
                        Version = version,
                        Size = zipArchiveStream.Length,
                        ZipDirectory = zipDirectory,
                    };
                }
            }
        }

        private class ZipEntry
        {
            public ZipEntry(string name)
            {
                Name = name;
            }

            public string Name { get; set; }
            public string Contents { get; set; } = string.Empty;
            public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
            public int? ExternalAttributes { get; set; }
            public DateTimeOffset? LastWriteTime { get; set; }
        }
    }
}
