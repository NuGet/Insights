// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class TempStreamWriterTest
    {
        public class TheGetTempFileNameFactoryMethod
        {
            [Theory]
            [InlineData("processor", ".dll", "_processor.dll")]
            [InlineData("processor", ".foo.dll", "_processor.dll")]
            [InlineData("processor", ".foo-dll", "_processor.tmp")]
            [InlineData("processor", "dll", "_processor.dll")]
            [InlineData("processor", "", "_processor.tmp")]
            [InlineData("", ".dll", ".dll")]
            [InlineData(null, null, ".tmp")]
            public void ReturnsExpectedFileNameSuffix(string? contextHint, string? extension, string expected)
            {
                // Arrange
                var factory = TempStreamWriter.GetTempFileNameFactory("NuGet.Versioning", "3.5.0", contextHint, extension);

                // Act
                var tempFileName = factory();

                // Assert
                Assert.EndsWith("_NuGet.Versioning_3.5.0" + expected, tempFileName, StringComparison.Ordinal);
            }
        }
    }
}
