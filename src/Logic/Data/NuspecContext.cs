// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml.Linq;

namespace NuGet.Insights
{
    public class NuspecContext
    {
        public NuspecContext(bool exists, XDocument document)
        {
            Exists = exists;
            Document = document;
        }

        public static NuspecContext FromStream(string id, string version, Stream stream, ILogger logger)
        {
            if (stream == null)
            {
                return new NuspecContext(exists: false, document: null);
            }

            try
            {
                var document = XmlUtility.LoadXml(stream);
                return new NuspecContext(exists: true, document: document);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not parse .nuspec for {Id} {Version}.", id, version);
                throw;
            }
        }

        public bool Exists { get; }
        public XDocument Document { get; }
    }
}
