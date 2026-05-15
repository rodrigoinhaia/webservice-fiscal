using System.Text.RegularExpressions;
using FiscalService.Api.Data;
using FiscalService.Api.Data.Entities;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace FiscalService.Api.Services;

public sealed class EmitenteService
{
    private readonly AppDbContext _db;
    private readonly CertificadoService _certificadoService;
    private readonly CertificadoSenhaProtector _senhaProtector;
    private readonly ILogger<EmitenteService> _logger;

    public EmitenteService(
        AppDbContext db,
        CertificadoService certificadoService,
        CertificadoSenhaProtector senhaProtector,
        ILogger<EmitenteService> logger)
    {
        _db = db;
        _certificadoService = certificadoService;
        _senhaProtector = senhaProtector;
        _logger = logger;
    }

    public async Task<EmitenteResponse> CriarAsync(EmitenteCadastroRequest request, CancellationToken ct = default)
    {
        var cnpj = SomenteDigitos(request.Cnpj);
        if (cnpj.Length != 14)
            throw new ArgumentException("CNPJ deve conter 14 dígitos.");

        if (await _db.Emitentes.AnyAsync(e => e.Cnpj == cnpj, ct))
            throw new InvalidOperationException($"Emitente com CNPJ {cnpj} já cadastrado.");

        if (request.ValidarCnpjCertificado)
            await ValidarCnpjCertificadoAsync(cnpj, request.CertificadoPath, request.CertificadoSenha);

        var agora = DateTime.UtcNow;
        var entidade = MapearParaEntidade(new Emitente(), request, cnpj, agora);
        entidade.CertificadoSenhaProtegida = _senhaProtector.Proteger(request.CertificadoSenha);

        _db.Emitentes.Add(entidade);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Emitente cadastrado: CNPJ={Cnpj}", cnpj);
        return MapearParaResponse(entidade);
    }

