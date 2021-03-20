using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using NCrontab;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class FunctionsTest : BaseWorkerLogicIntegrationTest
    {
        public FunctionsTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public void TimerFunctionIsHalfOfMinimumFrequency()
        {
            var method = typeof(Functions).GetMethod(nameof(Functions.TimerAsync));
            var parameterAttributes = method
                .GetParameters()
                .SelectMany(x => x.CustomAttributes)
                .Where(x => x.AttributeType == typeof(TimerTriggerAttribute));

            var attribute = Assert.Single(parameterAttributes);
            var argument = Assert.Single(attribute.ConstructorArguments);
            var scheduleStr = Assert.IsType<string>(argument.Value);

            var schedule = CrontabSchedule.Parse(scheduleStr, new CrontabSchedule.ParseOptions
            {
                IncludingSeconds = true,
            });
            var nextOccurrences = schedule
                .GetNextOccurrences(new DateTime(2021, 1, 1), new DateTime(2021, 2, 1))
                .Take(2)
                .ToList();
            var delta = nextOccurrences[1] - nextOccurrences[0];

            var timers = Host.Services.GetRequiredService<IEnumerable<ITimer>>();
            var minFrequency = timers.Min(x => x.Frequency);

            Assert.Equal(minFrequency / 2, delta);
        }
    }
}
