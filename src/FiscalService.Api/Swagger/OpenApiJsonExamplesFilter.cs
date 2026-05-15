using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FiscalService.Api.Swagger;

/// <summary>Adiciona exemplos JSON de <c>docs/exemplos</c> às operações do Swagger.</summary>
public sealed class OpenApiJsonExamplesFilter : IOperationFilter
{
    private static readonly Dictionary<string, string> Mapa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["POST:/api/nfe/emitir"] = "nfe/crt1-simples-csosn102-homologacao.json",
        ["POST:/api/nfe/emitir:emitente"] = "nfe/emitir-via-emitente-cnpj.json",
        ["POST:/api/emitentes"] = "emitente/cadastro-homologacao.json",
        ["POST:/api/nfe/distribuicao-dfe"] = "nfe/distribuicao-dfe.json",
        ["POST:/api/nfe/manifestar-destinatario"] = "nfe/manifestar-ciencia.json",
        ["POST:/api/nfe/cancelar"] = "nfe/cancelar-homologacao.json",
        ["POST:/api/nfe/carta-correcao"] = "nfe/carta-correcao-homologacao.json",
        ["POST:/api/nfce/emitir"] = "nfce/emitir-homologacao-csc.json"
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metodo = context.ApiDescription.HttpMethod?.ToUpperInvariant() ?? "GET";
        var rota = "/" + (context.ApiDescription.RelativePath ?? "").TrimStart('/');
        var chave = $"{metodo}:{rota}";

        string? relativo = Mapa.GetValueOrDefault(chave);
        if (relativo is null && chave == "POST:/api/nfe/emitir")
            relativo = Mapa["POST:/api/nfe/emitir"];

        if (relativo is null) return;

        var json = CarregarExemplo(relativo);
        if (json is null) return;

        foreach (var content in operation.RequestBody?.Content.Values ?? [])
            content.Example = OpenApiAnyFactory.CreateFromJson(json);
    }

    private static string? CarregarExemplo(string caminhoRelativo)
    {
        var bases = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "docs", "exemplos"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "exemplos"),
            Path.Combine(Directory.GetCurrentDirectory(), "docs", "exemplos")
        };

        foreach (var baseDir in bases)
        {
            var full = Path.GetFullPath(Path.Combine(baseDir, caminhoRelativo));
            if (File.Exists(full))
                return File.ReadAllText(full);
        }

        return null;
    }
}
