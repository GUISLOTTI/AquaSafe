using System.Text.Json;
using System.Text.Json.Serialization;
using AquaSafe.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AquaSafe.Api.Services;

file sealed record OpenMeteoResponse(
    [property: JsonPropertyName("current")] OpenMeteoCurrent? Current,
    [property: JsonPropertyName("daily")]   OpenMeteoDailyData? Daily
);

file sealed record OpenMeteoCurrent(
    [property: JsonPropertyName("weather_code")] int? WeatherCode,
    [property: JsonPropertyName("precipitation")] double? Precipitation
);

file sealed record OpenMeteoDailyData(
    [property: JsonPropertyName("time")]               List<string>? Time,
    [property: JsonPropertyName("precipitation_sum")]  List<double?>? PrecipitationSum,
    [property: JsonPropertyName("rain_sum")]           List<double?>? RainSum
);

public sealed class WeatherService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<WeatherService> logger)
{
    private const string CacheKey = "rain_alert";
    private const string CitiesCacheKey = "city_conditions";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private const double Lat = -26.92;
    private const double Lng = -48.64;
    private const double RainThresholdMm = 5.0;

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

    public async Task<RainAlert> GetRainAlertAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out RainAlert? cached) && cached is not null)
            return cached;

        var alert = await FetchRainAlertAsync(ct);
        cache.Set(CacheKey, alert, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });
        return alert;
    }

    private async Task<RainAlert> FetchRainAlertAsync(CancellationToken ct)
    {
        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast" +
                      $"?latitude={Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      $"&longitude={Lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      $"&current=weather_code" +
                      $"&daily=precipitation_sum,rain_sum" +
                      $"&past_days=1" +
                      $"&forecast_days=1" +
                      $"&timezone=America%2FSao_Paulo";

            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<OpenMeteoResponse>(json, JsonOpts);

            var (condition, conditionLabel) = MapCondition(data?.Current?.WeatherCode);

            if (data?.Daily?.PrecipitationSum is null || data.Daily.PrecipitationSum.Count == 0)
                return new RainAlert(false, "Dados de chuva indisponíveis no momento.", 0, "unknown", condition, conditionLabel);

            var yesterday = data.Daily.PrecipitationSum.ElementAtOrDefault(0) ?? 0;
            var today = data.Daily.PrecipitationSum.ElementAtOrDefault(1) ?? 0;
            var totalMm = yesterday + today;

            logger.LogInformation("Precipitação: ontem={Yesterday}mm, hoje={Today}mm, total={Total}mm, condição={Condition}",
                yesterday, today, totalMm, condition);

            var level = totalMm switch
            {
                >= 20 => "heavy",
                >= RainThresholdMm => "moderate",
                >= 1  => "light",
                _     => "none"
            };

            if (totalMm >= RainThresholdMm)
            {
                var msg = totalMm >= 20
                    ? $"Chuva intensa registrada nas últimas 24h na região ({totalMm:F1} mm). " +
                      "Evite o banho de mar por pelo menos 48h após precipitações fortes."
                    : $"Chuva registrada nas últimas 24h ({totalMm:F1} mm). " +
                      "Qualidade da água pode estar afetada — verifique o status de cada praia antes de entrar no mar.";

                return new RainAlert(true, msg, Math.Round(totalMm, 1), level, condition, conditionLabel);
            }

            var msgLight = totalMm >= 1
                ? $"Chuva fraca nas últimas 24h ({totalMm:F1} mm). Sem impacto significativo na balneabilidade."
                : "Sem registro de chuva significativa nas últimas 24h na região.";

            return new RainAlert(false, msgLight, Math.Round(totalMm, 1), level, condition, conditionLabel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao consultar Open-Meteo.");
            return new RainAlert(false, "Dados de chuva indisponíveis no momento.", 0, "unknown", "unknown", "Sem dados");
        }
    }

    public async Task<IReadOnlyDictionary<string, CityCondition>> GetCityConditionsAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CitiesCacheKey, out IReadOnlyDictionary<string, CityCondition>? cached) && cached is not null)
            return cached;

        var tasks = Cities.Select(async kv =>
        {
            var condition = await FetchCityConditionAsync(kv.Value.Lat, kv.Value.Lng, ct);
            return (City: kv.Key, Condition: condition);
        });

        var results = await Task.WhenAll(tasks);
        var dict = results.ToDictionary(r => r.City, r => r.Condition);

        cache.Set(CitiesCacheKey, (IReadOnlyDictionary<string, CityCondition>)dict, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });
        return dict;
    }

    private async Task<CityCondition> FetchCityConditionAsync(double lat, double lng, CancellationToken ct)
    {
        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast" +
                      $"?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      $"&longitude={lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      $"&current=weather_code,precipitation" +
                      $"&timezone=America%2FSao_Paulo";

            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<OpenMeteoResponse>(json, JsonOpts);

            var (condition, label) = MapCondition(data?.Current?.WeatherCode);
            var mm = data?.Current?.Precipitation ?? 0;
            return new CityCondition(condition, label, Math.Round(mm, 1));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar tempo em ({Lat},{Lng}).", lat, lng);
            return new CityCondition("unknown", "Sem dados", 0);
        }
    }

    private static (string Condition, string Label) MapCondition(int? code) => code switch
    {
        0           => ("clear",         "Céu limpo"),
        1 or 2      => ("partly_cloudy", "Parcialmente nublado"),
        3           => ("cloudy",        "Nublado"),
        45 or 48    => ("fog",           "Neblina"),
        51 or 53 or 55 or 61 or 80 => ("light_rain", "Chuva fraca"),
        63 or 65 or 81             => ("rain",       "Chuva"),
        82 or 95 or 96 or 99       => ("heavy_rain", "Chuva forte"),
        _ => ("unknown", "Sem dados")
    };
}
