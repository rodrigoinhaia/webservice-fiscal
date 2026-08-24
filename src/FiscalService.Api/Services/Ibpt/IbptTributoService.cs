using FiscalService.Api.Config;
using FiscalService.Api.Models.Requests;

namespace FiscalService.Api.Services.Ibpt;

/// <summary>
/// Orquestra o cálculo da Lei 12.741/2012 para uma nota: preenche
/// <see cref="ItemNFeRequest.ValorAproximadoTributos"/> e monta o texto de <c>infCpl</c>.
/// </summary>
public sealed class IbptTributoService
{
    private readonly FiscalConfig _config;
    private readonly IIbptAliquotaLookup _lookup;
    private readonly ILogger<IbptTributoService> _logger;

    public IbptTributoService(
        FiscalConfig config,
        IIbptAliquotaLookup lookup,
        ILogger<IbptTributoService> logger)
    {
        _config = config;
        _lookup = lookup;
        _logger = logger;
    }

    public async Task<IbptNotaResultado> AplicarAsync(
        ConfiguracaoEmitenteRequest emitente,
        IList<ItemNFeRequest> itens,
        bool? calcularOverride,
        CancellationToken ct)
    {
        var habilitado = calcularOverride ?? _config.Ibpt.Habilitado;
        if (!habilitado || itens.Count == 0)
            return new IbptNotaResultado { Aplicado = false };

        var token = ResolverToken(emitente);
        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(_config.Ibpt.ArquivoTabela))
        {
            const string msg = "IBPT habilitado mas sem token (emitente/global) e sem tabela local.";
            if (_config.Ibpt.Obrigatorio)
                throw new InvalidOperationException(msg);
            _logger.LogWarning(msg);
            return new IbptNotaResultado { Aplicado = false, Aviso = msg };
        }

        var credencial = new IbptCredencial(emitente.Cnpj, token ?? "");
        var uf = (emitente.Uf ?? "").Trim().ToUpperInvariant();
        var limite = Math.Clamp(_config.Ibpt.MaxConsultasParalelas, 1, 8);
        using var gate = new SemaphoreSlim(limite);

        var tarefas = itens.Select(async item =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return await CalcularUmAsync(credencial, uf, item, ct);
            }
            finally
            {
                gate.Release();
            }
        });

        var resultados = await Task.WhenAll(tarefas);

        var federal = 0m;
        var estadual = 0m;
        var municipal = 0m;
        var calculados = 0;
        var semAliquota = 0;
        string? fonte = null;
        string? versao = null;
        string? chaveIbpt = null;

        for (var i = 0; i < itens.Count; i++)
        {
            var r = resultados[i];
            if (r is null)
            {
                semAliquota++;
                continue;
            }

            calculados++;
            itens[i].ValorAproximadoTributos = r.Total;
            federal += r.Federal;
            estadual += r.Estadual;
            municipal += r.Municipal;
            fonte ??= r.Aliquota?.Fonte;
            versao ??= r.Aliquota?.Versao;
            chaveIbpt ??= r.Aliquota?.Chave;
        }

        if (calculados == 0)
        {
            const string aviso = "Nenhum NCM encontrado na tabela/API IBPT.";
            if (_config.Ibpt.Obrigatorio)
                throw new InvalidOperationException(aviso);
            _logger.LogWarning("IBPT: {Aviso} CNPJ={Cnpj} UF={Uf}", aviso, emitente.Cnpj, uf);
            return new IbptNotaResultado { Aplicado = false, Aviso = aviso, ItensSemAliquota = semAliquota };
        }

        for (var i = 0; i < itens.Count; i++)
        {
            if (resultados[i] is null)
                itens[i].ValorAproximadoTributos ??= 0;
        }

        var infCpl = _config.Ibpt.IncluirInfCpl
            ? IbptTributoCalculator.MontarInfCpl(federal, estadual, municipal, fonte, versao)
            : null;

        _logger.LogInformation(
            "IBPT aplicado: CNPJ={Cnpj} itens={Itens}/{Total} fed={Fed} est={Est} mun={Mun} fonte={Fonte}/{Versao}",
            emitente.Cnpj, calculados, itens.Count, federal, estadual, municipal, fonte, versao);

        return new IbptNotaResultado
        {
            Aplicado = true,
            Federal = federal,
            Estadual = estadual,
            Municipal = municipal,
            Total = federal + estadual + municipal,
            Fonte = fonte,
            Versao = versao,
            Chave = chaveIbpt,
            InfCpl = infCpl,
            ItensCalculados = calculados,
            ItensSemAliquota = semAliquota
        };
    }

    private async Task<IbptItemTributo?> CalcularUmAsync(
        IbptCredencial credencial,
        string uf,
        ItemNFeRequest item,
        CancellationToken ct)
    {
        var ncm = IbptTributoCalculator.NormalizarNcm(item.Ncm);
        if (ncm.Length == 0 || ncm == "00000000")
            return null;

        var aliquota = await _lookup.ObterAsync(
            credencial,
            new IbptConsultaChave(ncm, uf, item.NcmExcecao ?? 0),
            item.DescricaoProduto,
            item.UnidadeComercial,
            item.ValorTotalBruto,
            item.CodigoEan ?? "SEM GTIN",
            ct);

        if (aliquota is null)
            return null;

        return IbptTributoCalculator.CalcularItem(item, aliquota);
    }

    private string? ResolverToken(ConfiguracaoEmitenteRequest emitente)
    {
        if (!string.IsNullOrWhiteSpace(emitente.IbptToken))
            return emitente.IbptToken.Trim();
        return string.IsNullOrWhiteSpace(_config.Ibpt.Token) ? null : _config.Ibpt.Token.Trim();
    }
}
