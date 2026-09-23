using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dzaba.HomeSecurity.WebApi.Hal;

/// <summary>
/// Hand-rolled HAL <c>_links</c>/<c>_embedded</c> envelope - no well-adopted,
/// actively-maintained, net10-ready HAL package exists for ASP.NET Core (see
/// the plan's HATEOAS section for the packages evaluated and rejected), and
/// HAL's shape is simple/stable enough that hand-rolling it is the right
/// call rather than a shortcut. Uses only System.Text.Json - no new package.
/// </summary>
public static class HalEnvelope
{
    public static JsonObject Wrap<T>(T resource, IReadOnlyDictionary<string, HalLink> links, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(options);

        var node = JsonSerializer.SerializeToNode(resource, options)!.AsObject();
        node["_links"] = JsonSerializer.SerializeToNode(links, options);
        return node;
    }

    public static JsonObject WrapCollection<T>(string embeddedKey, IEnumerable<T> items,
        IReadOnlyDictionary<string, HalLink> links, JsonSerializerOptions options,
        Func<T, IReadOnlyDictionary<string, HalLink>>? itemLinks = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(embeddedKey);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(options);

        var embeddedItems = items
            .Select(item => itemLinks is null ? JsonSerializer.SerializeToNode(item, options)! : Wrap(item, itemLinks(item), options))
            .Cast<JsonNode?>()
            .ToArray();

        return new JsonObject
        {
            ["_links"] = JsonSerializer.SerializeToNode(links, options),
            ["_embedded"] = new JsonObject { [embeddedKey] = new JsonArray(embeddedItems) },
        };
    }
}
