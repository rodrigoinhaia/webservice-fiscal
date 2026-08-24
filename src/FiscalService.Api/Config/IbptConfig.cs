namespace FiscalService.Api.Config;

/// <summary>
/// Integração Lei 12.741/2012 (De Olho no Imposto / IBPT).
/// Token é por CNPJ — preferir cadastro do emitente; este é o fallback global.
/// </summary>
public sealed class IbptConfig
{
    public bool Habilitado { get; set; } = true;

    /// <summary>Token global (env <c>Fiscal__Ibpt__Token</c>). Não versionar.</summary>
    public string? Token { get; set; }

    public string UrlProdutos { get; set; } = "https://apidoni.ibpt.org.br/api/v1/produtos";

    public string UrlServicos { get; set; } = "https://apidoni.ibpt.org.br/api/v1/servicos";

    public int TimeoutSegundos { get; set; } = 8;

    /// <summary>TTL do cache em memória das alíquotas (NCM+UF+EX).</summary>
    public int CacheMinutos { get; set; } = 1440;

    /// <summary>Se true, a emissão falha quando não for possível calcular os tributos aproximados.</summary>
    public bool Obrigatorio { get; set; }

    /// <summary>Anexa o texto da Lei 12.741/2012 em <c>infCpl</c>.</summary>
    public bool IncluirInfCpl { get; set; } = true;

    /// <summary>CSV/TXT da tabela IBPT (download no portal). Fallback quando a API estiver fora.</summary>
    public string? ArquivoTabela { get; set; }

    /// <summary>UF da tabela local quando o arquivo não tiver coluna UF (tabelas oficiais são por estado).</summary>
    public string? UfTabela { get; set; }

    /// <summary>Pasta de upload da tabela (Docker: <c>/app/ibpt</c>).</summary>
    public string? Diretorio { get; set; }

    public int MaxConsultasParalelas { get; set; } = 4;

    public string ResolverDiretorio()
    {
        if (!string.IsNullOrWhiteSpace(ArquivoTabela))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(ArquivoTabela));
            if (!string.IsNullOrWhiteSpace(dir))
                return dir;
        }

        if (!string.IsNullOrWhiteSpace(Diretorio))
            return Path.GetFullPath(Diretorio);

        if (Directory.Exists("/app/ibpt"))
            return "/app/ibpt";

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "ibpt"));
    }

    public string ResolverCaminhoArquivo(string? uf = null)
    {
        var ufNorm = (uf ?? "").Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(ArquivoTabela) && (string.IsNullOrEmpty(ufNorm) || ufNorm == "*"))
            return Path.GetFullPath(ArquivoTabela);

        var dir = ResolverDiretorio();
        var nome = string.IsNullOrEmpty(ufNorm) || ufNorm == "*"
            ? "TabelaIBPTax.csv"
            : $"TabelaIBPTax{ufNorm}.csv";
        return Path.Combine(dir, nome);
    }
}
