using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RcBunny.Data;

/// <summary>JSON columns (Laravel's "array" casts): free-form JSON as JsonNode, string lists as List&lt;string&gt;.</summary>
public static class JsonColumn
{
    public static readonly ValueConverter<JsonNode?, string?> Converter = new(
        v => v == null ? null : v.ToJsonString((JsonSerializerOptions?)null),
        v => string.IsNullOrEmpty(v) ? null : JsonNode.Parse(v, null, default));

    public static readonly ValueComparer<JsonNode?> Comparer = new(
        (a, b) => JsonNode.DeepEquals(a, b),
        v => v == null ? 0 : v.ToJsonString((JsonSerializerOptions?)null).GetHashCode(),
        v => v == null ? null : v.DeepClone());

    public static readonly ValueConverter<List<string>, string> ListConverter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => string.IsNullOrEmpty(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

    public static readonly ValueComparer<List<string>> ListComparer = new(
        (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
        v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
        v => v.ToList());
}
