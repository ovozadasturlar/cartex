using System.Text.Json.Serialization;
using Cartex.Shared.Models.Catalog;

namespace Cartex.Application.Common.Settings;

public sealed class ProductReferenceSettings
{
    public const string DefaultEndpointBaseUrl = "https://bvkcbcctttqbzvxidlus.supabase.co/functions/v1/catalog-lookup";
    public const string DefaultImageBaseUrl = "https://catalog.cartex.uz/";

    [JsonConverter(typeof(JsonStringEnumConverter<CatalogSourceMode>))]
    public CatalogSourceMode Mode { get; set; } = CatalogSourceMode.Online;

    public string EndpointBaseUrl { get; set; } = DefaultEndpointBaseUrl;

    public string ImageBaseUrl { get; set; } = DefaultImageBaseUrl;
}
