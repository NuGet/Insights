# Architecture of NuGet Insights

The purpose of this repository is to explore the characteristics, oddities, and
inconsistencies of NuGet.org's available packages.

Fundamentally, the project uses the [NuGet.org
catalog](https://docs.microsoft.com/en-us/nuget/api/catalog-resource) to
enumerate all package IDs and versions. For each ID and version, some unit of
work is performed. This unit of work can be some custom analysis that you want
to do on a package. There are some helper classes to write the results out to
big CSVs for importing into Kusto or the like but in general, you can do
whatever you want per package.

The custom logic to run on a per-package (or per catalog leaf/page) is referred
to as a **"driver"**.

The enumeration of the catalog is called a "catalog scan". The catalog scan is
within a specified time range in the catalog, with respect to the catalog commit
timestamp. A catalog scan finds all catalog leaves in the provided min and max
commit timestamp and then executes a "driver" for each package ID and version
found.

All work is executed in the context of an Azure Function that reads a single
worker queue (Azure Storage Queue).

The general flow of a catalog scan is:

1. Download the catalog index.
1. Find all catalog pages in the time range.
1. For each page, enumerate all leaf items per page in the time range.
1. For each leaf item, write the ID and version to Azure Table Storage to find
   the latest leaf.
1. After all leaf items have been written to Table Storage, enqueue one message
   per row.
1. For each queue message, execute the driver.

Note there is an option to disable step 4 and run the driver for every single
catalog leaf item. Depending on the logic of the driver, this may yield
duplicated effort and is often not desired.

The implementation is geared towards Azure Functions Consumption Plan for
compute (cheap) and Azure Storage for persistence (cheap).

### Workflow

The driver code is chained together with other operational tasks in a sequence
of steps called a **workflow**. The workflow is run on a regular cadence (e.g.
daily). The workflow performs these step for each iteration:

1. Run all catalog scans to read the latest information NuGet.org catalog.
   - Some catalog scans can run in parallel, others depend on each other.
1. Clean up orphan records.
   - Example of orphan record: a certificate that was only referenced by a
     package that was deleted.
1. Update auxiliary files.
   - These data sets contain some info about all packages in a single file.
1. Import the updates CSVs to Kusto (Azure Data Explorer).
   - This performs an import of all CSV blobs to new tables and then does an
     atomic table swap.

If any of these steps does not complete, the workflow hangs and no further
worflows can start.

## Main components

The main working components of NuGet Insights are **drivers** (process catalog leaves), other **message processors** (process queue messages), and the **admin panel**. The admin panel is the main purpose of the Website project ("the front end"). The drivers, other message processors, and timers for recurring workflows and metrics all run inside the Worker project ("the back end").

The current drivers for analyzing NuGet.org packages are documented in the [Drivers list](docs/drivers/README.md).

Several message processors exist to emit other useful data:

- [`DownloadsToCsv`](src/Worker.Logic/MessageProcessors/DownloadsToCsv/DownloadsToCsvUpdater.cs):
  read `downloads.v1.json` and write it to CSV
- [`OwnersToCsv`](src/Worker.Logic/MessageProcessors/OwnersToCsv/OwnersToCsvUpdater.cs):
  read `owners.v2.json` and write it to CSV
- [`VerifiedPackagesToCsv`](src/Worker.Logic/MessageProcessors/VerifiedPackagesToCsv/VerifiedPackagesToCsvUpdater.cs):
  read `verifiedPackages.json` and write it to CSV

Several message processors are used for aggregating or automating other
processes:

- [`CsvCompact`](src/Worker.Logic/MessageProcessors/CsvCompact/CsvCompactProcessor.cs):
  aggregate CSV records saved to Table Storage into partitioned CSV blobs
- [`KustoIngestion`](src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs):
  orchestrates ingestion and validation of CSV blobs into Kusto (Azure Data
  Explorer) tables
- [`CleanupOrphanRecords`](src/Worker.Logic/MessageProcessors/ReferenceTracking/CleanupOrphanRecordsProcessor.cs):
  removes records that are marked as orphans from the `ReferenceTracking` tables
- [`Workflow`](src/Worker.Logic/MessageProcessors/Workflow/WorkflowRunMessageProcessor.cs):
  orchestrates the entire workflow (as mentioned in the **Architecture** section
  above)

## Projects

Here's a high-level description of main projects in this repository:

- [`Worker`](src/Worker) - the Azure Function itself, a thin adapter between
  core logic and Azure Functions
- [`Website`](src/Website) - a website for an admin panel to managed scans
- [`Worker.Logic`](src/Worker.Logic) - all of the catalog scan and driver logic,
  this is the most interesting project
- [`Logic`](src/Logic) - contains more generic logic related to NuGet.org
  protocol and is mostly not directly related to distributed processing

Other projects are:

- [`Forks`](src/Forks) - download, patch, and list code from other open source
  projects
- [`SourceGenerator`](src/SourceGenerator) - AOT source generation logic for
  reading and writing CSVs
- [`Tool`](src/Tool) - a command-line app used for pretty much just prototyping
  code
