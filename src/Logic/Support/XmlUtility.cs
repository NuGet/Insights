using System;
using System.IO;
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
