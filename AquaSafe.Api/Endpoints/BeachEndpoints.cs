using AquaSafe.Api.Models;
using AquaSafe.Api.Services;

namespace AquaSafe.Api.Endpoints;

public static class BeachEndpoints
{
    public static void MapBeachEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/beaches")
                       .WithTags("Beaches")
                       .WithOpenApi();

        // GET /api/beaches
        // Retorna todas as praias da região com status atual de balneabilidade
        group.MapGet("/", async (ImaService svc, CancellationToken ct) =>
        {
            var beaches = await svc.GetBeachesAsync(ct);
            return Results.Ok(beaches);
        })
        .WithSummary("Lista todas as praias monitoradas")
        .WithDescription("Retorna status de balneabilidade atual de cada praia da região Itajaí–Penha. Dados cacheados por 6h.");

        // GET /api/beaches/{id}/history
        // Retorna histórico das últimas coletas de um ponto específico
        group.MapGet("/{id}/history", async (string id, ImaService svc, CancellationToken ct) =>
        {
            var history = await svc.GetHistoryAsync(id, ct);
            return history is null
                ? Results.NotFound(new { error = $"Praia '{id}' não encontrada." })
                : Results.Ok(history);
        })
        .WithSummary("Histórico de balneabilidade de uma praia")
        .WithDescription("Retorna as últimas 5 coletas do ponto selecionado.");
    }
}

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new
        {
            status    = "healthy",
            timestamp = DateTime.UtcNow,
            version   = "1.0.0"
        }))
        .WithTags("Health")
        .WithOpenApi()
        .WithSummary("Health check da API");
    }
}
