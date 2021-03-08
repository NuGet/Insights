# Other Notable Classes

Aside from the [drivers], there are several classes that provide useful infrastructure or are interesting for other
one-off experiments.

- [`RunRealRestore`](../src/ExplorePackages.Worker.Logic/MessageProcessors/RunRealRestore/RunRealRestoreCompactProcessor.cs) - run `dotnet restore` to test package compatibility

Finally, some interesting generic services were built to enable this analysis:

- [`AppendResultStorageService`](../src/ExplorePackages.Worker.Logic/AppendResults/AppendResultStorageService.cs) - Azure Function result aggregation using Tables or append blobs
- [`AutoRenewingStorageLeaseService`](../src/ExplorePackages.Logic/Leasing/AutoRenewingStorageLeaseService.cs) - an `IAsyncDisposable` that keeps a global lease renewed
- [`CsvRecordGenerator`](../src/ExplorePackages.SourceGenerator/CsvRecordGenerator.cs) - AOT CSV reading and writing for a C# record/POCO
- [`TableEntitySizeCalculator`](../src/ExplorePackages.Logic/Storage/TableEntitySizeCalculator.cs) - calculate exact size in bytes for a Table Storage entity
- [`TablePrefixScanner`](../src/ExplorePackages.Logic/TablePrefixScan/TablePrefixScanner.cs) - run a distributed scan of a big Azure Storage Table, faster than serial scans
- [`TempStreamService`](../src/ExplorePackages.Logic/TempStream/TempStreamService.cs) - buffer to local storage (memory or disk), great for Azure Functions Consumption Plan
- [`WideEntityService`](../src/ExplorePackages.Logic/WideEntities/WideEntityService.cs) - Blob Storage-like semantics with Azure Table Storage, enables batch operations
