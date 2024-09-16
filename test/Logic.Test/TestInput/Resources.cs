// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public static class Resources
    {
        public static MemoryStream LoadMemoryStream(string resourceName)
        {
            using (var fileStream = GetFileStream(resourceName))
            {
                var memoryStream = new MemoryStream();
                fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                return memoryStream;
            }
        }

        public static StringReader LoadStringReader(string resourceName)
        {
            using (var reader = new StreamReader(GetFileStream(resourceName)))
            {
                return new StringReader(reader.ReadToEnd());
            }
        }

        private static FileStream GetFileStream(string resourceName)
        {
            return File.OpenRead(Path.Combine("TestInput", resourceName));
        }
    }
}
