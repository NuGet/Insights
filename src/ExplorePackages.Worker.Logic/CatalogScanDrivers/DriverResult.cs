using System;

namespace Knapcode.ExplorePackages.Worker
{
    public class DriverResult
    {
        internal DriverResult(DriverResultType type)
        {
            Type = type;
        }

        public DriverResultType Type { get; }

        public static DriverResult Success() => new DriverResult(DriverResultType.Success);
        public static DriverResult<T> Success<T>(T value) => new DriverResult<T>(DriverResultType.Success, value);
        public static DriverResult TryAgainLater() => new DriverResult(DriverResultType.TryAgainLater);
        public static DriverResult<T> TryAgainLater<T>() => new DriverResult<T>(DriverResultType.TryAgainLater, default);
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
