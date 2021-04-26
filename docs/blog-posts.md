# Blog posts

I've written several blog posts based on findings in this project:

- [Disk write performance on Azure Functions](https://www.joelverhagen.com/blog/2021/02/azure-function-disk-performance) - use the Azure File Share efficiently in Consumption plan
  - Recommendations are implemented in [`TempStreamWriter.cs`](../src/Logic/TempStream/TempStreamWriter.cs).
- [How to run a distributed scan of Azure Table Storage](https://www.joelverhagen.com/blog/2020/12/distributed-scan-of-azure-tables) - 10 minute Azure Functions limit and big Table Storage
  - This trick is implemented in [`TablePrefixScan`](../src/Logic/TablePrefixScan).
- [The fastest CSV parser in .NET](https://www.joelverhagen.com/blog/2020/12/fastest-net-csv-parsers) - comparing the performance of .NET CSV parsers on NuGet.org
  - I used one of the initial fastest implementations in [`NRecoCsvReader`](../src/Worker.Logic/AppendResults/NRecoCsvReader.cs).
- [The fastest way to dynamically activate objects in .NET](https://www.joelverhagen.com/blog/2020/11/dynamically-activate-objects-net) - ILEmit vs. `new T()` vs. `Activator`, etc.
  - I used the fastest approach allowing generics in [`NRecoCsvReader`](../src/Worker.Logic/AppendResults/NRecoCsvReader.cs), which is tied between `new T()` and `Activator`. I didn't use ILEmit since the overhead was too high for Azure Functions.
