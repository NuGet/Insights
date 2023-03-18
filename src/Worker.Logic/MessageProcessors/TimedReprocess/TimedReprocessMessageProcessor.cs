// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessMessageProcessor : IMessageProcessor<TimedReprocessMessage>
    {
        private readonly TimedReprocessStorageService _storageService;
        private readonly IMessageEnqueuer _messageEnqueuer;

        public TimedReprocessMessageProcessor(
            TimedReprocessStorageService storageService,
            IMessageEnqueuer messageEnqueuer)
        {
            _storageService = storageService;
            _messageEnqueuer = messageEnqueuer;
        }

        public async Task ProcessAsync(TimedReprocessMessage message, long dequeueCount)
        {
            var run = await _storageService.GetRunAsync(message.RunId);
            if (run == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(dequeueCount * 15));
                throw new InvalidOperationException($"An incomplete timed reprocess run for {message.RunId} should have already been created.");
            }

            if (run.State == TimedReprocessState.Created)
            {
                run.Started = DateTimeOffset.UtcNow;
                run.State = TimedReprocessState.Initializing;
                await _storageService.ReplaceRunAsync(run);
            }

            if (run.State == TimedReprocessState.Initializing)
            {
                var buckets = await _storageService.GetBucketsToReprocessAsync();
                await _storageService.MarkBucketsAsProcessedAsync(buckets);

                run.State = TimedReprocessState.Working;
                await _storageService.ReplaceRunAsync(run);

                message.AttemptCount++;
                await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                return;
            }

            if (run.State == TimedReprocessState.Working)
            {
                run.State = TimedReprocessState.Finalizing;
                await _storageService.ReplaceRunAsync(run);

                message.AttemptCount++;
                await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                return;
            }

            if (run.State == TimedReprocessState.Finalizing)
            {
                run.Completed = DateTimeOffset.UtcNow;
                run.State = TimedReprocessState.Complete;
                await _storageService.ReplaceRunAsync(run);
            }
        }
    }
}
