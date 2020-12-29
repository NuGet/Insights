using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableRowCopyMessageProcessor<T> : IMessageProcessor<TableRowCopyMessage<T>> where T : ITableEntity, new()
    {
        private readonly ServiceClientFactory _serviceClientFactory;

        public TableRowCopyMessageProcessor(ServiceClientFactory serviceClientFactory)
        {
            _serviceClientFactory = serviceClientFactory;
        }

        public async Task ProcessAsync(TableRowCopyMessage<T> message, int dequeueCount)
        {
            var sourceTable = _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(message.SourceTableName);

            var destinationTable = _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(message.DestinationTableName);

            var sourceRecord = await sourceTable.RetrieveAsync<T>(message.PartitionKey, message.RowKey);

            await destinationTable.InsertOrReplaceAsync(sourceRecord);
        }
    }
}
