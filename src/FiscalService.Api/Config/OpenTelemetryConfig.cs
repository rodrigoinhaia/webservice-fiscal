namespace FiscalService.Api.Config;

/// <summary>Exportação OTLP (métricas + traces). Também respeita <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> padrão OpenTelemetry.</summary>
public sealed class OpenTelemetryConfig
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>Liga exportação OTLP quando há endpoint (este campo ou variável de ambiente).</summary>
    public bool Enabled { get; set; }

    /// <summary>Ex.: <c>http://localhost:4317</c> (gRPC). Se vazio, usa <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>.</summary>
    public string? OtlpEndpoint { get; set; }
}
