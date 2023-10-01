# NuGet Insights

**Analyze NuGet.org packages 📦 using Azure Functions ⚡.**

This project enables you to write a bit of code that will be executed for each
package on NuGet.org in parallel. The results of the code will be collected into
CSV files stored Azure Blob Storage. These CSV files can be imported into any
query system you want, for easy analysis. This project is about building those
CSV blobs in a fast, scalable, and reproducible way as well as keeping those
files up to date.

The data sets are great for:

- 🔎 Ad-hoc investigations of the .NET ecosystem
- 🐞 Estimate the blast radius of a bug affecting NuGet packages
- 📈 Check the trends over time on NuGet.org
- 📊 Look at adoption of various NuGet or .NET features

The data sets currently produced by NuGet Insights are listed in
[`docs/tables/README.md`](docs/tables/README.md#tables).

## Quickstart

**We follow a 3 step process to go from nothing to a completely deployed Azure
solution.**

1. Build the code
3. Deploy to Azure
4. Start analysis from the admin panel

### Build the code

1. Ensure you have the .NET 7 SDK installed. [Install it if
   needed](https://dotnet.microsoft.com/download).
   ```
   dotnet --info
   ```
2. Clone the repository.
   ```
   git clone https://github.com/NuGet/Insights.git
   ```
3. Run `dotnet publish` on the website and worker projects. This produces
   compiled directories that can be deployed to Azure later.
   ```
   cd Insights
   dotnet publish src/Worker -c Release
   dotnet publish src/Website -c Release
   ```

### Deploy to Azure

PowerShell is used for the following steps. I have tested Windows PowerShell
5.1, Windows PowerShell 7.3.7, and Linux PowerShell 7.3.7.

1. Ensure you have the Az PowerShell modules. [Install them if
   needed](https://docs.microsoft.com/en-us/powershell/azure/install-az-ps).
   ```powershell
   Connect-AzAccount
   ```
1. Ensure you have Bicep installed. [Install it if
   needed](https://docs.microsoft.com/azure/azure-resource-manager/bicep/install).
   ```
   bicep --version
   ```
1. Ensure you have the desired Azure subscription selected.
   ```powershell
   Set-AzContext -Subscription $mySubscriptionId
   ```
1. From the root of the repo, deploy with the desired [config](deploy/config)
   and stamp name.
   ```powershell
   ./deploy/deploy.ps1 -ConfigName dev -StampName Joel -AllowDeployUser
   ```
   If you run into trouble, try adding the `-Debug` option to get more
   diagnostic information.

This will create a new resource group with name `NuGet.Insights-{StampName}`
deploy several resources into it including:
- an App Service, containing a website for starting scans
- a Function App with Consumption plan, for running the scans
- a Storage account, for maintaining intermediate state and results (CSV files)
- an Application Insights instance, for investigating metrics and error logs
- a Key Vault for auto-rotating the storage access key

### Start analysis from the admin panel

When the deployment completes successfully, a "website URL" will be reporting in
the console as part of a warm-up. You can use this to access the admin panel.
The end of the output looks like this:

<pre>
...
Warming up the website and workers...
<b>https://nugetinsights-joel.azurewebsites.net/</b> - 200 OK
https://nugetinsights-joel-worker-0.azurewebsites.net/ - 200 OK
Deployment is complete.
Go to here for the admin panel: https://nugetinsights-joel.azurewebsites.net/Admin
</pre>

You can go the first URL which is the website URL in your web browser and click on
the **Admin** link in the nav bar. Then, you can start a short run using the
"All catalog scans" section, "Use custom cursor" checkbox, and "Start all" button.

For more information about running catalog scans, see [Starting a catalog
scan](#starting-a-catalog-scan).

## Running locally

Use one of the following approaches to run Insights locally. [Using Project
Tye](#using-project-tye) is the easiest if you have Docker installed, otherwise
[use a standalone Azure Storage
emulator](#using-a-standalone-azure-storage-emulator).

### Using Project Tye

From Project Tye's GitHub page:

> Tye is a developer tool that makes developing, testing, and deploying
> microservices and distributed applications easier. Project Tye includes a
> local orchestrator to make developing microservices easier and the ability to
> deploy microservices to Kubernetes with minimal configuration.

It's a great way to run the Insights website, worker, and the Azurite storage
emulator all at once with a single command.

1. Clone the Insights repository.
2. [Install Project
   Tye](https://github.com/dotnet/tye/blob/main/docs/getting_started.md#installing-tye)
   if you haven't already. 
3. Make sure you have Docker installed since it is used for running Azurite.
4. Execute `tye run` in the root of the repository.
5. Open the Tye dashboard using the URL printed to stdout, e.g.
   ```
   Dashboard running on http://127.0.0.1:8000
   ```
6. From the Tye dashboard, you can navigate to the website URL (shown in the
   **Bindings**).

Proceed to the [Starting a catalog scan](#starting-a-catalog-scan) section.

### Using a standalone Azure Storage emulator

1. Clone the repository.
2. Install and start an Azure Storage emulator for Blob, Queue, and Table
   storage.
   - [Azurite](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azurite):
     can run from VS Code, npm, and more; **make sure to use version 3.19.0 or
     newer**.
   - [Azure Storage
     Emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator):
     this emulator only works on Windows and is deprecated.
3. Execute `dotnet run --project src/Worker` from the root of the repository.
3. From another terminal window, run `dotnet run --project src/Website` from the
   root of the repository.
   - The website and the worker don't necessarily need to run in parallel, but
     it's easier to watch the progress if you leave both running.
4. Open the website URL printed to stdout, e.g.
   ```
   Now listening on: http://localhost:60491
   ```

Proceed to the [Starting a catalog scan](#starting-a-catalog-scan) section.

### Starting a catalog scan

A **catalog scan** is a unit of work for Insights which runs analysis against
all of the packages published during some time range. The time range for a
catalog scan is bounded by the a previous catalog stamp used
(as an exclusive minimum) and an arbitrary timestamp to process up to (as an
inclusive maximum). For more information, see the [architecture
section](#architecture).

Once you have opened the localhost website URL mentioned in the section above, follow these
steps to start your first catalog scan from the Insights admin panel.

1. In your web browser, viewing the website URL, click on the "Admin" link in
   the navigation bar.
2. Start one or more catalog scans.
   - For your first try, run a single driver against a single [NuGet V3
     catalog](https://docs.microsoft.com/en-us/nuget/api/catalog-resource)
     commit.
     - Expand the **Load package archive** section.
     - Check **Use custom cursor**.
     - Use the default value of `2015-02-01T06:22:45.8488496Z`, which is the
       very first commit timestamp in the NuGet V3 catalog.
     - Click **Start**.
   - You can start all of the catalog scans with the same timestamp using the
     "All catalog scans" section but this will take many hours while running on
     your local machine. There are a lot of drivers and a lot of packages on
     NuGet.org 😉.
3. Make sure the background worker is running (either via Tye or starting the
   Worker project from the terminal).
4. Wait until the catalog scan is done. You can check the current progress by
   refreshing the admin panel and looking at the number of messages in the
   queues (first section in the admin panel) or by looking at the catalog scan
   record created in the previous step.

If you ran a driver like **Load package archive**, data will be populated into
your Azure Table Storage emulator in the `packagearchives` table. If you ran a
driver like **Package asset to CSV**, CSV files will be populated into your
Azure Blob Storage emulator in the `packageassets` container. For more information on what each driver does, see the [drivers list](docs/drivers/README.md).

You can use the [Azure Storage
Explorer](https://azure.microsoft.com/en-us/products/storage/storage-explorer/)
to interact with your Azure Storage endpoints (either the storage emulator
running locally or in Azure).

When running locally, you can check the application logs shown in the Tye
dashboard or terminal stdout. When running in Azure, you can use Application
Insights (note the default logging is Warning or higher to reduce cost). You can
also look at the Azure Queue Storage queues to understand what sort of work the
Worker has left.

## Documentation

- **[Tables](docs/tables/README.md) - documentation for all of the data tables
  produced by this project**
- **[Adding a new driver](docs/new-driver.md) - a guide to help you enhance
  Insights to suit your needs**
- [Drivers](docs/drivers/README.md) - the list of existing drivers
- [Reusable classes](docs/reusable-classes.md) - interesting or useful classes
  or concepts supporting this project
- [Blog posts](docs/blog-posts.md) - blog posts about lessons learned from this
  project
- [Cost](docs/cost.md) - approximately how much it costs to run several of the
  implemented catalog scans

## Architecture

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

## Screenshots

### Resources in Azure

These are what the resources look like in Azure after deployment.

![Azure resources](docs/azure-resources.png)

### Azure Function running locally

This is what the Azure Function looks like running locally, for the Package
Manifest to CSV driver.

![Azure Function running locally](docs/local-azure-function.png)

### Results running locally

This is what the results look like in Azure Table Storage. Each row is a package
.nuspec stored as compressed MessagePack bytes.

![Results running locally](docs/local-results.png)

### Admin panel

This is what the admin panel looks like to start catalog scans.

![Admin panel](docs/admin-panel.png)

### Load Package Archive

This is the driver that reads the file list and package signature from all NuGet
packages on NuGet.org and loads them into Azure Table Storage. It took about 35
minutes to do this and costed about $3.37.

#### Azure Functions Execution Count

![Azure Functions Execution Count](docs/find-package-files-exucution-count.png)

#### Azure Functions Execution Count

![Azure Functions Execution Units](docs/find-package-files-execution-units.png)

## Trademarks

This project may contain trademarks or logos for projects, products, or
services. Authorized use of Microsoft trademarks or logos is subject to and must
follow Microsoft's Trademark & Brand Guidelines. Use of Microsoft trademarks or
logos in modified versions of this project must not cause confusion or imply
Microsoft sponsorship. Any use of third-party trademarks or logos are subject to
those third-party's policies.
