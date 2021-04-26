using System;

namespace Knapcode.ExplorePackages
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
