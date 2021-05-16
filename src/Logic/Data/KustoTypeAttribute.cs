using System;

namespace NuGet.Insights
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class KustoTypeAttribute : Attribute
    {
        public KustoTypeAttribute(string type)
        {
            KustoType = type;
        }

        public string KustoType { get; }
    }
}
