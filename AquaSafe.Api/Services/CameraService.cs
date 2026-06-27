using System.Text.Json;
using System.Text.Json.Serialization;
using AquaSafe.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AquaSafe.Api.Services;

file sealed record WindyResponse(
    [property: JsonPropertyName("webcams")] List<WindyWebcam>? Webcams
);

file sealed record WindyWebcam(
    [property: JsonPropertyName("title")]  string? Title,
    [property: JsonPropertyName("images")] WindyImages? Images,
    [property: JsonPropertyName("player")] WindyPlayer? Player,
    [property: JsonPropertyName("viewCount")] int? ViewCount
);

file sealed record WindyImages(
    [property: JsonPropertyName("current")] WindyImageSet? Current
);

file sealed record WindyImageSet(
    [property: JsonPropertyName("preview")] string? Preview
);

file sealed record WindyPlayer(
    [property: JsonPropertyName("day")] string? Day
);

public sealed class CameraService(
    HttpClient httpClient,
    IMemoryCache cache,
    IConfiguration config,
    ILogger<CameraService> logger)
{
    private static readonly TimeSpan WindyCacheTtl = TimeSpan.FromMinutes(9);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CameraInfo> GetCameraAsync(string beachId, double lat, double lng, string beachName, CancellationToken ct = default)
    {
        var cacheKey = $"camera:{beachId}";
        if (cache.TryGetValue(cacheKey, out CameraInfo? cached) && cached is not null)
            return cached;

        var camera = await ResolveCameraAsync(lat, lng, beachName, ct);

        if (camera.Source == "windy")
        {
            cache.Set(cacheKey, camera, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = WindyCacheTtl
            });
        }

        return camera;
    }

    private async Task<CameraInfo> ResolveCameraAsync(double lat, double lng, string beachName, CancellationToken ct)
    {
        var apiKey = config["Windy:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var windy = await FetchWindyAsync(apiKey, lat, lng, ct);
            if (windy is not null) return windy;
        }

        var query = Uri.EscapeDataString($"{beachName} ao vivo");
        return new CameraInfo(
            Source:       "search",
            EmbedUrl:     null,
            ThumbnailUrl: null,
            ExternalUrl:  $"https://www.youtube.com/results?search_query={query}",
            Title:        $"Buscar transmissões de {beachName} no YouTube"
        );
    }

    private async Task<CameraInfo?> FetchWindyAsync(string apiKey, double lat, double lng, CancellationToken ct)
    {
        try
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var url = $"https://api.windy.com/webcams/api/v3/webcams" +
                      $"?nearby={lat.ToString(inv)},{lng.ToString(inv)},20" +
                      $"&limit=1&include=images,player,location" +
                      $"&categories=beach,coast";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("x-windy-api-key", apiKey);

            var resp = await httpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<WindyResponse>(json, JsonOpts);

            var cam = data?.Webcams?.FirstOrDefault();
            if (cam is null) return null;

            return new CameraInfo(
                Source:       "windy",
                EmbedUrl:     cam.Player?.Day,
                ThumbnailUrl: cam.Images?.Current?.Preview,
                ExternalUrl:  cam.Player?.Day,
                Title:        cam.Title ?? "Câmera ao vivo"
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha na Windy Webcams API.");
            return null;
        }
    }
}
