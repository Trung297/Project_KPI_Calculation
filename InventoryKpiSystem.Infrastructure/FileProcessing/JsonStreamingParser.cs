using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using InventoryKpiSystem.Core.Interfaces;

namespace InventoryKpiSystem.Infrastructure.FileProcessing;

public class JsonStreamingParser<T> : IAsyncFileParser<T>
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public JsonStreamingParser()
    {
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    public async IAsyncEnumerable<T> ParseAsync(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 8192,
            useAsync: true);

        var asyncEnumerable = JsonSerializer.DeserializeAsyncEnumerable<T>(
            fileStream,
            _jsonSerializerOptions,
            cancellationToken);

        await foreach (var item in asyncEnumerable.WithCancellation(cancellationToken))
        {
            if (item != null)
            {
                yield return item;
            }
        }
    }
}