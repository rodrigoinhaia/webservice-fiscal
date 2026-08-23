using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NumeracaoConfirmarRequest
{
    [Required]
    public string Cnpj { get; set; } = string.Empty;

    /// <summary>Modelo do documento: "55", "65", "57", "58", "NS".</summary>
    [Required]
    public string Modelo { get; set; } = string.Empty;

    [Required]
    public string Serie { get; set; } = string.Empty;

    public int Numero { get; set; }

    /// <summary>"Homologacao" ou "Producao" (padrão Homologacao).</summary>
    public string? Ambiente { get; set; }
}

public class StatusServicoRequest : IEmitenteConfigSource
{
    /// <summary>CNPJ do emitente cadastrado em <c>/api/emitentes</c> (alternativa a <see cref="ConfiguracaoEmitente"/>).</summary>
    public string? EmitenteCnpj { get; set; }

    public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; set; }

    /// <summary>Modelo: "NFe", "NFCe", "CTe", "MDFe".</summary>
    public string Modelo { get; set; } = "NFe";
}
