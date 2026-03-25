namespace RufistoWard.Auth.Admin.Common;

using System.Text.Json.Serialization;

public sealed record PagedResult<T>
{
    [JsonPropertyName("items")]
    public required IReadOnlyList<T> Items { get; init; }

    [JsonPropertyName("totalCount")]
    public required int TotalCount { get; init; }

    [JsonPropertyName("page")]
    public required int Page { get; init; }

    [JsonPropertyName("pageSize")]
    public required int PageSize { get; init; }
}
