using AquaSafe.Api.Services;

namespace AquaSafe.Api.Endpoints;

public static class WeatherEndpoints
{
    public static void MapWeatherEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/weather")
                       .WithTags("Weather")
                       .WithOpenApi();

        group.MapGet("/rain-alert", async (WeatherService svc, CancellationToken ct) =>
        {
            var alert = await svc.GetRainAlertAsync(ct);
            return Results.Ok(alert);
        })
        .WithSummary("Alerta de chuva recente")
        .WithDescription("Verifica precipitação nas últimas 24h via Open-Meteo. Limiar >= 5mm. Cache de 1h.");

        group.MapGet("/cities", async (WeatherService svc, CancellationToken ct) =>
        {
            var conditions = await svc.GetCityConditionsAsync(ct);
            return Results.Ok(conditions);
        })
        .WithSummary("Condição climática por cidade")
        .WithDescription("Retorna o tempo atual (Open-Meteo) para cada um dos quatro municípios monitorados. Cache de 1h.");
    }
}
