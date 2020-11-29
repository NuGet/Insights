using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Knapcode.ExplorePackages.Worker;
using Knapcode.ExplorePackages.Worker.FindPackageAssets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;

namespace Knapcode.ExplorePackages
{
    public class TheAppendResultStorageServiceClass
    {
        // [SimpleJob(launchCount: 0, warmupCount: 0, targetCount: 1)]
        public class TheCompactAsyncMethod : IDisposable
        {
            private readonly ServiceProvider _serviceProvider;
            private readonly AppendResultStorageService _appendResultStorageService;
            private readonly byte[] _dataBytes;
            private byte[] _writtenBytes;

            public TheCompactAsyncMethod()
            {
                var serviceCollection = new ServiceCollection();

                serviceCollection.AddExplorePackages("Knapcode.ExplorePackages.Benchmarks");
                serviceCollection.AddExplorePackagesWorker();

                var serviceClientFactory = new Mock<IServiceClientFactory>();
                var account = new Mock<ICloudStorageAccount>();
                var blobClient = new Mock<ICloudBlobClient>();
                var container = new Mock<ICloudBlobContainer>();
                var blob = new Mock<ICloudBlockBlobWrapper>();

                _dataBytes = File.ReadAllBytes("FindPackageAssets.csv");

                blob
                    .Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>()))
                    .Returns<Stream>(s =>
                    {
                        s.Write(_dataBytes, 0, _dataBytes.Length);
                        return Task.CompletedTask;
                    });
                blob
                    .Setup(x => x.UploadFromStreamAsync(
                        It.IsAny<Stream>(),
                        It.IsAny<AccessCondition>(),
                        It.IsAny<BlobRequestOptions>(),
                        It.IsAny<OperationContext>()))
                    .Returns<Stream, AccessCondition, BlobRequestOptions, OperationContext>((s, _, __, ___) =>
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            s.CopyTo(memoryStream);
                            _writtenBytes = memoryStream.ToArray();
                        }

                        return Task.CompletedTask;
                    });
                blob.Setup(x => x.Properties).Returns(new BlobProperties());
                blob.Setup(x => x.ExistsAsync()).ReturnsAsync(true);

                container.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(() => blob.Object);
                container.Setup(x => x.GetBlockBlobReference(It.IsAny<string>())).Returns(() => blob.Object);
                blobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(() => container.Object);
                account.Setup(x => x.CreateCloudBlobClient()).Returns(() => blobClient.Object);
                serviceClientFactory.Setup(x => x.GetAbstractedStorageAccount()).Returns(() => account.Object);

                serviceCollection.AddSingleton(serviceClientFactory.Object);

                serviceCollection.Configure<ExplorePackagesWorkerSettings>(x =>
                {
                    x.AppendResultStorageMode = AppendResultStorageMode.AppendBlob;
                });

                _serviceProvider = serviceCollection.BuildServiceProvider();
                _appendResultStorageService = _serviceProvider.GetRequiredService<AppendResultStorageService>();
            }

            public void Dispose() => _serviceProvider.Dispose();

            [Benchmark]
            public async Task CustomCsvReader() => await ExecuteAsync(new CustomCsvReader());

            [Benchmark]
            public async Task NRecoCsvReader() => await ExecuteAsync(new NRecoCsvReader());

            [Benchmark]
            public async Task ServiceStackTextCsvReader() => await ExecuteAsync(new ServiceStackTextCsvReader());

            [Benchmark]
            public async Task CsvHelperCsvReader() => await ExecuteAsync(new CsvHelperCsvReader());

            [Benchmark]
            public async Task FastCsvParserCsvReader() => await ExecuteAsync(new FastCsvParserCsvReader());

            [Benchmark]
            public async Task LumenWorksCsvReader() => await ExecuteAsync(new LumenWorksCsvReader());

            [Benchmark]
            public async Task TinyCsvReader() => await ExecuteAsync(new TinyCsvReader());

            private async Task ExecuteAsync(ICsvReader reader)
            {
                await _appendResultStorageService.CompactAsync<PackageAsset>(
                   "findpackageassets",
                   "findpackageassets",
                   0,
                   force: true,
                   mergeExisting: true,
                   FindPackageAssetsCompactProcessor.PruneAssets,
                   reader);

                if (!_dataBytes.SequenceEqual(_writtenBytes))
                {
                    throw new InvalidOperationException("The written bytes do no match the read bytes.");
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            IConfig config = null;
#if DEBUG
            config = new DebugInProcessConfig();
#endif
            BenchmarkRunner.Run<TheAppendResultStorageServiceClass.TheCompactAsyncMethod>(config);
        }
    }
}
