// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetPe;

#nullable enable

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv;

public class TemporaryFileProvider : ITemporaryFileProvider
{
    private readonly ILogger<TemporaryFileProvider> _logger;
    private static readonly char[] SlashSeparators = ['/', '\\'];

    public TemporaryFileProvider(ILogger<TemporaryFileProvider> logger)
    {
        _logger = logger;
    }

    public TemporaryFile GetTemporaryFile(Stream stream, IPackage? package, string? fileName, IPart? part)
    {
        var pieces = new List<string>();
        var specific = true;

        if (package is not null)
        {
            pieces.Add(package.Id);
            pieces.Add(package.Version.ToNormalizedString());
        }
        else
        {
            specific = false;
        }

        if (part?.Path is not null)
        {
            pieces.AddRange(part.Path.Split(SlashSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        else if (part?.Name is not null)
        {
            pieces.Add(part.Name);
        }
        else
        {
            pieces.Add(".tmp");
        }

        const int MaxPath = 255;
        var builder = new StringBuilder();
        var dir = Path.Combine(Path.GetTempPath(), "npe");
        builder.Append(Path.Combine(dir, Guid.NewGuid().ToByteArray().ToTrimmedBase32()));
        foreach (var piece in pieces)
        {
            var separator = piece.StartsWith('.') ? string.Empty : "_";

            if (builder.Length + separator.Length + piece.Length > MaxPath)
            {
                specific = false;
                break;
            }

            builder.Append(separator);
            builder.Append(piece);
        }

        var path = builder.ToString();
        Directory.CreateDirectory(dir);

        long length;
        try
        {
            length = stream.Length;
        }
        catch (NotSupportedException)
        {
            length = -1;
        }

        var metadata = KustoDynamicSerializer.Serialize(new
        {
            Id = package?.Id,
            Version = package?.Version.ToFullString(),
            FileName = fileName,
            PartPath = part?.Path,
            PartName = part?.Name,
            Length = length,
        });

        try
        {
            using var hasher = IncrementalHash.CreateSHA256();
            using var destination = new FileStream(
                path,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 4096);

            if (length > 0)
            {
                destination.SetLengthAndWrite(length);
            }

            stream.CopyToSlow(
                destination,
                length,
                NuGetPackageExplorerToCsvDriver.FileBufferSize,
                hasher,
                _logger);

            if (!specific)
            {
                _logger.LogWarning(
                    "Ambiguous temporary file copied to disk. Path: {Path}. Hash: {SHA256}. Length: {Length}. Metadata: {Metadata}",
                    path,
                    hasher.Output.SHA256.ToBase64(),
                    destination.Length,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save a temporary file. Path: {Path}. Metadata: {Metadata}", path, metadata);
            throw;
        }

        return new TemporaryFile(path);
    }
}
