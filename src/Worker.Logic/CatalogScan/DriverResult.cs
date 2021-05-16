using System;

namespace NuGet.Insights.Worker
{
    public class DriverResult
    {
        internal DriverResult(DriverResultType type)
        {
            Type = type;
        }

        public DriverResultType Type { get; }

        public static DriverResult Success()
        {
            return new DriverResult(DriverResultType.Success);
        }

        public static DriverResult<T> Success<T>(T value)
        {
            return new DriverResult<T>(DriverResultType.Success, value);
        }

        public static DriverResult TryAgainLater()
        {
            return new DriverResult(DriverResultType.TryAgainLater);
        }

        public static DriverResult<T> TryAgainLater<T>()
        {
            return new DriverResult<T>(DriverResultType.TryAgainLater, default);
        }
    }

    public class DriverResult<T> : DriverResult
    {
        private readonly T _value;

        internal DriverResult(DriverResultType type, T value) : base(type)
        {
            _value = value;
        }

        public T Value => Type == DriverResultType.Success ? _value : throw new InvalidOperationException($"No value available. Result type is {Type}.");
    }
}
