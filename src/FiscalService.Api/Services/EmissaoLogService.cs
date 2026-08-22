using FiscalService.Api.Config;
using FiscalService.Api.Data;
using FiscalService.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace FiscalService.Api.Services;

/// <summary>
/// Consulta o histórico de emissões registradas em <c>emissao_logs</c>.
/// Suporta filtros por CNPJ, modelo, série, ambiente, status, chave e janela de datas.
/// </summary>
public sealed class EmissaoLogService
{
    public const int TamanhoPaginaMinimo = 1;
    public const int TamanhoPaginaMaximo = 200;
    public const int TamanhoPaginaDefault = 50;

    private readonly AppDbContext _db;
    private readonly FiscalConfig _fiscalConfig;

    public EmissaoLogService(AppDbContext db, FiscalConfig fiscalConfig)
    {
        _db = db;
        _fiscalConfig = fiscalConfig;
    }

    public async Task<PagedResponse<EmissaoLogResponse>> ListarAsync(
        string? cnpj,
        string? modelo,
        string? serie,
        string? ambiente,
        string? status,
        string? chaveAcesso,
        DateTime? dataDe,
        DateTime? dataAte,
        int pagina,
        int tamanhoPagina,
        CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanhoPagina = Math.Clamp(tamanhoPagina, TamanhoPaginaMinimo, TamanhoPaginaMaximo);

        var query = _db.EmissaoLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(cnpj))
            query = query.Where(e => e.Cnpj == cnpj.Trim());
        if (!string.IsNullOrWhiteSpace(modelo))
            query = query.Where(e => e.Modelo == modelo.Trim());
        if (!string.IsNullOrWhiteSpace(serie))
            query = query.Where(e => e.Serie == serie.Trim());
        if (!string.IsNullOrWhiteSpace(ambiente))
            query = query.Where(e => e.Ambiente == ambiente.Trim());
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(chaveAcesso))
            query = query.Where(e => e.ChaveAcesso == chaveAcesso.Trim());
        if (dataDe is { } de)
            query = query.Where(e => e.DataEmissao >= de);
        if (dataAte is { } ate)
            query = query.Where(e => e.DataEmissao <= ate);

        var total = await query.LongCountAsync(ct);

        var itens = await query
            .OrderByDescending(e => e.DataEmissao)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(e => new EmissaoLogResponse
            {
                Id = e.Id,
                Cnpj = e.Cnpj,
                Modelo = e.Modelo,
                Serie = e.Serie,
                Numero = e.Numero,
                ChaveAcesso = e.ChaveAcesso,
                Protocolo = e.Protocolo,
                Status = e.Status,
                CodigoStatus = e.CodigoStatus,
                MensagemStatus = e.MensagemStatus,
                Ambiente = e.Ambiente,
                DataEmissao = e.DataEmissao,
                DataProcessamento = e.DataProcessamento
            })
            .ToListAsync(ct);

        return new PagedResponse<EmissaoLogResponse>
        {
            Itens = itens,
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina,
            Total = total
        };
    }

    public async Task<EmissaoLogResponse?> ObterPorChaveAsync(string chaveAcesso, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chaveAcesso)) return null;

        return await _db.EmissaoLogs
            .AsNoTracking()
            .Where(e => e.ChaveAcesso == chaveAcesso.Trim())
            .OrderByDescending(e => e.DataProcessamento)
            .Select(e => new EmissaoLogResponse
            {
                Id = e.Id,
                Cnpj = e.Cnpj,
                Modelo = e.Modelo,
                Serie = e.Serie,
                Numero = e.Numero,
                ChaveAcesso = e.ChaveAcesso,
                Protocolo = e.Protocolo,
                Status = e.Status,
                CodigoStatus = e.CodigoStatus,
                MensagemStatus = e.MensagemStatus,
                Ambiente = e.Ambiente,
                DataEmissao = e.DataEmissao,
                DataProcessamento = e.DataProcessamento
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Obtém o XML autorizado pela chave de acesso: primeiro <c>XmlPath</c> do log mais recente,
    /// depois busca em <see cref="FiscalConfig.DiretorioXmls"/> por nome contendo a chave.
    /// </summary>
    public async Task<EmissaoXmlResponse?> ObterXmlPorChaveAsync(string chaveAcesso, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chaveAcesso)) return null;

        var chave = chaveAcesso.Trim();

        var xmlPath = await _db.EmissaoLogs
            .AsNoTracking()
            .Where(e => e.ChaveAcesso == chave)
            .OrderByDescending(e => e.DataProcessamento)
            .Select(e => e.XmlPath)
            .FirstOrDefaultAsync(ct);

        string? xml = null;

        if (!string.IsNullOrWhiteSpace(xmlPath) && File.Exists(xmlPath))
            xml = await File.ReadAllTextAsync(xmlPath, ct);
        else
            xml = await BuscarXmlNoDiretorioAsync(chave, ct);

        if (string.IsNullOrWhiteSpace(xml))
            return null;

        return new EmissaoXmlResponse
        {
            Sucesso = true,
            ChaveAcesso = chave,
            ContentType = "application/xml",
            Xml = xml
        };
    }

    private async Task<string?> BuscarXmlNoDiretorioAsync(string chave, CancellationToken ct)
    {
        var dir = _fiscalConfig.DiretorioXmls;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return null;

        var candidatos = new[]
        {
            Path.Combine(dir, $"{chave}-procNFe.xml"),
            Path.Combine(dir, $"{chave}-nfe.xml"),
            Path.Combine(dir, $"{chave}.xml"),
        };

        foreach (var path in candidatos)
        {
            if (File.Exists(path))
                return await File.ReadAllTextAsync(path, ct);
        }

        string? encontrado = null;
        try
        {
            encontrado = Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f)
                    .Contains(chave, StringComparison.OrdinalIgnoreCase));
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        if (encontrado is null)
            return null;

        return await File.ReadAllTextAsync(encontrado, ct);
    }
}
