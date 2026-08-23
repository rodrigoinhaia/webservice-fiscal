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
    public string? InscricaoMunicipal { get; set; }
    public string? Email { get; set; }
    public bool? Ativo { get; set; }
    public bool ValidarCnpjCertificado { get; set; } = true;

    /// <summary>ID Token CSC NFC-e (homologação). Null = não altera.</summary>
    public string? IdCscHomologacao { get; set; }

    /// <summary>CSC homologação. Null = não altera; string vazia = remove.</summary>
    public string? CscHomologacao { get; set; }

    /// <summary>ID Token CSC NFC-e (produção). Null = não altera.</summary>
    public string? IdCscProducao { get; set; }

    /// <summary>CSC produção. Null = não altera; string vazia = remove.</summary>
    public string? CscProducao { get; set; }
}
