namespace Wasla.Api.Configuration;

/// <summary>OpenTelemetry settings.</summary>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public string ServiceName { get; set; } = "wasla-api";

    /// <summary>OTLP exporter endpoint (e.g. http://localhost:4317). Null disables exporting.</summary>
    public string? OtlpEndpoint { get; set; }
}
