// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Licenses;
using NuGet.Versioning;

#nullable enable

namespace NuGet.Insights.Worker
{
    public static class KustoDynamicSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(),

                new StringDictionaryConverterFactory(),

                new NuGetFrameworkConverter(),
                new VersionRangeConverter(),
                new LicenseMetadataConverter(),
                new FrameworkSpecificGroupConverter(),
                new FrameworkReferenceGroupConverter(),
                new PackageDependencyGroupConverter(),

                new NuGetLicenseExpressionConverter(),
            },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    SortObjectProperties
                }
            }
        };

        public static string? Serialize<T>(T? data) where T : class
        {
            if (data is null)
            {
                return null;
            }

            return JsonSerializer.Serialize(data, Options);
        }

        private static bool TryGetMatchingInterface(Type type, Type genericInterface, [NotNullWhen(true)] out Type? matchingInterface)
        {
            if (!genericInterface.IsGenericType || !genericInterface.IsInterface)
            {
                throw new ArgumentException("The generic interface parameter must be a generic interface.", nameof(genericInterface));
            }

            var isInterface = type.IsGenericType && type.GetGenericTypeDefinition() == genericInterface;
            if (isInterface)
            {
                matchingInterface = type;
                return true;
            }

            matchingInterface = type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == genericInterface);
            if (matchingInterface is not null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// This method may throw exceptions in supported scenarios, but swallowed by System.Text.Json. One such case
        /// is when a string is stored as an object and System.Text.Json needs to evaluate polymorphism options. This
        /// leads to all of System.String's interfaces getting passed in here, such as IEnumerable which will be
        /// rejected. We apply <see cref="DebuggerHiddenAttribute"/> to avoid the debugger breaking on these exceptions
        /// and stalling normal execution.
        /// </summary>
        [DebuggerHidden]
        private static void SortObjectProperties(JsonTypeInfo info)
        {
            // These should be handled by a converter.
            if (info.Kind == JsonTypeInfoKind.Dictionary)
            {
                throw new NotImplementedException($"Dictionaries should be serialized using the Dictionary<string, T> converter. The type {info.Type.FullName} will not be serialized.");
            }

            // No unordered sequences.
            if (info.Kind == JsonTypeInfoKind.Enumerable)
            {
                if (!TryGetMatchingInterface(info.Type, typeof(IReadOnlyList<>), out _))
                {
                    throw new NotImplementedException($"Enumerables must implement IReadOnlyList<T>, i.e. they must have a defined order. The type {info.Type.FullName} will not be serialized.");
                }
            }

            if (info.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            var properties = new List<JsonPropertyInfo>(info.Properties);
            properties.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));

            info.Properties.Clear();
            for (var i = 0; i < properties.Count; i++)
            {
                info.Properties.Add(properties[i]);
            }
        }

        private class NuGetFrameworkConverter : JsonConverter<NuGetFramework>
        {
            public override NuGetFramework Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, NuGetFramework value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.GetShortFolderName());
            }
        }

        private class VersionRangeConverter : JsonConverter<VersionRange>
        {
            public override VersionRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, VersionRange value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToNormalizedString());
            }
        }

        private class LicenseMetadataConverter : JsonConverter<LicenseMetadata>
        {
            public override LicenseMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, LicenseMetadata value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, new
                {
                    value.Type,
                    value.License,
                    value.WarningsAndErrors,
                    value.Version,
                    value.LicenseUrl,
                }, options);
            }
        }

        private class FrameworkReferenceGroupConverter : JsonConverter<FrameworkReferenceGroup>
        {
            public override FrameworkReferenceGroup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, FrameworkReferenceGroup value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, new
                {
                    value.TargetFramework,
                    FrameworkReferences = value.FrameworkReferences.ToList(),
                }, options);
            }
        }

        private class FrameworkSpecificGroupConverter : JsonConverter<FrameworkSpecificGroup>
        {
            public override FrameworkSpecificGroup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, FrameworkSpecificGroup value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, new
                {
                    value.TargetFramework,
                    Items = value.Items.ToList(),
                }, options);
            }
        }

        private class PackageDependencyGroupConverter : JsonConverter<PackageDependencyGroup>
        {
            public override PackageDependencyGroup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, PackageDependencyGroup value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, new
                {
                    value.TargetFramework,
                    Packages = value.Packages.ToList(),
                }, options);
            }
        }

        private class NuGetLicenseExpressionConverter : JsonConverter<NuGetLicenseExpression>
        {
            public override NuGetLicenseExpression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, NuGetLicenseExpression value, JsonSerializerOptions options)
            {
                switch (value)
                {
                    case WithOperator withOperator:
                        JsonSerializer.Serialize(writer, withOperator, options);
                        break;
                    case NuGetLicense license:
                        JsonSerializer.Serialize(writer, license, options);
                        break;
                    case LogicalOperator logicalOperator:
                        JsonSerializer.Serialize(writer, logicalOperator, options);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private class StringDictionaryConverterFactory : JsonConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                if (TryGetMatchingInterface(typeToConvert, typeof(IReadOnlyDictionary<,>), out _)
                    || TryGetMatchingInterface(typeToConvert, typeof(IDictionary<,>), out _))
                {
                    return true;
                }

                return false;
            }

            public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            {
                object? converter;
                if (TryGetMatchingInterface(typeToConvert, typeof(IReadOnlyDictionary<,>), out var matching))
                {
                    var valueType = matching.GetGenericArguments()[1];
                    converter = Activator.CreateInstance(typeof(StringReadOnlyDictionaryConverter<>).MakeGenericType(valueType));
                }
                else if (TryGetMatchingInterface(typeToConvert, typeof(IDictionary<,>), out matching))
                {
                    var valueType = matching.GetGenericArguments()[1];
                    converter = Activator.CreateInstance(typeof(StringDictionaryConverter<>).MakeGenericType(valueType));
                }
                else
                {
                    throw new NotImplementedException();
                }

                return (JsonConverter)converter!;
            }
        }

        private class StringReadOnlyDictionaryConverter<T> : JsonConverter<IReadOnlyDictionary<string, T>>
        {
            public override Dictionary<string, T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, T> value, JsonSerializerOptions options)
            {
                WriteStringValuePairs(writer, value, options);
            }
        }

        private class StringDictionaryConverter<T> : JsonConverter<IDictionary<string, T>>
        {
            public override Dictionary<string, T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, IDictionary<string, T> value, JsonSerializerOptions options)
            {
                WriteStringValuePairs(writer, value, options);
            }
        }

        private static void WriteStringValuePairs<T>(Utf8JsonWriter writer, IEnumerable<KeyValuePair<string, T>> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var pair in value.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(pair.Key);
                JsonSerializer.Serialize(writer, pair.Value, options);
            }

            writer.WriteEndObject();
        }
    }
}
