using System.Diagnostics.Metrics;

namespace FiscalService.Api.Telemetry;

/// <summary>Métricas e (opcional) tracing para operações fiscais / SEFAZ.</summary>
public static class FiscalTelemetry
{
    public const string MeterName = "FiscalService";

    private static readonly Meter Meter = new(MeterName, typeof(FiscalTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0");
    private static readonly Counter<long> SefazOutcomes = Meter.CreateCounter<long>("fiscal.sefaz.outcomes");

    /// <summary>Ex.: <c>NFeController.Emitir</c> (controller + action).</summary>
    public static void RecordSefazOutcome(string operation, bool sucesso, string? cStatOuTipo)
    {
        SefazOutcomes.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("sucesso", sucesso),
            new KeyValuePair<string, object?>("cstat", cStatOuTipo ?? ""));
    }
}
