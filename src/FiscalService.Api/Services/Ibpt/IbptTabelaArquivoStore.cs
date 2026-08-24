using FiscalService.Api.Config;

namespace FiscalService.Api.Services.Ibpt;

/// <summary>Carrega a tabela IBPT do disco (fallback quando a API De Olho no Imposto estiver indisponível).</summary>
public sealed class IbptTabelaArquivoStore
{
    private readonly FiscalConfig _config;
    private readonly IbptCacheStamp _cacheStamp;
    private readonly ILogger<IbptTabelaArquivoStore> _logger;
    private readonly object _lock = new();
    private IReadOnlyDictionary<string, IbptAliquota>? _indice;

    public IbptTabelaArquivoStore(FiscalConfig config, IbptCacheStamp cacheStamp, ILogger<IbptTabelaArquivoStore> logger)
    {
        _config = config;
        _cacheStamp = cacheStamp;
        _logger = logger;
    }

    public int QuantidadeRegistros => GarantirCarregado().Count;

    public string? Caminho
    {
        get
        {
            var path = _config.Ibpt.ResolverCaminhoArquivo(_config.Ibpt.UfTabela);
            return File.Exists(path) ? path : _config.Ibpt.ArquivoTabela;
        }
    }

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
        _cacheStamp.Invalidar();
    }

    /// <summary>Valida, grava no disco e recarrega a tabela enviada pelo painel/API.</summary>
    public IbptTabelaUploadInterno Importar(Stream conteudo, string? uf)
    {
        using var buffer = new MemoryStream();
        conteudo.CopyTo(buffer);
        if (buffer.Length == 0)
            return new IbptTabelaUploadInterno(false, 0, null, uf, null, null, "Arquivo vazio.");

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var ufNorm = string.IsNullOrWhiteSpace(uf) ? _config.Ibpt.UfTabela : uf.Trim().ToUpperInvariant();
        var lista = IbptTabelaParser.Parse(reader, ufNorm);
        if (lista.Count == 0)
            return new IbptTabelaUploadInterno(false, 0, null, ufNorm, null, null,
                "Nenhum NCM válido no arquivo. Use o CSV do portal De Olho no Imposto (separador ;).");

        var path = _config.Ibpt.ResolverCaminhoArquivo(ufNorm);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        buffer.Position = 0;
        using (var fs = File.Create(tmp))
            buffer.CopyTo(fs);
        File.Move(tmp, path, overwrite: true);

        _config.Ibpt.ArquivoTabela = path;
        if (!string.IsNullOrWhiteSpace(ufNorm) && ufNorm != "*")
            _config.Ibpt.UfTabela = ufNorm;

        lock (_lock)
            _indice = Indexar(lista);

        _cacheStamp.Invalidar();
        var amostra = lista[0];
        _logger.LogInformation("Tabela IBPT importada: {Path} ({Qtd} NCMs, UF={Uf}, versao={Versao})",
            path, lista.Count, ufNorm, amostra.Versao);

        return new IbptTabelaUploadInterno(true, lista.Count, path, ufNorm, amostra.Versao, amostra.Fonte, "Tabela importada.");
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

        var path = _config.Ibpt.ResolverCaminhoArquivo(_config.Ibpt.UfTabela);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            if (!string.IsNullOrWhiteSpace(_config.Ibpt.ArquivoTabela) && !File.Exists(_config.Ibpt.ArquivoTabela))
                _logger.LogWarning("Tabela IBPT configurada mas arquivo não encontrado: {Path}", _config.Ibpt.ArquivoTabela);
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
            _indice = Indexar(lista);
            _logger.LogInformation("Tabela IBPT carregada: {Path} ({Qtd} NCMs, UF={Uf})", path, _indice.Count, uf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar tabela IBPT {Path}", path);
            _indice = new Dictionary<string, IbptAliquota>();
        }

        return _indice;
    }

    private static Dictionary<string, IbptAliquota> Indexar(IReadOnlyList<IbptAliquota> lista)
    {
        var dict = new Dictionary<string, IbptAliquota>(StringComparer.Ordinal);
        foreach (var item in lista)
        {
            var ufItem = string.IsNullOrWhiteSpace(item.Uf) ? "*" : item.Uf;
            dict[Chave(item.Codigo, ufItem, item.Ex)] = item with { Uf = ufItem };
        }

        return dict;
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

public sealed record IbptTabelaUploadInterno(
    bool Sucesso,
    int Registros,
    string? Caminho,
    string? Uf,
    string? Versao,
    string? Fonte,
    string Mensagem);
