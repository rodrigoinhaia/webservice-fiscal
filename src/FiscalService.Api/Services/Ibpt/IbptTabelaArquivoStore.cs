using FiscalService.Api.Config;

namespace FiscalService.Api.Services.Ibpt;

/// <summary>Carrega a tabela IBPT do disco (fallback quando a API De Olho no Imposto estiver indisponível).</summary>
public sealed class IbptTabelaArquivoStore
{
    private readonly FiscalConfig _config;
    private readonly ILogger<IbptTabelaArquivoStore> _logger;
    private readonly object _lock = new();
    private IReadOnlyDictionary<string, IbptAliquota>? _indice;

    public IbptTabelaArquivoStore(FiscalConfig config, ILogger<IbptTabelaArquivoStore> logger)
    {
        _config = config;
        _logger = logger;
    }

    public int QuantidadeRegistros => GarantirCarregado().Count;

    public string? Caminho => _config.Ibpt.ArquivoTabela;

    public bool Carregada => QuantidadeRegistros > 0;

    public IbptAliquota? Buscar(string ncm, string uf, int ex)
    {
        var indice = GarantirCarregado();
        if (indice.Count == 0)
            return null;

        var codigo = IbptTributoCalculator.NormalizarNcm(ncm);
        var ufNorm = (uf ?? "").Trim().ToUpperInvariant();

        if (indice.TryGetValue(Chave(codigo, ufNorm, ex), out var hit))
            return hit;
        if (ex != 0 && indice.TryGetValue(Chave(codigo, ufNorm, 0), out hit))
            return hit;
        if (indice.TryGetValue(Chave(codigo, "*", ex), out hit))
            return hit;
        if (ex != 0 && indice.TryGetValue(Chave(codigo, "*", 0), out hit))
            return hit;

        return null;
    }

    public void Recarregar()
    {
        lock (_lock)
        {
            _indice = null;
            GarantirCarregadoLocked();
        }
    }

    private IReadOnlyDictionary<string, IbptAliquota> GarantirCarregado()
    {
        var atual = _indice;
        if (atual is not null)
            return atual;
        lock (_lock)
            return GarantirCarregadoLocked();
    }

    private IReadOnlyDictionary<string, IbptAliquota> GarantirCarregadoLocked()
    {
        if (_indice is not null)
            return _indice;

        var path = _config.Ibpt.ArquivoTabela;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            if (!string.IsNullOrWhiteSpace(path))
                _logger.LogWarning("Tabela IBPT configurada mas arquivo não encontrado: {Path}", path);
            _indice = new Dictionary<string, IbptAliquota>();
            return _indice;
        }

        try
        {
            using var reader = new StreamReader(path);
            var uf = string.IsNullOrWhiteSpace(_config.Ibpt.UfTabela)
                ? InferirUfDoNome(path)
                : _config.Ibpt.UfTabela.Trim().ToUpperInvariant();
            var lista = IbptTabelaParser.Parse(reader, uf);
            var dict = new Dictionary<string, IbptAliquota>(StringComparer.Ordinal);
            foreach (var item in lista)
            {
                var ufItem = string.IsNullOrWhiteSpace(item.Uf) ? "*" : item.Uf;
                dict[Chave(item.Codigo, ufItem, item.Ex)] = item with { Uf = ufItem };
            }

            _indice = dict;
            _logger.LogInformation("Tabela IBPT carregada: {Path} ({Qtd} NCMs, UF={Uf})", path, dict.Count, uf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar tabela IBPT {Path}", path);
            _indice = new Dictionary<string, IbptAliquota>();
        }

        return _indice;
    }

    private static string InferirUfDoNome(string path)
    {
        var nome = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
        foreach (var uf in new[] { "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT", "PA", "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO" })
        {
            if (nome.Contains(uf, StringComparison.Ordinal))
                return uf;
        }

        return "*";
    }

    private static string Chave(string ncm, string uf, int ex) => $"{uf}|{ncm}|{ex}";
}
