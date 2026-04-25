using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FiscalService.Api.Data.Entities;

[Table("emissao_logs")]
public class EmissaoLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>CNPJ do emitente (14 dígitos, sem máscara).</summary>
    [Required]
    [MaxLength(14)]
    [Column("cnpj")]
    public string Cnpj { get; set; } = string.Empty;

    /// <summary>Modelo do documento: "55" (NF-e), "65" (NFC-e), "57" (CT-e), "58" (MDF-e).</summary>
    [Required]
    [MaxLength(2)]
    [Column("modelo")]
    public string Modelo { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    [Column("serie")]
    public string Serie { get; set; } = string.Empty;

    [Column("numero")]
    public int Numero { get; set; }

    /// <summary>Chave de acesso de 44 dígitos.</summary>
    [MaxLength(44)]
    [Column("chave_acesso")]
    public string? ChaveAcesso { get; set; }

    [MaxLength(20)]
    [Column("protocolo")]
    public string? Protocolo { get; set; }

    /// <summary>"Autorizado", "Cancelado", "Rejeitado", "Inutilizado".</summary>
    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Código de status retornado pela SEFAZ (cStat).</summary>
    [MaxLength(3)]
    [Column("codigo_status")]
    public string? CodigoStatus { get; set; }

    [MaxLength(500)]
    [Column("mensagem_status")]
    public string? MensagemStatus { get; set; }

    /// <summary>"Homologacao" ou "Producao".</summary>
    [Required]
    [MaxLength(20)]
    [Column("ambiente")]
    public string Ambiente { get; set; } = string.Empty;

    [Column("data_emissao")]
    public DateTime DataEmissao { get; set; }

    [Column("data_processamento")]
    public DateTime DataProcessamento { get; set; }

    /// <summary>Caminho do XML autorizado salvo em disco.</summary>
    [MaxLength(500)]
    [Column("xml_path")]
    public string? XmlPath { get; set; }
}
