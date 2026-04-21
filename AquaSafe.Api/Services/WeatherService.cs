using System.Text.Json;
using System.Text.Json.Serialization;
using AquaSafe.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AquaSafe.Api.Services;

file sealed record OpenMeteoResponse(
    [property: JsonPropertyName("daily")] OpenMeteoDailyData? Daily
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
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private const double Lat = -26.92;
    private const double Lng = -48.64;
    private const double RainThresholdMm = 5.0;

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
                      $"&daily=precipitation_sum,rain_sum" +
                      $"&past_days=1" +
                      $"&forecast_days=1" +
                      $"&timezone=America%2FSao_Paulo";

            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<OpenMeteoResponse>(json, JsonOpts);

            if (data?.Daily?.PrecipitationSum is null || data.Daily.PrecipitationSum.Count == 0)
                return NoAlert();

            var yesterday = data.Daily.PrecipitationSum.ElementAtOrDefault(0) ?? 0;
            var today = data.Daily.PrecipitationSum.ElementAtOrDefault(1) ?? 0;
            var totalMm = yesterday + today;

            logger.LogInformation("Precipitação: ontem={Yesterday}mm, hoje={Today}mm, total={Total}mm",
                yesterday, today, totalMm);

            if (totalMm >= RainThresholdMm)
            {
                var msg = totalMm >= 20
                    ? $"Chuva intensa registrada nas últimas 24h na região ({totalMm:F1} mm). " +
                      "Evite o banho de mar por pelo menos 48h após precipitações fortes."
                    : $"Chuva registrada nas últimas 24h ({totalMm:F1} mm). " +
                      "Qualidade da água pode estar afetada — verifique o status de cada praia antes de entrar no mar.";

                return new RainAlert(HasRecentRain: true, Message: msg);
            }

            return NoAlert();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao consultar Open-Meteo.");
            return NoAlert();
        }
    }

    private static RainAlert NoAlert() =>
        new(HasRecentRain: false, Message: "Sem registro de chuva significativa nas últimas 24h na região.");
}