    public async Task<EmitenteResponse?> ObterPorCnpjAsync(string cnpj, CancellationToken ct = default)
    {
        var digits = SomenteDigitos(cnpj);
        var entidade = await _db.Emitentes.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Cnpj == digits && e.Ativo, ct);
        return entidade is null ? null : MapearParaResponse(entidade);
    }

    public async Task<EmitenteListaResponse> ListarAsync(int pagina, int tamanhoPagina, bool? ativo, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanhoPagina = Math.Clamp(tamanhoPagina, 1, 200);

        var query = _db.Emitentes.AsNoTracking();
        if (ativo.HasValue)
            query = query.Where(e => e.Ativo == ativo.Value);

        var total = await query.CountAsync(ct);
        var itens = await query
            .OrderBy(e => e.RazaoSocial)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);

        return new EmitenteListaResponse
        {
            Itens = itens.Select(MapearParaResponse).ToList(),
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina,
            Total = total
        };
    }

    public async Task<EmitenteResponse?> AtualizarAsync(string cnpj, EmitenteAtualizarRequest request, CancellationToken ct = default)
    {
        var digits = SomenteDigitos(cnpj);
        var entidade = await _db.Emitentes.FirstOrDefaultAsync(e => e.Cnpj == digits, ct);
        if (entidade is null) return null;

        if (!string.IsNullOrWhiteSpace(request.RazaoSocial)) entidade.RazaoSocial = request.RazaoSocial.Trim();
        if (request.NomeFantasia is not null) entidade.NomeFantasia = request.NomeFantasia;
        if (request.Ie is not null) entidade.Ie = request.Ie;
        if (request.Crt is { } crt) entidade.Crt = crt;
        if (!string.IsNullOrWhiteSpace(request.Uf)) entidade.Uf = request.Uf.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.Ambiente)) entidade.Ambiente = request.Ambiente;
        if (!string.IsNullOrWhiteSpace(request.CertificadoPath)) entidade.CertificadoPath = request.CertificadoPath.Trim();

        var senhaNova = request.CertificadoSenha;
        var pathCert = entidade.CertificadoPath;
        if (!string.IsNullOrWhiteSpace(senhaNova))
        {
            if (request.ValidarCnpjCertificado)
                await ValidarCnpjCertificadoAsync(digits, pathCert, senhaNova);
            entidade.CertificadoSenhaProtegida = _senhaProtector.Proteger(senhaNova);
        }

        if (request.Endereco is not null)
            AplicarEndereco(entidade, request.Endereco);

        if (request.Ativo.HasValue) entidade.Ativo = request.Ativo.Value;
        entidade.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Emitente atualizado: CNPJ={Cnpj}", digits);
        return MapearParaResponse(entidade);
    }

    public async Task<bool> DesativarAsync(string cnpj, CancellationToken ct = default)
    {
        var digits = SomenteDigitos(cnpj);
        var entidade = await _db.Emitentes.FirstOrDefaultAsync(e => e.Cnpj == digits, ct);
        if (entidade is null) return false;

        entidade.Ativo = false;
        entidade.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ConfiguracaoEmitenteRequest> ResolverConfiguracaoAsync(
        IEmitenteConfigSource source,
        CancellationToken ct = default)
    {
        var cnpjRef = SomenteDigitos(source.EmitenteCnpj ?? source.ConfiguracaoEmitente?.Cnpj ?? "");
        if (string.IsNullOrEmpty(cnpjRef))
            throw new ArgumentException("Informe emitenteCnpj ou configuracaoEmitente.cnpj.");

        var overrideConfig = source.ConfiguracaoEmitente;
        var usarCadastro = !string.IsNullOrWhiteSpace(source.EmitenteCnpj)
            || overrideConfig is null
            || string.IsNullOrWhiteSpace(overrideConfig.CertificadoSenha);

        if (usarCadastro)
        {
            var entidade = await _db.Emitentes.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Cnpj == cnpjRef && e.Ativo, ct)
                ?? throw new KeyNotFoundException($"Emitente {cnpjRef} não encontrado ou inativo. Cadastre em POST /api/emitentes.");

            var config = EntidadeParaConfig(entidade);
            if (overrideConfig is not null)
                MesclarOverride(config, overrideConfig, cnpjRef);
            return config;
        }

        if (overrideConfig is null)
            throw new ArgumentException("configuracaoEmitente é obrigatório quando emitenteCnpj não é informado.");

        overrideConfig.Cnpj = cnpjRef;
        return overrideConfig;
    }

    private async Task ValidarCnpjCertificadoAsync(string cnpj, string certificadoPath, string senha)
    {
        var validacao = _certificadoService.Validar(new CertificadoValidarRequest
        {
            CertificadoBase64 = Convert.ToBase64String(
                await File.ReadAllBytesAsync(_certificadoService.ResolvePath(certificadoPath))),
            Senha = senha
        });

        if (!validacao.Valido)
            throw new InvalidOperationException(validacao.Erro?.Mensagem ?? "Certificado inválido.");

        if (!string.IsNullOrEmpty(validacao.Cnpj) && validacao.Cnpj != cnpj)
            throw new InvalidOperationException(
                $"CNPJ do certificado ({validacao.Cnpj}) diverge do CNPJ cadastrado ({cnpj}).");
    }

    private ConfiguracaoEmitenteRequest EntidadeParaConfig(Emitente e) => new()
    {
        Cnpj = e.Cnpj,
        RazaoSocial = e.RazaoSocial,
        NomeFantasia = e.NomeFantasia,
        Ie = e.Ie,
        Crt = e.Crt,
        Uf = e.Uf,
        Ambiente = e.Ambiente,
        CertificadoPath = e.CertificadoPath,
        CertificadoSenha = _senhaProtector.Desproteger(e.CertificadoSenhaProtegida),
        Endereco = string.IsNullOrWhiteSpace(e.Logradouro) ? null : new EnderecoRequest
        {
            Logradouro = e.Logradouro,
            Numero = e.Numero,
            Complemento = e.Complemento,
            Bairro = e.Bairro,
            Municipio = e.Municipio,
            CodigoMunicipio = e.CodigoMunicipio,
            Uf = e.Uf,
            Cep = e.Cep,
            Telefone = e.Telefone
        }
    };

    private static void MesclarOverride(ConfiguracaoEmitenteRequest baseConfig, ConfiguracaoEmitenteRequest over, string cnpjEsperado)
    {
        if (!string.IsNullOrWhiteSpace(over.Cnpj) && SomenteDigitos(over.Cnpj) != cnpjEsperado)
            throw new ArgumentException("CNPJ em configuracaoEmitente diverge de emitenteCnpj.");

        if (!string.IsNullOrWhiteSpace(over.Ambiente)) baseConfig.Ambiente = over.Ambiente;
        if (!string.IsNullOrWhiteSpace(over.RazaoSocial)) baseConfig.RazaoSocial = over.RazaoSocial;
        if (over.NomeFantasia is not null) baseConfig.NomeFantasia = over.NomeFantasia;
        if (over.Ie is not null) baseConfig.Ie = over.Ie;
        if (over.Crt is >= 1 and <= 3) baseConfig.Crt = over.Crt;
        if (!string.IsNullOrWhiteSpace(over.Uf)) baseConfig.Uf = over.Uf;
        if (over.Endereco is not null) baseConfig.Endereco = over.Endereco;
        if (!string.IsNullOrWhiteSpace(over.CertificadoPath)) baseConfig.CertificadoPath = over.CertificadoPath;
        if (!string.IsNullOrWhiteSpace(over.CertificadoSenha)) baseConfig.CertificadoSenha = over.CertificadoSenha;
    }

    private static Emitente MapearParaEntidade(Emitente e, EmitenteCadastroRequest r, string cnpj, DateTime agora)
    {
        e.Cnpj = cnpj;
        e.RazaoSocial = r.RazaoSocial.Trim();
        e.NomeFantasia = r.NomeFantasia;
        e.Ie = r.Ie;
        e.Crt = r.Crt;
        e.Uf = r.Uf.Trim().ToUpperInvariant();
        e.Ambiente = r.Ambiente;
        e.CertificadoPath = r.CertificadoPath.Trim();
        e.CriadoEm = agora;
        e.AtualizadoEm = agora;
        e.Ativo = true;
        if (r.Endereco is not null) AplicarEndereco(e, r.Endereco);
        return e;
    }

    private static void AplicarEndereco(Emitente e, EnderecoRequest end)
    {
        e.Logradouro = end.Logradouro;
        e.Numero = end.Numero;
        e.Complemento = end.Complemento;
        e.Bairro = end.Bairro;
        e.Municipio = end.Municipio;
        e.CodigoMunicipio = end.CodigoMunicipio;
        e.Cep = end.Cep;
        e.Telefone = end.Telefone;
    }

    private static EmitenteResponse MapearParaResponse(Emitente e) => new()
    {
        Id = e.Id,
        Cnpj = e.Cnpj,
        RazaoSocial = e.RazaoSocial,
        NomeFantasia = e.NomeFantasia,
        Ie = e.Ie,
        Crt = e.Crt,
        Uf = e.Uf,
        Ambiente = e.Ambiente,
        CertificadoPath = e.CertificadoPath,
        Ativo = e.Ativo,
        CriadoEm = e.CriadoEm,
        AtualizadoEm = e.AtualizadoEm,
        Endereco = string.IsNullOrWhiteSpace(e.Logradouro) ? null : new EnderecoEmitenteResponse
        {
            Logradouro = e.Logradouro,
            Numero = e.Numero,
            Complemento = e.Complemento,
            Bairro = e.Bairro,
            Municipio = e.Municipio,
            CodigoMunicipio = e.CodigoMunicipio,
            Cep = e.Cep,
            Telefone = e.Telefone
        }
    };

    private static string SomenteDigitos(string valor) => Regex.Replace(valor ?? "", @"\D", "");
}
