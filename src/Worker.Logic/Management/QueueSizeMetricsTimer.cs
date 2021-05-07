using System;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public class QueueSizeMetricsTimer : ITimer
    {
        private readonly IRawMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;

        public QueueSizeMetricsTimer(
            IRawMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient)
        {
            _messageEnqueuer = messageEnqueuer;
            _telemetryClient = telemetryClient;
        }

        public string Name => "QueueSizeMetrics";
        public TimeSpan Frequency => TimeSpan.FromSeconds(30);
        public bool AutoStart => true;
        public bool IsEnabled => true;
        public int Precedence => default;

        public async Task<bool> ExecuteAsync()
        {
            await Task.WhenAll(Enum
                .GetValues(typeof(QueueType))
                .Cast<QueueType>()
                .SelectMany(x => new[]
                {
                    EmitQueueSizeAsync(_messageEnqueuer.GetApproximateMessageCountAsync(x), x, isPoison: false),
                    EmitQueueSizeAsync(_messageEnqueuer.GetPoisonApproximateMessageCountAsync(x), x, isPoison: true),
                })
                .ToList());
            return true;
        }

        private async Task EmitQueueSizeAsync(Task<int> countAsync, QueueType queue, bool isPoison)
        {
            var count = await countAsync;
            var metric = _telemetryClient.GetMetric("StorageQueueSize", "QueueType", "IsPoison");
            metric.TrackValue(count, queue.ToString(), isPoison ? "true" : "false");
        }

        public async Task InitializeAsync()
        {
            await _messageEnqueuer.InitializeAsync();
        }

        public Task<bool> IsRunningAsync()
        {
            return Task.FromResult(false);
        }
    }
}
