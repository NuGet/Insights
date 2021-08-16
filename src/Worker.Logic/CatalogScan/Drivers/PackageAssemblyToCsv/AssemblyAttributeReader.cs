// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    public record AssemblyAttributes(
        bool AreTruncated,
        bool HaveMethodDefinitions,
        bool HaveTypeDefinitionConstructors,
        bool HaveDuplicateArgumentNames,
        int TotalCount,
        int TotalDataLength,
        IDictionary<string, List<IDictionary<string, object>>> NameToParameters,
        ISet<string> FailedDecode);

    /// <summary>
    /// Inspired by Bruno Garcia's UnoptimizedAssemblyDetector.
    /// Source: https://github.com/bruno-garcia/unoptimized-assembly-detector/blob/main/src/UnoptimizedAssemblyDetector/DetectUnoptimizedAssembly.cs
    /// This code attempts to capture a serializable representation of an assemblies custom attributes.
    /// </summary>
    public static class AssemblyAttributeReader
    {
        public static AssemblyAttributes Read(MetadataReader metadata, PackageAssembly assembly, ILogger logger)
        {
            var nameToArguments = new SortedDictionary<string, List<IDictionary<string, object>>>(StringComparer.Ordinal);
            var failedDecode = new SortedSet<string>(StringComparer.Ordinal);
            var areTruncated = false;
            var haveMethodDefinitions = false;
            var haveTypeDefinitionConstructors = false;
            var haveDuplicateArgumentNames = false;
            var totalCount = 0;
            var addedAttributeLength = 0;
            var totalValueLength = 0;

            const int maxAttributeCount = 32;
            const int maxAttributeNameLength = 128;
            const int maxAttributeValueLength = 2048;

            foreach (var customAttributeHandle in metadata.GetAssemblyDefinition().GetCustomAttributes())
            {
                totalCount++;

                var attribute = metadata.GetCustomAttribute(customAttributeHandle);
                string attributeName;
                if (attribute.Constructor.Kind == HandleKind.MemberReference)
                {
                    var memberReference = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                    if (memberReference.Parent.Kind == HandleKind.TypeReference)
                    {
                        var typeReference = metadata.GetTypeReference((TypeReferenceHandle)memberReference.Parent);
                        attributeName = metadata.GetString(typeReference.Name);
                    }
                    else if (memberReference.Parent.Kind == HandleKind.TypeDefinition)
                    {
                        haveTypeDefinitionConstructors = true;
                        var typeDefinition = metadata.GetTypeDefinition((TypeDefinitionHandle)memberReference.Parent);
                        attributeName = metadata.GetString(typeDefinition.Name);
                    }
                    else
                    {
                        throw new NotImplementedException("Encountered unexpected attribute constructor parent handle: " + memberReference.Parent.Kind);
                    }
                }
                else if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
                {
                    haveMethodDefinitions = true;
                    var methodDefinition = metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                    var methodName = metadata.GetString(methodDefinition.Name);
                    var typeDefinitionHandle = methodDefinition.GetDeclaringType();
                    var typeDefinition = metadata.GetTypeDefinition(typeDefinitionHandle);
                    attributeName = metadata.GetString(typeDefinition.Name);
                    if (methodName != ".ctor")
                    {
                        throw new NotImplementedException("Encountered unexpected method name: " + methodName);
                    }
                }
                else
                {
                    throw new NotImplementedException("Encountered unexpected attribute handle: " + attribute.Constructor.Kind);
                }

                const string suffix = "Attribute";
                if (attributeName.EndsWith(suffix, StringComparison.Ordinal))
                {
                    attributeName = attributeName.Substring(0, attributeName.Length - suffix.Length);
                }

                void RecordFailedDecode()
                {
                    if (failedDecode.Count < maxAttributeCount && attributeName.Length <= maxAttributeNameLength)
                    {
                        failedDecode.Add(attributeName);
                    }
                    else
                    {
                        areTruncated = true;
                    }
                }

                BlobReader blobReader;
                try
                {
                    blobReader = metadata.GetBlobReader(attribute.Value);
                }
                catch (BadImageFormatException ex)
                {
                    RecordFailedDecode();
                    logger.LogInformation(
                        ex,
                        "Package {Id} {Version} could not get blob reader for custom attribute {Name}. Path: {Path}",
                        assembly.Id,
                        assembly.Version,
                        attributeName,
                        assembly.Path);
                    continue;
                }
                
                var attributeValueLength = blobReader.Length;
                totalValueLength += attributeValueLength;

                if (nameToArguments.Count > maxAttributeCount // Don't record too many attributes
                    || attributeName.Length > maxAttributeNameLength // Don't record an attribute that's name is too long
                    || addedAttributeLength + attributeValueLength > maxAttributeValueLength // Don't record an attribute if the data exceeds the exceeds to total max
                    || failedDecode.Contains(attributeName)) // Don't check an attribute if the attribute failed to decode already for this assembly
                {
                    areTruncated = true;
                    continue;
                }

                CustomAttributeValue<object> value;
                try
                {
                    value = attribute.DecodeValue(TypelessDecoder.Instance);
                }
                catch (BadImageFormatException ex)
                {
                    RecordFailedDecode();
                    logger.LogInformation(
                        ex,
                        "Package {Id} {Version} could not decode custom attribute {Name}. Path: {Path}",
                        assembly.Id,
                        assembly.Version,
                        attributeName,
                        assembly.Path);
                    continue;
                }

                var arguments = new SortedDictionary<string, object>(StringComparer.Ordinal);
                for (var i = 0; i < value.FixedArguments.Length; i++)
                {
                    arguments.Add(i.ToString(), value.FixedArguments[i].Value);
                }
                foreach (var argument in value.NamedArguments)
                {
                    if (!arguments.ContainsKey(argument.Name))
                    {
                        arguments.Add(argument.Name, argument.Value);
                    }
                    else
                    {
                        haveDuplicateArgumentNames = true;
                    }
                }

                if (!nameToArguments.TryGetValue(attributeName, out var argumentList))
                {
                    argumentList = new List<IDictionary<string, object>>();
                    nameToArguments.Add(attributeName, argumentList);
                }

                argumentList.Add(arguments);

                addedAttributeLength += attributeValueLength;
            }

            return new AssemblyAttributes(
                areTruncated,
                haveMethodDefinitions,
                haveTypeDefinitionConstructors,
                haveDuplicateArgumentNames,
                totalCount,
                totalValueLength,
                nameToArguments,
                failedDecode);
        }

        private class TypelessDecoder : ICustomAttributeTypeProvider<object>
        {
            public static TypelessDecoder Instance { get; } = new TypelessDecoder();

            public object GetPrimitiveType(PrimitiveTypeCode typeCode) => null;
            public object GetSystemType() => null;
            public object GetSZArrayType(object elementType) => null;
            public object GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => null;
            public object GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => null;
            public object GetTypeFromSerializedName(string name) => null;
            public PrimitiveTypeCode GetUnderlyingEnumType(object type) => PrimitiveTypeCode.Int32;
            public bool IsSystemType(object type) => false;
        }
    }
}
