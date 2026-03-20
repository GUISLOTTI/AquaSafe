namespace AquaSafe.Api.Models;

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
    string Message
);
