using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class CTeEmitirRequest
{
    [Required]
    public ConfiguracaoEmitenteRequest ConfiguracaoEmitente { get; set; } = null!;

    public int NumeroNota { get; set; }
    public string Serie { get; set; } = "1";
    public string NaturezaOperacao { get; set; } = "Prestação de Serviço de Transporte";

    /// <summary>CFOP do CT-e.</summary>
    public string Cfop { get; set; } = "6351";

    /// <summary>Modal de transporte: 01=Rodoviário, 02=Aéreo, 03=Aquaviário, 04=Ferroviário, 05=Dutoviário, 06=Multimodal.</summary>
    public string Modal { get; set; } = "01";

    [Required]
    public RemetenteCTeRequest Remetente { get; set; } = null!;

    [Required]
    public DestinatarioRequest Destinatario { get; set; } = null!;

    [Required]
    public TomadorCTeRequest Tomador { get; set; } = null!;

    public decimal ValorTotalServico { get; set; }
    public decimal ValorTotalCarga { get; set; }

    public string? InformacoesAdicionais { get; set; }

    public List<DocumentoCTeRequest> Documentos { get; set; } = new();
}

public class RemetenteCTeRequest
{
    public string? Cnpj { get; set; }
    public string? Cpf { get; set; }
    public string RazaoSocial { get; set; } = string.Empty;
    public EnderecoRequest? Endereco { get; set; }
}

public class TomadorCTeRequest
{
    /// <summary>Indicador do tomador: 0=Remetente, 1=Expedidor, 2=Recebedor, 3=Destinatário, 4=Outros.</summary>
    public int IndicadorTomador { get; set; } = 0;

    public string? Cnpj { get; set; }
    public string? Cpf { get; set; }
    public string? RazaoSocial { get; set; }
    public EnderecoRequest? Endereco { get; set; }
}

public class DocumentoCTeRequest
{
    public string? ChaveNFe { get; set; }
    public string? NumeroDocumento { get; set; }
    public string? Serie { get; set; }
    public decimal? ValorDocumento { get; set; }
}
