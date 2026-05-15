using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

/// <summary>Consulta ao serviço nacional de Distribuição DF-e (documentos de interesse do destinatário).</summary>
public class NFeDistribuicaoDfeRequest : IEmitenteConfigSource
{
    public string? EmitenteCnpj { get; set; }
    public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; set; }

    /// <summary>CNPJ ou CPF do interessado. Se omitido, usa o CNPJ do emitente cadastrado.</summary>
    public string? DocumentoInteressado { get; set; }

    /// <summary>Último NSU recebido (padrão 0 na primeira consulta).</summary>
    public string? UltNsu { get; set; }

    /// <summary>Consulta por NSU específico (mutuamente exclusivo com chave e distNSU).</summary>
    public string? Nsu { get; set; }

    /// <summary>Consulta por chave de acesso (44 dígitos).</summary>
    [StringLength(44, MinimumLength = 44)]
    public string? ChaveAcesso { get; set; }
}
