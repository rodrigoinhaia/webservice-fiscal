namespace FiscalService.Api.Config;

public sealed class FiscalConfig
{
    public const string SectionName = "Fiscal";

    /// <summary>Ambiente SEFAZ: "Homologacao" ou "Producao".</summary>
    public string Ambiente { get; set; } = "Homologacao";

    /// <summary>Persiste os XMLs autorizados em disco.</summary>
    public bool SalvarXmls { get; set; } = true;

    /// <summary>Diretório onde os XMLs emitidos são salvos.</summary>
    public string DiretorioXmls { get; set; } = "/app/xmls";

    /// <summary>Diretório dos schemas XSD do DFe.NET.</summary>
    public string DiretorioSchemas { get; set; } = "/app/schemas";

    /// <summary>Diretório dos certificados .pfx.</summary>
    public string DiretorioCertificados { get; set; } = "/app/certificados";

    /// <summary>Timeout em segundos para chamadas ao WebService da SEFAZ.</summary>
    public int TimeoutWs { get; set; } = 30;

    /// <summary>Dias antes do vencimento para o /health reportar status degradado.</summary>
    public int DiasAlertaCertificado { get; set; } = 30;

    /// <summary>Retentativas em falhas transitórias de rede/timeout nas chamadas SEFAZ.</summary>
    public bool SefazRetryHabilitado { get; set; } = true;

    /// <summary>Número máximo de tentativas (inclui a primeira chamada).</summary>
    public int SefazRetryMaxTentativas { get; set; } = 3;

    /// <summary>Intervalo base em ms entre tentativas (multiplicado pelo número da tentativa).</summary>
    public int SefazRetryIntervaloMs { get; set; } = 1000;

    /// <summary>Resolve um path de certificado relativo para absoluto.</summary>
    public string ResolveCertificadoPath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        return Path.Combine(DiretorioCertificados, path);
    }
}
