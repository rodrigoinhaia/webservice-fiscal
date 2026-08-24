using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class EmitenteCadastroRequest
{
    [Required]
    public string Cnpj { get; set; } = string.Empty;

    [Required]
    public string RazaoSocial { get; set; } = string.Empty;

    public string? NomeFantasia { get; set; }
    public string? Ie { get; set; }
    public int Crt { get; set; } = 1;

    [Required]
    public string Uf { get; set; } = string.Empty;

    public string Ambiente { get; set; } = "Homologacao";

    [Required]
    public string CertificadoPath { get; set; } = string.Empty;

    [Required]
    public string CertificadoSenha { get; set; } = string.Empty;

    public EnderecoRequest? Endereco { get; set; }

    public string? InscricaoMunicipal { get; set; }
    public string? Email { get; set; }

    /// <summary>ID Token CSC NFC-e (homologação).</summary>
    public string? IdCscHomologacao { get; set; }

    /// <summary>CSC NFC-e em texto (homologação) — armazenado protegido.</summary>
    public string? CscHomologacao { get; set; }

    /// <summary>ID Token CSC NFC-e (produção).</summary>
    public string? IdCscProducao { get; set; }

    /// <summary>CSC NFC-e em texto (produção) — armazenado protegido.</summary>
    public string? CscProducao { get; set; }

    /// <summary>Se true, valida se o CNPJ do certificado coincide com o CNPJ cadastrado.</summary>
    public bool ValidarCnpjCertificado { get; set; } = true;

    /// <summary>Token da API De Olho no Imposto (por CNPJ). Armazenado protegido.</summary>
    public string? IbptToken { get; set; }
}
