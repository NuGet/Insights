# Adding a new driver

So the data produced by the [existing drivers](../README.md#drivers) don't cut it and you want to discover something
new about NuGet packages?

Follow these steps to write a new catalog scan **driver**.

## High level checklist

1. Implement your driver
1. Start a short time range catalog scan for your driver using the website admin panel
1. Complete the catalog scan by running the Azure Function
1. Verify the results in Azure Storage
1. Add integration tests
1. Update the [drivers list](../README.md#drivers) to mention your driver
2. If outputting CSVs, update [ImportTo-Kusto.ps1](../scripts/Kusto/ImportTo-Kusto.ps1) and [compare.kql](../scripts/Kusto/compare.kql) with your new tables
3. Submit a 🆒 PR

## "Figure it out yourself" flow

If you don't want to follow a long guide, consider these steps:

1. Copy-paste an existing driver similar to do what you want to do.
1. Start the Azure Storage Emulator.
1. Start the [`Website`](../src/Website) project locally.
1. Navigate to the admin panel ("Admin" in the navbar).
1. Start a very short catalog scan for your new driver (e.g. **Use custom max** then specify `2015-02-01T06:22:45.8488496Z`)
1. Start the [`Worker`](../src/Worker) Azure Function project locally.
1. Address errors as they come up.

## Guided flow

If you want more details regarding the steps of the checklist above, read on.

### Implement a driver interface

To plug into the catalog scan flow, pick one of the following interfaces to implement with your new driver class. If
you're not sure, just mimic (read: copy-paste) one of the existing drivers.

Consider this step carefully because the implementation of a driver interface defines the core behavior of your driver
and how it fetches and persists data about packages.

1. [**`ICatalogLeafToCsvDriver<T>`**](../src/Worker.Logic/CatalogScan/CatalogScanToCsv/CatalogLeafToCsv/ICatalogLeafToCsvDriver.cs) -
   this is the most commonly used driver interface which makes it easy to read information about a specific package (ID +
   version) and write some results to CSV. Since CSV is a universal data format that just about every tool supports,
   you can collect your driver results into CSV and then import the CSVs into a tool of your choice.

   In short, this is great if you want to process each package individually and you want to write the results to CSV.

   When implementing the class for your CSV rows, considering inheriting from `PackageRecord`. This provides some nice
   default columns in your CSV. Also, your CSV row class must implement `ICsvRecord<T>`. This is a good thing since this
   allows your class to be automatically serializable to CSV using a built-in source generator.

   You'll need to add a Azure Blob Storage container name to
   [`NuGetInsightsWorkerSettings.cs`](../src/Worker.Logic/NuGetInsightsWorkerSettings.cs) to store the CSV files.

   - Example implementation: [`PackageAssetToCsvDriver`](../src/Worker.Logic/CatalogScan/Drivers/PackageAssetToCsv/PackageAssetToCsvDriver.cs) -
     For each catalog leaf item, this driver fetches the list of file in the .nupkg and execute's NuGet client tooling's
     restore pattern sets (i.e. the rules used by `dotnet restore` to understand the significance of each package file)
     on the file list to determine what assets are in the package. For each asset, it writes out all of the properties (such
     as target framework) for the asset.

1. [`ICatalogLeafScanNonBatchDriver`](../src/Worker.Logic/CatalogScan/ICatalogLeafScanNonBatchDriver.cs) -
   This interface allows you to hook into the catalog scan flow in any way you want. This means that you can process
   the catalog index, pages, or leaves individually. At the leaf (package ID + version) level, you are provided with the
   information in a catalog leaf item (package ID + version + leaf URL + type). You can do whatever you want to process
   that package. It's up to you to fetch the data about the package you care about and persist the results.

   - Example implementation: [`CatalogLeafItemToCsv`](../src/Worker.Logic/CatalogScan/Drivers/CatalogLeafItemToCsv/CatalogLeafItemToCsvDriver.cs).
     This driver operates that this level since it doesn't even need to process each leaf item individually. Instead, it
     can just observe the data at the [catalog page](https://docs.microsoft.com/en-us/nuget/api/catalog-resource#catalog-page)
     level and drive each leaf item to CSV.


1. [`ICatalogLeafScanBatchDriver`](../src/Worker.Logic/CatalogScan/ICatalogLeafScanBatchDriver.cs) -
   This is the lowest level driver interface. Same as the previous `ICatalogLeafScanNonBatchDriver` but allows
   operating on multiple package leaves at once. The only reason you'd use this is for performance reasons. You can
   process and save results for multiple packages at once. This can be used to reduce round trips to storage.

   - Example implementation: [`LoadPackageArchiveDriver`](../src/Worker.Logic/CatalogScan/Drivers/LoadPackageArchive/LoadPackageArchiveDriver.cs) -
     This driver uses [MiniZip](https://github.com/joelverhagen/MiniZip) to fetch the ZIP central directory and package
     signature file for several packages and store them in Table Storage. The "batch" part of this flow is saving the
     results into Azure Table Storage.

### Register the driver

Ensure the driver can be activated by the catalog scan and admin interface. Update these places to help this work out:

1. Add your driver to the [`CatalogScanDriverType`](../src/Worker.Logic/CatalogScan/CatalogScanDriverType.cs) enum.
   - This provides a uniquely identifiable enum value for your driver.
1. Add your driver to the [`CatalogScanDriverFactory`](../src/Worker.Logic/CatalogScan/CatalogScanDriverFactory.cs) switch.
   - This allows your driver to be activated given a `CatalogScanDriverType` value.
   - You may need to add new classes to [dependency injection](../src/Worker.Logic/ServiceCollectionExtensions.cs) depending on what your driver needs.
1. Add your driver to the [`CatalogScanService`](../src/Worker.Logic/CatalogScan/CatalogScanService.cs) class.
   - Update `GetOnlyLatestLeavesSupport`. It's most likely that this method should return `true` or `null` for your driver.
   - Update `UpdateAsync`. This enqueues a catalog scan with the proper parameters for your driver.
1. Add your driver to the [`CatalogScanCursorService`](../src/Worker.Logic/CatalogScan/CatalogScanCursorService.cs) class.
   - Update the `Dependencies` static. This defines what cursors or other drivers your driver should block on before proceeding.
1. If your driver implements `ICatalogLeafToCsvDriver<T>`:
   - Add a CSV compact message schema name to [`SchemaCollectionBuilder`](../src/Worker.Logic/Serialization/SchemaCollectionBuilder.cs) like `cc.<abbreviation for your driver>`.
1. Add your driver to the `TypeToInfo` static in [`CatalogScanServiceTest.cs`](../test/Worker.Logic.Test/CatalogScan/CatalogScanServiceTest.cs).
   This determines the default catalog timestamp min value for your driver and implements a test function that forces
   your driver's dependency cursors to a specific timestamp.
1. If you had to add a table or blob container for your driver (e.g. a blob container for your CSV output), initialize
   the container name in [`BaseWorkerLogicIntegrationTest`](../test/Worker.Logic.Test/TestSupport/BaseWorkerLogicIntegrationTest.cs)
   in `ConfigureWorkerDefaultsAndSettings` to have a unique value starting with `StoragePrefix` like the other existing container names.
1. If your table produces CSV output, add your Kusto table name to [ImportTo-Kusto.ps1](../scripts/Kusto/ImportTo-Kusto.ps1) and [compare.kql](../scripts/Kusto/compare.kql).

### Add tests for your driver

The easiest way to do this is to copy the integration tests for another similar driver. Your integration tests should
cover at least two cases:

1. **"Happy path"** - process two short time ranges in the catalog and assert the output in storage.
1. **"Delete path"** - process two short time ranges, the first of which adds a package and the second of which deletes
   the package. This is an important test case because when a package is deleted from NuGet.org, the data related to this
   package in NuGet.Insights should be set to some "deleted" or "cleared" state. This enables reproducibility (deleted
   data is no longer available on NuGet.org and therefore data produced prior to the delete cannot be reproduced) and
   respects the privacy of the package owner.

These integration tests typically have some expected output data checked into the Git repository in the
[TestData](../test/Worker.Logic.Test/TestData) directory. The driver is run by the integration test and
the actual output is compared against this expected test data.

**To produce this test data for the first time:**

1. Set the [`BaseLogicIntegrationTest`](../test/Logic.Test/TestSupport/BaseLogicIntegrationTest.cs) `OverwriteTestData`
   static property to `true`.
1. Run your new tests.
1. Take a look the pending changes in Git (perhaps using `git diff`) and check if they make sense.
1. Set the `OverwriteTestData` to `false`.
1. Run the tests again to make sure the tests are passing.

You've now locked your test results into static files in the Git repository so future regressions can be caught.

### Document your driver

Update the [drivers list](../README.md#drivers) to mention your driver.

If your driver produces a CSV, the CSV schema must be documented similar to the existing CSV tables, in
[`docs/tables`](../docs/tables). The easiest way to get started is to run the `AllTablesAreListedInREADME` test in
[`DocsTest`](../test/Worker.Logic.Test/Docs/DocsTest.cs) to write out an initial version of the document matching the patterns
of the existing documents. Just fill in the TODOs and make sure all of the tests in 
[`DocsTest`](../test/Worker.Logic.Test/Docs/DocsTest.cs) after you are done with your document.
