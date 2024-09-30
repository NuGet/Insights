// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class FanOutRecoveryService
    {
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly IMetric _unstartedWorkCount;

        public FanOutRecoveryService(
            IMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _messageEnqueuer = messageEnqueuer;
            _telemetryClient = telemetryClient;
            _options = options;

            _unstartedWorkCount = _telemetryClient.GetMetric($"{nameof(FanOutRecoveryService)}.UnstartedWorkCount", "Type");
        }

        public async Task EnqueueUnstartedWorkAsync<T>(
            Func<int, Task<IReadOnlyList<T>>> getWorkAsync,
            Func<IReadOnlyList<T>, Task> enqueueWorkAsync)
        {
            var unstartedWork = await getWorkAsync(StorageUtility.MaxTakeCount);
            _unstartedWorkCount.TrackValue(unstartedWork.Count, typeof(T).Name);

            if (unstartedWork.Count > 0)
            {
                await enqueueWorkAsync(unstartedWork);
            }
        }

        public async Task<bool> ShouldRequeueAsync(DateTimeOffset lastProgress, params Type[] messageTypes)
        {
            // FAN OUT RECOVERY: it is possible for page scans from a duplicate catalog index scan message to have been
            // added after the enqueue state completed. To handle this rare case, we will try to reprocess any page
            // scans that are still hanging. This prevents us from being stuck in the working state waiting on page
            // scans that have no corresponding catalog page scan queue message.
            var workingDuration = DateTimeOffset.UtcNow - lastProgress;
            if (workingDuration >= _options.Value.FanOutRequeueTime)
            {
                var messageCount = await _messageEnqueuer.GetMaxApproximateMessageCountAsync(messageTypes);

                if (messageCount <= _options.Value.FanOutRequeueMaxMessageCount)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
