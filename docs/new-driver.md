# Adding a new driver

So the data produced by the [existing drivers](../docs/drivers/README.md) don't cut it and you want to discover something
new about NuGet packages?

Follow these steps to write a new catalog scan **driver**.

## High level checklist

These can all be done locally without deploying to Azure.

1. Implement your driver
1. Start a short time range catalog scan for your driver using the website admin panel
1. Complete the catalog scan by running the worker
1. Verify the results in Azure Storage (likely Azurite)
1. Add integration tests
2. Update the [drivers list](../docs/drivers/README.md) to mention your driver
3. Submit a 🆒 PR

## "Figure it out yourself" flow

If you don't want to follow a long guide, consider these steps:

1. Copy-paste an existing driver similar to do what you want to do.
2. Start Azurite, perhaps by installing the Azurite VS Code extension and starting Table, Queue, and Blob service.
3. Start the [`Website`](../src/Website) project locally.
4. Navigate to the admin panel ("Admin" in the navbar).
5. Start a very short catalog scan for your new driver (e.g. check **Use custom max** then use the default `2015-02-01T06:22:45.8488496Z`)
6. Start the [`Worker`](../src/Worker) Azure Function project locally.
7. Address errors as they come up.

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
   default columns in your CSV. Also, your CSV row class must implement `IAggregatedCsvRecord<T>`. This is a good thing since this
   allows your class to be automatically serializable to CSV using a built-in source generator.

   You'll need to add a Azure Blob Storage container name to
   [`NuGetInsightsWorkerSettings.Drivers.cs`](../src/Worker.Logic/NuGetInsightsWorkerSettings.Drivers.cs) to store the CSV files.

   - Example implementation: [`PackageAssetToCsvDriver`](../src/Worker.Logic/Drivers/PackageAssetToCsv/PackageAssetToCsvDriver.cs) -
     For each catalog leaf item, this driver fetches the list of file in the .nupkg and execute's NuGet client tooling's
     restore pattern sets (i.e. the rules used by `dotnet restore` to understand the significance of each package file)
     on the file list to determine what assets are in the package. For each asset, it writes out all of the properties (such
     as target framework) for the asset.

1. [`ICatalogLeafScanNonBatchDriver`](../src/Worker.Logic/CatalogScan/ICatalogLeafScanNonBatchDriver.cs) -
   This interface allows you to hook into the catalog scan flow in any way you want. This means that you can process
   the catalog index, pages, or leaves individually. At the leaf (package ID + version) level, you are provided with the
   information in a catalog leaf item (package ID + version + leaf URL + type). You can do whatever you want to process
   that package. It's up to you to fetch the data about the package you care about and persist the results.

1. [`ICatalogLeafScanBatchDriver`](../src/Worker.Logic/CatalogScan/ICatalogLeafScanBatchDriver.cs) -
   This is the lowest level driver interface. Same as the previous `ICatalogLeafScanNonBatchDriver` but allows
   operating on multiple package leaves at once. The only reason you'd use this is for performance reasons. You can
   process and save results for multiple packages at once. This can be used to reduce round trips to storage.

   - Example implementation: [`LoadPackageArchiveDriver`](../src/Worker.Logic/Drivers/LoadPackageArchive/LoadPackageArchiveDriver.cs) -
     This driver uses [MiniZip](https://github.com/joelverhagen/MiniZip) to fetch the ZIP central directory and package
     signature file for several packages and store them in Table Storage. The "batch" part of this flow is saving the
     results into Azure Table Storage.

### Register the driver

Ensure the driver can be activated by the catalog scan and admin interface. Update these places to help this work out:

1. Add your driver to the [`CatalogScanDriverType.Drivers.cs`](../src/Worker.Logic/CatalogScan/CatalogScanDriverType.Drivers.cs) enum.
   - This provides a uniquely identifiable enum value for your driver.
2. Add your driver as a static property in [`CatalogScanDriverMetadata.Drivers.cs`](../src/Worker.Logic/CatalogScan/CatalogScanDriverMetadata.Drivers.cs).
   - The establishes attributes for your driver that are needed for defaults and enabling/disabling features.
   - If you've mimicked another driver, consider look for how it is defined in that class an copy it.
3. Add your driver as a static proeprty in [`CatalogScanServiceTest.DriverInfo.cs`](../test/Worker.Logic.Test/CatalogScan/CatalogScanServiceTest.DriverInfo.cs).
   - This determines the default catalog timestamp min value for your driver and implements a test function that forces your driver's dependency cursors to a specific timestamp.
   - This essentially duplicates the information in `CatalogScanDriverMetadata` that you edited above to testing purposes.

### Add tests for your driver

The easiest way to do this is to copy the integration tests for another similar driver. Your integration tests should
cover at least two cases:

1. **"Happy path"** - process two short time ranges in the catalog and assert the output in storage.
1. **"Delete path"** - process two short time ranges, the first of which adds a package and the second of which deletes
   the package. This is an important test case because when a package is deleted from NuGet.org, the data related to this
   package in NuGet Insights should be set to some "deleted" or "cleared" state. This enables reproducibility (deleted
   data is no longer available on NuGet.org and therefore data produced prior to the delete cannot be reproduced) and
   respects the privacy of the package owner.

These integration tests typically have some expected output data checked into the Git repository in the
[TestData](../test/Worker.Logic.Test/TestData) directory. The driver is run by the integration test and
the actual output is compared against this expected test data.

**To produce this test data for the first time:**

1. Set the [`TestLevers`](../test/Logic.Test/TestLevers.cs) `OverwriteTestData` static property to `true`.
2. Run your new tests.
3. Take a look the pending changes in Git (perhaps using `git diff`) and check if they make sense.
4. Set the `OverwriteTestData` to `false`.
5. Run the tests again to make sure the tests are passing.

You've now locked your test results into static files in the Git repository so future regressions can be caught.

### Document your driver

When you run the [`DriverDocTest`](../test/Worker.Logic.Test/Docs/DriverDocsTest.cs) suite, it will generate a baseline driver document in the [`docs/drivers`](drivers) directory. If this doesn't happen, the actual code of your driver probably isn't done. Make sure all of the above steps are done. You'll need to fill in some placeholder TODOs for the generated driver document. It can help to look at the driver document of a similar driver. The [`PackageArchiveToCsv`](drivers/PackageArchiveToCsv.md) document is good to mimic for a driver that generates CSV records. The [`LoadPackageArchive`](drivers/LoadPackageArchive.md) document is good to mimic if your driver just loads data into Azure Table Storage for other drivers to use. Finally, update the [drivers list](drivers/README.md) to mention your driver.

If your driver produces a CSV, the CSV schema must be documented similar to the existing CSV tables, in
[`docs/tables`](tables). The easiest way to get started is to run the `AllTablesAreListedInREADME` test in
[`TableDocsTest.AllTables.cs`](../test/Worker.Logic.Test/Docs/TableDocsTest.AllTables.cs) suite to write out an initial version of the document
matching the patterns of the existing documents. Just fill in the TODOs and make sure all of the tests in
[`TableDocsTest`](../test/Worker.Logic.Test/Docs/TableDocsTest.cs) suite pass after you are done with your document.

Finally, update the [tables list](tables/README.md) to mention your table.
