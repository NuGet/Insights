// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml;
using System.Xml.Linq;

namespace NuGet.Insights
{
    public static class XmlUtility
    {
        public static XDocument LoadXml(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };

            using (var streamReader = new StreamReader(stream))
            using (var xmlReader = XmlReader.Create(streamReader, settings))
            {
                var document = XDocument.Load(xmlReader, LoadOptions.None);

                // Make the document immutable.
                document.Changing += (s, ev) =>
                {
                    throw new NotSupportedException();
                };

                return document;
            }
        }
    }
}
