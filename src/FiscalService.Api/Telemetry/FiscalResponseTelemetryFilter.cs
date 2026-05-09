using FiscalService.Api.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FiscalService.Api.Telemetry;

/// <summary>
/// Registra métricas de resultado SEFAZ (<see cref="FiscalResponse.CodigoStatus"/>) por operação de controller.
/// </summary>
public sealed class FiscalResponseTelemetryFilter : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: FiscalResponse fr })
        {
            var controller = context.RouteData.Values["controller"]?.ToString() ?? "?";
            var action = context.RouteData.Values["action"]?.ToString() ?? "?";
            var status = fr.CodigoStatus ?? fr.Erro?.Tipo ?? "";
            FiscalTelemetry.RecordSefazOutcome($"{controller}.{action}", fr.Sucesso, status);
        }

        return next();
    }
}
