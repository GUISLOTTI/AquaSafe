using AquaSafe.Api.Services;

namespace AquaSafe.Api.Endpoints;

public static class MarineEndpoints
{
    public static void MapMarineEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/marine")
                       .WithTags("Marine")
                       .WithOpenApi();

        group.MapGet("/cities", async (MarineService svc, CancellationToken ct) =>
        {
            var data = await svc.GetCityMarineAsync(ct);
            return Results.Ok(data);
        })
        .WithSummary("Condições marinhas por cidade")
        .WithDescription("Ondas, temperatura da água, maré e UV via Open-Meteo Marine. Cache de 1h.");
    }
}

public static class CameraEndpoints
{
    public static void MapCameraEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cameras")
                       .WithTags("Cameras")
                       .WithOpenApi();

        group.MapGet("/{beachId}", async (string beachId, ImaService ima, CameraService cam, CancellationToken ct) =>
        {
            var beaches = await ima.GetBeachesAsync(ct);
            var beach = beaches.FirstOrDefault(b => b.Id == beachId);
            if (beach is null)
                return Results.NotFound(new { error = $"Praia '{beachId}' não encontrada." });

            var info = await cam.GetCameraAsync(beachId, beach.Latitude, beach.Longitude, beach.Name, ct);
            return Results.Ok(info);
        })
        .WithSummary("Câmera ao vivo da praia")
        .WithDescription("Busca câmera próxima via Windy Webcams API; se indisponível, retorna link de busca no YouTube.");
    }
}
