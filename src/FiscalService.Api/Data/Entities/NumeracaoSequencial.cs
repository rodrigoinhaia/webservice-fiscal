using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FiscalService.Api.Data.Entities;

[Table("numeracoes_sequenciais")]
public class NumeracaoSequencial
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [MaxLength(14)]
    [Column("cnpj")]
    public string Cnpj { get; set; } = string.Empty;

    /// <summary>Modelo do documento: "55", "65", "57", "58", "NS" (NFS-e DPS).</summary>
    [Required]
    [MaxLength(2)]
    [Column("modelo")]
    public string Modelo { get; set; } = string.Empty;

    [Required]
    [MaxLength(5)]
    [Column("serie")]
    public string Serie { get; set; } = string.Empty;

    /// <summary>Último número efetivamente usado ou reservado.</summary>
    [Column("ultimo_numero")]
    public int UltimoNumero { get; set; }

    [Column("ultima_atualizacao")]
    public DateTime UltimaAtualizacao { get; set; }
}
