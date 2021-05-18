// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml.Linq;

namespace NuGet.Insights
{
    public class XmlDependency
    {
        public XmlDependency(string id, string version, XElement element)
        {
            Id = id;
            Version = version;
            Element = element;
        }

        public string Id { get; }
        public string Version { get; }
        public XElement Element { get; }
    }
}
