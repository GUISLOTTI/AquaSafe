using AquaSafe.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AquaSafe.Api.Services;

public sealed class ImaService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<ImaService> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private const string CacheKeyBeaches = "beaches_region";

    private static readonly HashSet<string> TargetCities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Itajaí", "Navegantes", "Balneário Camboriú", "Penha"
    };

    // URL base do portal IMA
    private const string ImaBaseUrl = "https://balneabilidade.ima.sc.gov.br";

    // Coordenadas serão substituídas pelos dados reais no Sprint 2.
    private static readonly List<Beach> SeedBeaches =
    [
        // Penha
        new("penha-praia-alegre",   "Praia Alegre",                "Penha",              -26.7742, -48.6158, WaterQuality.Unknown, "-", "-"),
        new("penha-armacao",        "Praia da Armação",            "Penha",              -26.7705, -48.6361, WaterQuality.Unknown, "-", "-"),
        new("penha-saudade",        "Praia da Saudade",            "Penha",              -26.7620, -48.6290, WaterQuality.Unknown, "-", "-"),
        new("penha-sao-miguel",     "Praia de São Miguel",         "Penha",              -26.7850, -48.6380, WaterQuality.Unknown, "-", "-"),
        new("penha-praia-grande",   "Praia Grande",                "Penha",              -26.7930, -48.6450, WaterQuality.Unknown, "-", "-"),

        // Balneário Camboriú
        new("bc-laranjeiras",       "Praia de Laranjeiras",        "Balneário Camboriú", -27.0120, -48.6510, WaterQuality.Unknown, "-", "-"),
        new("bc-taquaras",          "Praia de Taquaras",           "Balneário Camboriú", -27.0230, -48.6570, WaterQuality.Unknown, "-", "-"),
        new("bc-praia-central",     "Praia de Balneário Camboriú", "Balneário Camboriú", -26.9945, -48.6348, WaterQuality.Unknown, "-", "-"),
        new("bc-estaleiro",         "Praia do Estaleiro",          "Balneário Camboriú", -26.9760, -48.6450, WaterQuality.Unknown, "-", "-"),
        new("bc-estaleirinho",      "Praia do Estaleirinho",       "Balneário Camboriú", -26.9830, -48.6480, WaterQuality.Unknown, "-", "-"),

        // Itajaí
        new("itajai-praia-brava",   "Praia Brava",                 "Itajaí",             -26.9510, -48.6430, WaterQuality.Unknown, "-", "-"),
        new("itajai-cabecudas",     "Praia de Cabeçudas",          "Itajaí",             -26.9198, -48.6368, WaterQuality.Unknown, "-", "-"),
        new("itajai-atalaia",       "Praia do Atalaia",            "Itajaí",             -26.9385, -48.6548, WaterQuality.Unknown, "-", "-"),

        // Navegantes
        new("navegantes-central",   "Praia de Navegantes",         "Navegantes",         -26.8985, -48.6530, WaterQuality.Unknown, "-", "-"),
    ];

    // ── API pública ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Beach>> GetBeachesAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKeyBeaches, out IReadOnlyList<Beach>? cached) && cached is not null)
            return cached;

        var beaches = await FetchFromImaAsync(ct);

        cache.Set(CacheKeyBeaches, beaches, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
            SlidingExpiration = null
        });

        return beaches;
    }

    public async Task<BeachHistory?> GetHistoryAsync(string beachId, CancellationToken ct = default)
    {
        var all = await GetBeachesAsync(ct);
        var beach = all.FirstOrDefault(b => b.Id == beachId);
        if (beach is null) return null;

        var samples = Enumerable.Range(0, 5).Select(i => new SampleEntry(
            Date: DateOnly.FromDateTime(DateTime.Today.AddDays(-i * 7)).ToString("dd/MM/yyyy"),
            Quality: beach.Quality,
            RawStatus: beach.RawStatus
        )).ToList();

        return new BeachHistory(beach.Id, beach.Name, samples);
    }

    private async Task<IReadOnlyList<Beach>> FetchFromImaAsync(CancellationToken ct)
    {
        try
        {
            var response = await httpClient.GetAsync($"{ImaBaseUrl}/", ct);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(ct);
            var parsed = ParseImaHtml(html);

            if (parsed.Count > 0)
            {
                logger.LogInformation("IMA scraping: {Count} praias obtidas.", parsed.Count);
                return parsed;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar dados do IMA. Retornando dados seed.");
        }

        return SeedBeaches.AsReadOnly();
    }

    private static List<Beach> ParseImaHtml(string html)
    {
        // TODO Sprint 2.
        return [];
    }
}