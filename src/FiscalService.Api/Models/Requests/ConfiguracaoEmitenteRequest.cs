using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

/// <summary>Dados do emitente e do certificado digital — reutilizado em todos os endpoints fiscais.</summary>
public class ConfiguracaoEmitenteRequest
{
    [Required]
    public string Cnpj { get; set; } = string.Empty;

    [Required]
    public string RazaoSocial { get; set; } = string.Empty;

    public string? NomeFantasia { get; set; }

    public string? Ie { get; set; }

    /// <summary>Código de Regime Tributário: 1=Simples Nacional, 2=Simples Nacional Excesso, 3=Regime Normal.</summary>
    public int Crt { get; set; } = 1;

    [Required]
    public string Uf { get; set; } = string.Empty;

    public EnderecoRequest? Endereco { get; set; }

    /// <summary>"Homologacao" ou "Producao".</summary>
    public string Ambiente { get; set; } = "Homologacao";

    /// <summary>Path do .pfx (absoluto ou relativo ao diretório configurado).</summary>
    [Required]
    public string CertificadoPath { get; set; } = string.Empty;

    [Required]
    public string CertificadoSenha { get; set; } = string.Empty;
}
