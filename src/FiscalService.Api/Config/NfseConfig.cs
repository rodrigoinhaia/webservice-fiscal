namespace FiscalService.Api.Config;

/// <summary>Configuração do módulo NFS-e Padrão Nacional (OpenAC).</summary>
public sealed class NfseConfig
{
    public const string SectionName = "Fiscal:NFSe";

    /// <summary>Quando false, endpoints /api/nfse retornam 404.</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>Versão do layout DPS: Ve100 ou Ve101.</summary>
    public string VersaoDps { get; set; } = "Ve101";

    /// <summary>Diretório dos XSD da NFS-e Nacional (separado dos schemas DFe.NET).</summary>
    public string DiretorioSchemas { get; set; } = "/app/schemas/nfse";

    /// <summary>Timeout em segundos para chamadas REST ADN/Sefin (0 = usa Fiscal:TimeoutWs).</summary>
    public int TimeoutWs { get; set; }

    /// <summary>
    /// Valida XML da DPS contra XSD local antes do envio.
    /// Default false: validador OpenAC falha em .NET 8 (infDPS/namespace) — SEFIN valida no servidor.
    /// </summary>
    public bool ValidarSchemas { get; set; }
}
