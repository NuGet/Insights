using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Knapcode.ExplorePackages
{
    public static class AssemblyExtensions
    {
        public static IEnumerable<(Type serviceType, Type implementationType)> GetClassesImplementingGeneric(this Assembly assembly, Type openType)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType))
            {
                foreach (var i in type.GetInterfaces())
                {
                    if (!i.IsConstructedGenericType)
                    {
                        continue;
                    }

                    var genericType = i.GetGenericTypeDefinition();
                    if (genericType != openType)
                    {
                        continue;
                    }

                    yield return (i, type);
                }
            }
        }

        public static IEnumerable<Type> GetClassesImplementing(this Assembly assembly, Type type)
        {
            return assembly
                .GetTypes()
                .Where(t => t.GetInterfaces().Contains(type))
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType);
        }

        public static IEnumerable<Type> GetClassesImplementing<T>(this Assembly assembly)
        {
            return assembly.GetClassesImplementing(typeof(T));
        }
    }
}
