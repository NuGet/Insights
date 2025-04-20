// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NCrontab;

namespace NuGet.Insights
{
    public class TimerFrequency
    {
        public TimerFrequency(TimeSpan timeSpan)
        {
            TimeSpan = timeSpan;
        }

        public TimerFrequency(CrontabSchedule schedule)
        {
            Schedule = schedule;
        }

        public TimeSpan? TimeSpan { get; }
        public CrontabSchedule? Schedule { get; }

        public static TimerFrequency Parse(string frequency)
        {
            if (System.TimeSpan.TryParse(frequency, out var timeSpan))
            {
                return new TimerFrequency(timeSpan);
            }

            var includingSeconds = frequency.Split(" ", StringSplitOptions.RemoveEmptyEntries).Length > 5;
            var schedule = CrontabSchedule.TryParse(frequency, new CrontabSchedule.ParseOptions { IncludingSeconds = includingSeconds });
            if (schedule is not null)
            {
                return new TimerFrequency(schedule);
            }

            throw new ArgumentException($"Invalid frequency: {frequency}. It must be a TimeSpan or a valid NCrontab schedule expression.");
        }

        public override string ToString()
        {
            return TimeSpan?.ToString() ?? Schedule!.ToString();
        }
    }
}
