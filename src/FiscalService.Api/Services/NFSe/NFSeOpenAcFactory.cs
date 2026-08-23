using FiscalService.Api.Config;
using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFSe.Nacional;
using OpenAC.Net.NFSe.Nacional.Common.Types;
using System.Net;

namespace FiscalService.Api.Services.NFSe;

/// <summary>Monta e configura <see cref="OpenNFSeNacional"/> por requisição (Transient).</summary>
public sealed class NFSeOpenAcFactory
{
    private readonly FiscalConfig _fiscalConfig;
    private readonly CertificadoService _certificadoService;

    public NFSeOpenAcFactory(FiscalConfig fiscalConfig, CertificadoService certificadoService)
    {
        _fiscalConfig = fiscalConfig;
        _certificadoService = certificadoService;
    }

    public OpenNFSeNacional Criar(ConfiguracaoEmitenteRequest emitente, EmitenteNfseContexto? ctx = null)
    {
        var nfse = new OpenNFSeNacional();
        var nfseCfg = _fiscalConfig.NFSe;
        var versao = ResolverVersaoDps(nfseCfg.VersaoDps);

        nfse.Configuracoes.Geral.Versao = versao;
        nfse.Configuracoes.Geral.RetirarAcentos = true;
        nfse.Configuracoes.Geral.AssinarXml = false;
        nfse.Configuracoes.Geral.Salvar = _fiscalConfig.SalvarXmls;
        nfse.Configuracoes.Arquivos.VersaoSchema = versao;
        nfse.Configuracoes.Arquivos.Salvar = _fiscalConfig.SalvarXmls;

        nfse.Configuracoes.WebServices.Ambiente = ResolverAmbiente(emitente.Ambiente);
        nfse.Configuracoes.WebServices.ValidarSchemas = nfseCfg.ValidarSchemas;
        // OpenAC.Net.DFe.Core default inclui Tls/Tls11 — removidos no .NET 8 (NotSupportedException).
        nfse.Configuracoes.WebServices.Protocolos = ResolverProtocolosTls();

        var codMun = ctx?.CodigoMunicipioIbge
                     ?? emitente.Endereco?.CodigoMunicipio
                     ?? throw new ArgumentException("codigoMunicipio do emitente é obrigatório para NFS-e.");
        if (!int.TryParse(SomenteDigitos(codMun), out var codigoMunicipio) || codigoMunicipio <= 0)
            throw new ArgumentException("codigoMunicipio do emitente inválido para NFS-e.");

        nfse.Configuracoes.WebServices.CodigoMunicipio = codigoMunicipio;
        nfse.Configuracoes.WebServices.InscricaoMunicipal =
            ctx?.InscricaoMunicipal ?? string.Empty;

        var pathSchemas = ResolverPathSchemas(nfseCfg, versao);
        nfse.Configuracoes.Arquivos.PathSchemas = pathSchemas;
        nfse.Configuracoes.Arquivos.PathSalvar = ResolverPathSalvar();

        var certPath = _certificadoService.ResolvePath(emitente.CertificadoPath);
        nfse.Configuracoes.Certificados.Certificado = certPath;
        nfse.Configuracoes.Certificados.Senha = emitente.CertificadoSenha;

        return nfse;
    }

    public static DFeTipoAmbiente ResolverAmbiente(string ambiente) =>
        ambiente.Equals("Producao", StringComparison.OrdinalIgnoreCase)
            ? DFeTipoAmbiente.Producao
            : DFeTipoAmbiente.Homologacao;

    public static VersaoNFSe ResolverVersaoDps(string versao) =>
        versao.Equals("Ve100", StringComparison.OrdinalIgnoreCase)
            ? VersaoNFSe.Ve100
            : VersaoNFSe.Ve101;

    /// <summary>TLS aceitos pelo ADN/Sefin em .NET 8+ (sem SSL3/Tls1.0/Tls1.1).</summary>
    public static SecurityProtocolType ResolverProtocolosTls() =>
        SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

    private string ResolverPathSchemas(NfseConfig nfseCfg, VersaoNFSe versao)
    {
        var configured = nfseCfg.DiretorioSchemas;
        if (string.IsNullOrWhiteSpace(configured))
            configured = Path.Combine(_fiscalConfig.DiretorioSchemas, "nfse");

        if (!Path.IsPathRooted(configured))
            configured = Path.Combine(Directory.GetCurrentDirectory(), configured);

        var basePath = Path.GetFullPath(configured);
        var versaoFolder = versao switch
        {
            VersaoNFSe.Ve101 => "1.01",
            _ => "1.00"
        };

        var versioned = Path.Combine(basePath, versaoFolder);
        if (Directory.Exists(versioned))
            return versioned;

        var fallback = Path.Combine(AppContext.BaseDirectory, "Schemas", "NFSe", versaoFolder);
        if (Directory.Exists(fallback))
            return Path.GetFullPath(fallback);

        return versioned;
    }

    private string ResolverPathSalvar()
    {
        var baseDir = _fiscalConfig.DiretorioXmls;
        if (!Path.IsPathRooted(baseDir))
            baseDir = Path.Combine(Directory.GetCurrentDirectory(), baseDir);

        var nfseDir = Path.Combine(baseDir, "nfse");
        Directory.CreateDirectory(nfseDir);
        return Path.GetFullPath(nfseDir);
    }

    private static string SomenteDigitos(string valor) =>
        new string(valor.Where(char.IsDigit).ToArray());
}

/// <summary>Dados do emitente não presentes em ConfiguracaoEmitenteRequest (NF-e).</summary>
public sealed class EmitenteNfseContexto
{
    public string? InscricaoMunicipal { get; init; }
    public string? Email { get; init; }
    public string? CodigoMunicipioIbge { get; init; }
}
