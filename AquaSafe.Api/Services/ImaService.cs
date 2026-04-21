using System.Text.Json;
using System.Text.Json.Serialization;
using AquaSafe.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AquaSafe.Api.Services;

internal sealed record ImaMapaPonto(
    [property: JsonPropertyName("CODIGO")] string Codigo,
    [property: JsonPropertyName("MUNICIPIO")] string Municipio,
    [property: JsonPropertyName("BALNEARIO")] string Balneario,
    [property: JsonPropertyName("PONTO_NOME")] string? PontoNome,
    [property: JsonPropertyName("LOCALIZACAO")] string? Localizacao,
    [property: JsonPropertyName("LATITUDE")] string? Latitude,
    [property: JsonPropertyName("LONGITUDE")] string? Longitude,
    [property: JsonPropertyName("ANALISES")] List<ImaAnalise>? Analises
);

internal sealed record ImaAnalise(
    [property: JsonPropertyName("DATA")] string? Data,
    [property: JsonPropertyName("CONDICAO")] string? Condicao,
    [property: JsonPropertyName("CHUVA")] string? Chuva,
    [property: JsonPropertyName("RESULTADO")] string? Resultado,
    [property: JsonPropertyName("TEMP_AGUA")] string? TempAgua
);

public sealed class ImaService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<ImaService> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private const string CacheKeyBeaches = "beaches_region";
    private const string CacheKeyMapaRaw = "ima_mapa_raw";
    private const string ImaBaseUrl = "https://balneabilidade.ima.sc.gov.br";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Dictionary<string, string> CityKeyByMunicipio = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PENHA"] = "penha",
        ["NAVEGANTES"] = "navegantes",
        ["ITAJAÍ"] = "itajai",
        ["BALNEÁRIO CAMBORIÚ"] = "bc",
    };

    // Fallback quando a API do IMA não responde
    private static readonly List<Beach> SeedBeaches =
    [
        new("penha-praia-alegre",  "Praia Alegre",                "Penha",              -26.7686, -48.6190, WaterQuality.Unknown, "-", "-"),
        new("penha-armacao",       "Praia da Armação",            "Penha",              -26.7876, -48.6081, WaterQuality.Unknown, "-", "-"),
        new("penha-saudade",       "Praia da Saudade",            "Penha",              -26.7761, -48.6068, WaterQuality.Unknown, "-", "-"),
        new("penha-sao-miguel",    "Praia de São Miguel",         "Penha",              -26.8245, -48.6114, WaterQuality.Unknown, "-", "-"),
        new("penha-praia-grande",  "Praia Grande",                "Penha",              -26.7905, -48.5954, WaterQuality.Unknown, "-", "-"),
        new("bc-laranjeiras",      "Praia de Laranjeiras",        "Balneário Camboriú", -26.9949, -48.5823, WaterQuality.Unknown, "-", "-"),
        new("bc-taquaras",         "Praia de Taquaras",           "Balneário Camboriú", -27.0077, -48.5805, WaterQuality.Unknown, "-", "-"),
        new("bc-praia-central",    "Praia de Balneário Camboriú", "Balneário Camboriú", -26.9852, -48.6335, WaterQuality.Unknown, "-", "-"),
        new("bc-estaleiro",        "Praia do Estaleiro",          "Balneário Camboriú", -27.0505, -48.5945, WaterQuality.Unknown, "-", "-"),
        new("bc-estaleirinho",     "Praia do Estaleirinho",       "Balneário Camboriú", -27.0642, -48.5888, WaterQuality.Unknown, "-", "-"),
        new("itajai-praia-brava",  "Praia Brava",                 "Itajaí",             -26.9472, -48.6235, WaterQuality.Unknown, "-", "-"),
        new("itajai-cabecudas",    "Praia de Cabeçudas",          "Itajaí",             -26.9245, -48.6280, WaterQuality.Unknown, "-", "-"),
        new("itajai-atalaia",      "Praia do Atalaia",            "Itajaí",             -26.9185, -48.6288, WaterQuality.Unknown, "-", "-"),
        new("navegantes-central",  "Praia de Navegantes",         "Navegantes",         -26.8845, -48.6415, WaterQuality.Unknown, "-", "-"),
    ];

    public async Task<IReadOnlyList<Beach>> GetBeachesAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKeyBeaches, out IReadOnlyList<Beach>? cached) && cached is not null)
            return cached;

        var beaches = await FetchFromImaAsync(ct);
        cache.Set(CacheKeyBeaches, beaches, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });
        return beaches;
    }

    public async Task<BeachHistory?> GetHistoryAsync(string beachId, CancellationToken ct = default)
    {
        var all = await GetBeachesAsync(ct);
        var beach = all.FirstOrDefault(b => b.Id == beachId);
        if (beach is null) return null;

        if (cache.TryGetValue(CacheKeyMapaRaw, out List<ImaMapaPonto>? pontos) && pontos is not null)
        {
            var history = BuildHistoryFromMapa(beach, pontos);
            if (history is not null) return history;
        }

        // Fallback: replica última amostra
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
            var pontos = await FetchMapaAsync(ct);
            if (pontos.Count == 0)
            {
                logger.LogWarning("IMA retornou vazio. Usando dados seed.");
                return SeedBeaches.AsReadOnly();
            }

            cache.Set(CacheKeyMapaRaw, pontos, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });

            var pontosRegiao = pontos
                .Where(p => CityKeyByMunicipio.ContainsKey(p.Municipio))
                .ToList();

            if (pontosRegiao.Count == 0)
            {
                logger.LogWarning("Nenhum ponto na região alvo. Usando dados seed.");
                return SeedBeaches.AsReadOnly();
            }

            var beaches = pontosRegiao
                .GroupBy(p => (p.Municipio, p.Balneario))
                .Select(g => BuildBeachFromGroup(g.Key.Municipio, g.Key.Balneario, g.ToList()))
                .Where(b => b is not null)
                .Cast<Beach>()
                .OrderBy(b => b.City).ThenBy(b => b.Name)
                .ToList();

            logger.LogInformation("IMA: {Count} praias, {Pontos} pontos de coleta.", beaches.Count, pontosRegiao.Count);
            return beaches.AsReadOnly();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar dados do IMA. Usando dados seed.");
            return SeedBeaches.AsReadOnly();
        }
    }

    private async Task<List<ImaMapaPonto>> FetchMapaAsync(CancellationToken ct)
    {
        var url = $"{ImaBaseUrl}/relatorio/mapa";
        var response = await httpClient.PostAsync(url, null, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<ImaMapaPonto>>(json, JsonOpts) ?? [];
    }

    private Beach? BuildBeachFromGroup(string municipio, string balneario, List<ImaMapaPonto> pontos)
    {
        if (!CityKeyByMunicipio.TryGetValue(municipio, out var cityKey))
            return null;

        var cityName = municipio switch
        {
            "PENHA" => "Penha",
            "NAVEGANTES" => "Navegantes",
            "ITAJAÍ" => "Itajaí",
            "BALNEÁRIO CAMBORIÚ" => "Balneário Camboriú",
            _ => municipio
        };

        // Pior condição entre todos os pontos de coleta
        var allConditions = pontos
            .SelectMany(p => p.Analises?.Take(1) ?? [])
            .Select(a => a.Condicao)
            .ToList();

        var worstQuality = allConditions.Any(c => ParseQuality(c) == WaterQuality.Improper)
            ? WaterQuality.Improper
            : allConditions.Any(c => ParseQuality(c) == WaterQuality.Proper)
                ? WaterQuality.Proper
                : WaterQuality.Unknown;

        var worstRaw = worstQuality switch
        {
            WaterQuality.Improper => "IMPRÓPRIO",
            WaterQuality.Proper => "PRÓPRIO",
            _ => "-"
        };

        var lastDate = pontos
            .SelectMany(p => p.Analises?.Take(1) ?? [])
            .Select(a => a.Data)
            .Where(d => d is not null)
            .FirstOrDefault() ?? "-";

        var coords = pontos
            .Select(p => ParseCoords(p.Latitude, p.Longitude))
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .ToList();

        var (lat, lng) = coords.Count > 0
            ? (coords.Average(c => c.Lat), coords.Average(c => c.Lng))
            : (-26.92, -48.64);

        var id = BuildId(cityKey, balneario);
        return new Beach(id, balneario, cityName, lat, lng, worstQuality, lastDate, worstRaw);
    }

    private BeachHistory? BuildHistoryFromMapa(Beach beach, List<ImaMapaPonto> allPontos)
    {
        var cityMunicipio = CityKeyByMunicipio
            .FirstOrDefault(kv => beach.Id.StartsWith(kv.Value + "-"));

        if (cityMunicipio.Key is null) return null;

        var pontosDoBalneario = allPontos
            .Where(p => string.Equals(p.Municipio, cityMunicipio.Key, StringComparison.OrdinalIgnoreCase))
            .Where(p => BuildId(
                CityKeyByMunicipio.GetValueOrDefault(p.Municipio, ""),
                p.Balneario) == beach.Id)
            .ToList();

        if (pontosDoBalneario.Count == 0) return null;

        var samples = pontosDoBalneario
            .SelectMany(p => p.Analises ?? [])
            .GroupBy(a => a.Data)
            .OrderByDescending(g => ParseDate(g.Key))
            .Take(5)
            .Select(g =>
            {
                var conditions = g.Select(a => ParseQuality(a.Condicao)).ToList();
                var worst = conditions.Contains(WaterQuality.Improper)
                    ? WaterQuality.Improper
                    : conditions.Contains(WaterQuality.Proper)
                        ? WaterQuality.Proper
                        : WaterQuality.Unknown;
                return new SampleEntry(
                    Date: g.Key ?? "-",
                    Quality: worst,
                    RawStatus: worst switch
                    {
                        WaterQuality.Improper => "IMPRÓPRIO",
                        WaterQuality.Proper => "PRÓPRIO",
                        _ => "-"
                    }
                );
            })
            .ToList();

        return samples.Count > 0
            ? new BeachHistory(beach.Id, beach.Name, samples)
            : null;
    }

    private static WaterQuality ParseQuality(string? condicao) =>
        condicao?.Trim().ToUpperInvariant() switch
        {
            "PRÓPRIO" or "PROPRIO" => WaterQuality.Proper,
            "IMPRÓPRIO" or "IMPROPRIO" => WaterQuality.Improper,
            _ => WaterQuality.Unknown
        };

    private static string BuildId(string cityKey, string beachName)
    {
        var slug = beachName
            .ToLowerInvariant()
            .Replace("á", "a").Replace("à", "a").Replace("ã", "a").Replace("â", "a")
            .Replace("é", "e").Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o").Replace("õ", "o").Replace("ô", "o")
            .Replace("ú", "u").Replace("ü", "u")
            .Replace("ç", "c")
            .Replace(" ", "-");
        return $"{cityKey}-{slug}";
    }

    private static (double Lat, double Lng)? ParseCoords(string? lat, string? lng)
    {
        if (string.IsNullOrWhiteSpace(lat) || string.IsNullOrWhiteSpace(lng))
            return null;

        if (double.TryParse(lat, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var latVal) &&
            double.TryParse(lng, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lngVal) &&
            latVal != 0 && lngVal != 0)
        {
            return (latVal, lngVal);
        }
        return null;
    }

    private static DateOnly ParseDate(string? dateStr)
    {
        if (dateStr is not null &&
            DateOnly.TryParseExact(dateStr, "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }
        return DateOnly.MinValue;
    }
}
