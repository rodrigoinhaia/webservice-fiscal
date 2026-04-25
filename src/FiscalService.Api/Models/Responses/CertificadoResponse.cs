namespace FiscalService.Api.Models.Responses;

public class CertificadoValidarResponse
{
    public bool Valido { get; set; }
    public string? Cnpj { get; set; }
    public string? RazaoSocial { get; set; }
    public DateTime? Validade { get; set; }
    public string? Emissor { get; set; }
    public string? Thumbprint { get; set; }
    public ErroResponse? Erro { get; set; }
}

public class CertificadoUploadResponse
{
    public bool Sucesso { get; set; }
    public string? PathRelativo { get; set; }
    public string? PathAbsoluto { get; set; }
    public ErroResponse? Erro { get; set; }
}
