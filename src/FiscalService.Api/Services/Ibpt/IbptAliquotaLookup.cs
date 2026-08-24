using FiscalService.Api.Config;
using Microsoft.Extensions.Caching.Memory;

namespace FiscalService.Api.Services.Ibpt;

public interface IIbptAliquotaLookup
{
    Task<IbptAliquota?> ObterAsync(
        IbptCredencial credencial,
        IbptConsultaChave chave,
        string descricao,
        string unidade,
        decimal valor,
        string gtin,
        CancellationToken ct);
}

/// <summary>Resolve alíquota: cache → tabela local → API IBPT.</summary>
public sealed class IbptAliquotaLookup : IIbptAliquotaLookup
{
    private readonly IMemoryCache _cache;
    private readonly IbptTabelaArquivoStore _tabela;
    private readonly IbptApiClient _api;
    private readonly FiscalConfig _config;
    private readonly IbptCacheStamp _cacheStamp;
    private readonly ILogger<IbptAliquotaLookup> _logger;

    public IbptAliquotaLookup(
        IMemoryCache cache,
        IbptTabelaArquivoStore tabela,
        IbptApiClient api,
        FiscalConfig config,
        IbptCacheStamp cacheStamp,
        ILogger<IbptAliquotaLookup> logger)
    {
        _cache = cache;
        _tabela = tabela;
        _api = api;
        _config = config;
        _cacheStamp = cacheStamp;
        _logger = logger;
    }

    public async Task<IbptAliquota?> ObterAsync(
        IbptCredencial credencial,
        IbptConsultaChave chave,
        string descricao,
        string unidade,
        decimal valor,
        string gtin,
        CancellationToken ct)
    {
        var cacheKey = $"ibpt:{_cacheStamp.Geracao}:{chave.Uf}|{chave.Ncm}|{chave.Ex}";
        if (_cache.TryGetValue(cacheKey, out IbptAliquota? cached) && cached is not null)
            return cached;

        var daTabela = _tabela.Buscar(chave.Ncm, chave.Uf, chave.Ex);
        if (daTabela is not null)
        {
            Guardar(cacheKey, daTabela);
            return daTabela;
        }

        var daApi = await _api.ConsultarProdutoAsync(credencial, chave, descricao, unidade, valor, gtin, ct);
        if (daApi is not null)
        {
            Guardar(cacheKey, daApi);
            return daApi;
        }

        _logger.LogDebug("IBPT sem alíquota para NCM {Ncm} UF {Uf} EX {Ex}", chave.Ncm, chave.Uf, chave.Ex);
        return null;
    }

    private void Guardar(string key, IbptAliquota valor)
    {
        var minutos = Math.Clamp(_config.Ibpt.CacheMinutos, 5, 60 * 24 * 40);
        _cache.Set(key, valor, TimeSpan.FromMinutes(minutos));
    }
}
