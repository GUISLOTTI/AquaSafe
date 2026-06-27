using System.Text.Json.Serialization;

namespace AquaSafe.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WaterQuality
{
    Proper,
    Improper,
    Unknown
}

public sealed record Beach(
    string   Id,
    string   Name,
    string   City,
    double   Latitude,
    double   Longitude,
    WaterQuality Quality,
    string   LastSampleDate,
    string   RawStatus
);

public sealed record BeachHistory(
    string          Id,
    string          Name,
    List<SampleEntry> Samples
);

public sealed record SampleEntry(
    string       Date,
    WaterQuality Quality,
    string       RawStatus
);

public sealed record RainAlert(
    bool   HasRecentRain,
    string Message,
    double PrecipitationMm,
    string Level,
    string Condition,
    string ConditionLabel
);

public sealed record CityCondition(
    string Condition,
    string ConditionLabel,
    double PrecipitationMm
);

public sealed record MarineCondition(
    double? WaveHeightM,
    double? WavePeriodS,
    double? WaveDirectionDeg,
    double? SeaTemperatureC,
    double? TidalHeightM,
    double? UvIndex
);

public sealed record CameraInfo(
    string  Source,
    string? EmbedUrl,
    string? ThumbnailUrl,
    string? ExternalUrl,
    string? Title
);
