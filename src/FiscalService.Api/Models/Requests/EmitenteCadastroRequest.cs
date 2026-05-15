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

    /// <summary>Se true, valida se o CNPJ do certificado coincide com o CNPJ cadastrado.</summary>
    public bool ValidarCnpjCertificado { get; set; } = true;
}
