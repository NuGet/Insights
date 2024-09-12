// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.KustoIngestion
{
    /// <summary>
    /// A delegate to validate the data reader and return an error message if there is any.
    /// </summary>
    /// <param name="reader">The data reader.</param>
    /// <returns>The validation error message, or null if there is no error.</returns>
    public delegate string ValidateKustoDataReader(IDataReader reader);

    public record KustoValidation(string Label, string Query, ValidateKustoDataReader Validate);

    public interface IKustoValidationProvider
    {
        Task<IReadOnlyList<KustoValidation>> GetValidationsAsync();
    }
}
