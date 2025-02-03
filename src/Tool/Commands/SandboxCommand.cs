// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using McMaster.Extensions.CommandLineUtils;
using NuGet.Insights.Worker;
using NuGet.Insights.Worker.PackageFileToCsv;

namespace NuGet.Insights.Tool
{
    public class SandboxCommand : ICommand
    {
        private readonly ILogger<SandboxCommand> _logger;

        public SandboxCommand(ILogger<SandboxCommand> logger)
        {
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Yield();

            /*
            foreach (var path in Directory.EnumerateFiles(@"C:\Users\joelv\Downloads\packagefiles", "*.csv"))
            {
                _logger.LogInformation("Processing {Path}", path);
                using var writer = new StreamWriter(path + ".2");
                using var streamReader = new StreamReader(path);
                string line;
                var firstLine = true;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (firstLine)
                    {
                        line = "ScanId,ScanTimestamp," + line;
                        firstLine = false;
                    }
                    else
                    {
                        line = ",," + line;
                    }
                    writer.WriteLine(line);
                }
            }

            var csvReader = new CsvReaderAdapter(NullLogger<CsvReaderAdapter>.Instance);
            foreach (var path in Directory.EnumerateFiles(@"C:\Users\joelv\Downloads\packagefiles", "*.2"))
            {
                _logger.LogInformation("Processing {Path}", path);
                using var streamReader = new StreamReader(path);
                using var writer = new StreamWriter(Path.ChangeExtension(path, ".3"));
                PackageFileRecord.WriteHeader(writer);
                var records = csvReader.GetRecordsEnumerable<PackageFileRecord>(streamReader, CsvReaderAdapter.MaxBufferSize);
                foreach (var record in records)
                {
                    record.Write(writer);
                }
            }
            */

            var csvReader = new CsvReaderAdapter(NullLogger<CsvReaderAdapter>.Instance);
            foreach (var path in Directory.EnumerateFiles(@"C:\Users\joelv\Downloads\packagefiles", "*.3"))
            {
                _logger.LogInformation("Processing {Path}", path);
                using var streamReader = new StreamReader(path);
                using var writer = new StreamWriter(Path.ChangeExtension(path, ".4"));
                PackageFileRecord.WriteHeader(writer);
                var records = csvReader.GetRecordsEnumerable<PackageFileRecord>(streamReader, CsvReaderAdapter.MaxBufferSize);
                foreach (var record in records)
                {
                    record.Write(writer);
                }
            }
        }
    }
}
