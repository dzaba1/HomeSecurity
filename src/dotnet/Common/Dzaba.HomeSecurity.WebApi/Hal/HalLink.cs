using System.Text.Json.Serialization;

namespace Dzaba.HomeSecurity.WebApi.Hal;

/// <summary>One entry of a HAL <c>_links</c> object (docs/architecture/13-hateoas-public-api.md).</summary>
public sealed record HalLink(
    [property: JsonPropertyName("href")] string Href,
    [property: JsonPropertyName("method")] string? Method = null);
