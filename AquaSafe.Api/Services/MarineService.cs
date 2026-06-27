using System.Text.Json;
using System.Text.Json.Serialization;
using AquaSafe.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AquaSafe.Api.Services;

file sealed record MarineResponse(
    [property: JsonPropertyName("current")] MarineCurrent? Current
);

file sealed record MarineCurrent(
    [property: JsonPropertyName("wave_height")]               double? WaveHeight,
    [property: JsonPropertyName("wave_period")]               double? WavePeriod,
    [property: JsonPropertyName("wave_direction")]            double? WaveDirection,
    [property: JsonPropertyName("sea_surface_temperature")]   double? SeaSurfaceTemperature,
    [property: JsonPropertyName("sea_level_height_msl")]      double? SeaLevelHeight
);

file sealed record UvResponse(
    [property: JsonPropertyName("current")] UvCurrent? Current
);

file sealed record UvCurrent(
    [property: JsonPropertyName("uv_index")] double? UvIndex
);

public sealed class MarineService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<MarineService> logger)
{
    private const string CacheKey = "marine_cities";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private static readonly Dictionary<string, (double Lat, double Lng)> Cities = new()
    {
        ["Penha"]              = (-26.78, -48.63),
        ["Navegantes"]         = (-26.89, -48.65),
        ["Itajaí"]             = (-26.92, -48.66),
        ["Balneário Camboriú"] = (-26.99, -48.63),
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyDictionary<string, MarineCondition>> GetCityMarineAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, MarineCondition>? cached) && cached is not null)
            return cached;

        var tasks = Cities.Select(async kv =>
        {
            var condition = await FetchMarineAsync(kv.Value.Lat, kv.Value.Lng, ct);
            return (City: kv.Key, Condition: condition);
        });

        var results = await Task.WhenAll(tasks);
        var dict = results.ToDictionary(r => r.City, r => r.Condition);

        cache.Set(CacheKey, (IReadOnlyDictionary<string, MarineCondition>)dict, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });
        return dict;
    }

    private async Task<MarineCondition> FetchMarineAsync(double lat, double lng, CancellationToken ct)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var marineUrl = $"https://marine-api.open-meteo.com/v1/marine" +
                        $"?latitude={lat.ToString(inv)}&longitude={lng.ToString(inv)}" +
                        $"&current=wave_height,wave_period,wave_direction,sea_surface_temperature,sea_level_height_msl" +
                        $"&timezone=America%2FSao_Paulo";

        var uvUrl = $"https://api.open-meteo.com/v1/forecast" +
                    $"?latitude={lat.ToString(inv)}&longitude={lng.ToString(inv)}" +
                    $"&current=uv_index" +
                    $"&timezone=America%2FSao_Paulo";

        var marineTask = FetchJsonAsync<MarineResponse>(marineUrl, ct);
        var uvTask = FetchJsonAsync<UvResponse>(uvUrl, ct);

        await Task.WhenAll(marineTask, uvTask);

        var marine = marineTask.Result?.Current;
        var uv = uvTask.Result?.Current;

        return new MarineCondition(
            WaveHeightM:      Round1(marine?.WaveHeight),
            WavePeriodS:      Round1(marine?.WavePeriod),
            WaveDirectionDeg: Round1(marine?.WaveDirection),
            SeaTemperatureC:  Round1(marine?.SeaSurfaceTemperature),
            TidalHeightM:     Round2(marine?.SeaLevelHeight),
            UvIndex:          Round1(uv?.UvIndex)
        );
    }

    private async Task<T?> FetchJsonAsync<T>(string url, CancellationToken ct) where T : class
    {
        try
        {
            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar {Url}", url);
            return null;
        }
    }

    private static double? Round1(double? v) => v.HasValue ? Math.Round(v.Value, 1) : null;
    private static double? Round2(double? v) => v.HasValue ? Math.Round(v.Value, 2) : null;
}
