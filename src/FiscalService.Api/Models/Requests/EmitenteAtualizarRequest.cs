namespace FiscalService.Api.Models.Requests;

public class EmitenteAtualizarRequest
{
    public string? RazaoSocial { get; set; }
    public string? NomeFantasia { get; set; }
    public string? Ie { get; set; }
    public int? Crt { get; set; }
    public string? Uf { get; set; }
    public string? Ambiente { get; set; }
    public string? CertificadoPath { get; set; }
    public string? CertificadoSenha { get; set; }
    public EnderecoRequest? Endereco { get; set; }
    public bool? Ativo { get; set; }
    public bool ValidarCnpjCertificado { get; set; } = true;
}
