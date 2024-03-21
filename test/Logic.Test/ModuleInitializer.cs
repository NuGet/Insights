// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;
using Argon;
using DiffEngine;
using EmptyFiles;

namespace NuGet.Insights
{
    public static class ModuleInitializer
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            // don't automatically open diff tools, like Beyond Compare
            DiffRunner.Disabled = true;

            // remove CSV and JSON from the known text file list so we can handle encoding ourself
            FileExtensions.RemoveTextExtensions("csv", "json");

            // enable more succinct failure messages
            VerifyDiffPlex.Initialize();

            // store all snapshots in a subdirectory
            /*
            DerivePathInfo(
                (sourceFile, projectDirectory, type, method) =>
                {
                    var sourceDir = Path.GetDirectoryName(sourceFile);

                    if (!sourceDir.StartsWith(projectDirectory, StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"The source file directory '{sourceDir}' should start with the project directory '{projectDirectory}'.", nameof(sourceFile));
                    }

                    var sourceRelativeDir = sourceDir.Substring(projectDirectory.Length);
                    var snapshotDir = Path.Combine(projectDirectory, "TestData", sourceRelativeDir);

                    return new(
                        directory: snapshotDir,
                        typeName: type.Name,
                        methodName: method.Name);
                });
            */

            VerifierSettings.SortPropertiesAlphabetically();
            VerifierSettings.AutoVerify(includeBuildServer: false);

            // serialize objects as real JSON
            VerifierSettings.UseStrictJson();

            VerifierSettings.DontSortDictionaries();
            VerifierSettings.DontScrubDateTimes();
            VerifierSettings.DontScrubGuids();
            VerifierSettings.DontIgnoreEmptyCollections();

            VerifierSettings.UseUtf8NoBom();

            VerifierSettings.AddExtraSettings(x => x.Converters.Add(new CsvRecordConverter()));
        }

        private class CsvRecordConverter : WriteOnlyJsonConverter<ICsvRecord>
        {
            private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> TypeToProperties = new();

            public override void Write(VerifyJsonWriter writer, ICsvRecord record)
            {
                var properties = TypeToProperties.GetOrAdd(
                    record.GetType(),
                    type => type
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(x => x.GetMethod is not null && x.SetMethod is not null)
                        .OrderBy(x => x.Name, StringComparer.Ordinal)
                        .ToList());

                writer.WriteStartObject();

                foreach (var property in properties)
                {
                    var value = property.GetValue(record);
                    if (value is null || Equals(string.Empty, value)) // empty and null string are equivalent in CSV
                    {
                        continue;
                    }

                    writer.WritePropertyName(property.Name);

                    var kustoTypeAttribute = property.GetCustomAttribute<KustoTypeAttribute>();
                    if (kustoTypeAttribute is not null && property.PropertyType == typeof(string))
                    {
                        writer.Serialize(JToken.Parse((string)value));
                        continue;
                    }

                    writer.Serialize(value);
                }

                writer.WriteEndObject();
            }
        }
    }
}
