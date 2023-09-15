# Reusable Classes

Aside from the drivers in this project which are tightly coupled with NuGet or NuGet.org, there are several classes
that provide useful infrastructure or are interesting for other one-off experiments.

These "reusable classes" are not written to be perfectly abstracted and are not ready to drop in to another as is, but
should be relatively easy to refactor a bit and copy to your own projects (observing licensing rules of course).

- [`AppendResultStorageService`](../src/Worker.Logic/AppendResults/AppendResultStorageService.cs) - Azure Function result aggregation using Tables or append blobs
- [`AutoRenewingStorageLeaseService`](../src/Logic/Leasing/AutoRenewingStorageLeaseService.cs) - an `IAsyncDisposable` that keeps a global lease renewed
- [`CsvRecordGenerator`](../src/SourceGenerator/CsvRecordGenerator.cs) - AOT CSV reading and writing for a C# record/POCO
- [`ReferenceTracker`](../src/Logic/ReferenceTracking/ReferenceTracker.cs) - track many-to-many relationships in Azure Table Storage
- [`TableEntitySizeCalculator`](../src/Logic/Storage/TableEntitySizeCalculator.cs) - calculate exact size in bytes for a Table Storage entity
- [`TablePrefixScanner`](../src/Logic/TablePrefixScan/TablePrefixScanner.cs) - run a distributed scan of a big Azure Storage Table, faster than serial scans
- [`TempStreamService`](../src/Logic/TempStream/TempStreamService.cs) - buffer to local storage (memory or disk), great for Azure Functions Consumption Plan
- [`WideEntityService`](../src/Logic/WideEntities/WideEntityService.cs) - Blob Storage-like semantics with Azure Table Storage, enables batch operations


