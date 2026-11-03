using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace RcBunny.Backend.Infrastructure;

/// <summary>
/// HybridCache stores values as bytes (so they can go to Redis). Use the same camelCase JSON as the API,
/// so a cached anonymous object comes back out of the cache looking exactly like a fresh one.
/// </summary>
public sealed class WebJsonCacheSerializerFactory : IHybridCacheSerializerFactory
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
    {
        serializer = new Serializer<T>();
        return true;
    }

    private sealed class Serializer<T> : IHybridCacheSerializer<T>
    {
        public T Deserialize(ReadOnlySequence<byte> source)
        {
            var reader = new Utf8JsonReader(source);
            return JsonSerializer.Deserialize<T>(ref reader, Options)!;
        }

        public void Serialize(T value, IBufferWriter<byte> target)
        {
            using var writer = new Utf8JsonWriter(target);
            JsonSerializer.Serialize(writer, value, Options);
        }
    }
}
