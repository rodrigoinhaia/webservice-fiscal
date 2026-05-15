using NFe.Classes.Informacoes.Identificacao.Tipos;

namespace FiscalService.Api.Services.Fiscal;

/// <summary>Mapeia tipo de emissão da API para <see cref="TipoEmissao"/> (DFe.NET / layout NF-e 4.0).</summary>
public static class ContingenciaEmissaoMapper
{
    public static TipoEmissao Resolver(string? tipoEmissao)
    {
        if (string.IsNullOrWhiteSpace(tipoEmissao))
            return TipoEmissao.teNormal;

        var t = tipoEmissao.Trim().ToUpperInvariant().Replace("-", "").Replace("_", "");

        return t switch
        {
            "NORMAL" or "1" => TipoEmissao.teNormal,
            "SVCAN" or "6" => TipoEmissao.teSVCAN,
            "SVCRS" or "7" => TipoEmissao.teSVCRS,
            "OFFLINE" or "9" => TipoEmissao.teOffLine,
            _ => throw new ArgumentException(
                $"tipoEmissao '{tipoEmissao}' não suportado. Use: Normal, SVC-AN, SVC-RS ou Offline.")
        };
    }

    public static bool ExigeContingencia(TipoEmissao tipo) =>
        tipo is not TipoEmissao.teNormal;
}
