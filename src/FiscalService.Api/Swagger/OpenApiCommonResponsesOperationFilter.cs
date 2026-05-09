using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FiscalService.Api.Swagger;

/// <summary>
/// Documenta respostas HTTP comuns (autenticação, validação, limite, falha fiscal) no OpenAPI.
/// </summary>
public sealed class OpenApiCommonResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext _)
    {
        TryAdd(operation, "400",
            "Validação de entrada (DataAnnotations / FluentValidation). Corpo: ProblemDetails ou ModelState.");
        TryAdd(operation, "401",
            "Header `X-Api-Key` ausente ou não aceito. Corpo JSON: `{ \"sucesso\": false, \"erro\": { \"tipo\": \"NaoAutorizado\", ... } }`.");
        TryAdd(operation, "422",
            "Falha de negócio fiscal ou SEFAZ (`FiscalResponse` com `sucesso: false`).");
        TryAdd(operation, "429",
            "Limite de requisições por IP (rate limiting). Aguarde a próxima janela.");
    }

    private static void TryAdd(OpenApiOperation operation, string code, string description)
    {
        if (operation.Responses.ContainsKey(code))
            return;

        operation.Responses.Add(code, new OpenApiResponse { Description = description });
    }
}
