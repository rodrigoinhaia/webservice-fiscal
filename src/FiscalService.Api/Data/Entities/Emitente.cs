using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FiscalService.Api.Data.Entities;

[Table("emitentes")]
public class Emitente
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [MaxLength(14)]
    [Column("cnpj")]
    public string Cnpj { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    [Column("razao_social")]
    public string RazaoSocial { get; set; } = string.Empty;

    [MaxLength(120)]
    [Column("nome_fantasia")]
    public string? NomeFantasia { get; set; }

    [MaxLength(20)]
    [Column("ie")]
    public string? Ie { get; set; }

    [Column("crt")]
    public int Crt { get; set; } = 1;

    [Required]
    [MaxLength(2)]
    [Column("uf")]
    public string Uf { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("ambiente")]
    public string Ambiente { get; set; } = "Homologacao";

    [Required]
    [MaxLength(260)]
    [Column("certificado_path")]
    public string CertificadoPath { get; set; } = string.Empty;

    [Required]
    [Column("certificado_senha_protegida")]
    public string CertificadoSenhaProtegida { get; set; } = string.Empty;

    [MaxLength(120)]
    [Column("logradouro")]
    public string? Logradouro { get; set; }

    [MaxLength(20)]
    [Column("numero")]
    public string? Numero { get; set; }

    [MaxLength(60)]
    [Column("complemento")]
    public string? Complemento { get; set; }

    [MaxLength(60)]
    [Column("bairro")]
    public string? Bairro { get; set; }

    [MaxLength(80)]
    [Column("municipio")]
    public string? Municipio { get; set; }

    [MaxLength(10)]
    [Column("codigo_municipio")]
    public string? CodigoMunicipio { get; set; }

    [MaxLength(8)]
    [Column("cep")]
    public string? Cep { get; set; }

    [MaxLength(20)]
    [Column("telefone")]
    public string? Telefone { get; set; }

    [MaxLength(20)]
    [Column("inscricao_municipal")]
    public string? InscricaoMunicipal { get; set; }

    [MaxLength(120)]
    [Column("email")]
    public string? Email { get; set; }

    /// <summary>ID Token / IdCSC da NFC-e em homologação.</summary>
    [MaxLength(10)]
    [Column("id_csc_homologacao")]
    public string? IdCscHomologacao { get; set; }

    [Column("csc_homologacao_protegido")]
    public string? CscHomologacaoProtegido { get; set; }

    /// <summary>ID Token / IdCSC da NFC-e em produção.</summary>
    [MaxLength(10)]
    [Column("id_csc_producao")]
    public string? IdCscProducao { get; set; }

    [Column("csc_producao_protegido")]
    public string? CscProducaoProtegido { get; set; }

    /// <summary>Token API IBPT (De Olho no Imposto), protegido via Data Protection.</summary>
    [Column("ibpt_token_protegido")]
    public string? IbptTokenProtegido { get; set; }

    [Column("ativo")]
    public bool Ativo { get; set; } = true;

    [Column("criado_em")]
    public DateTime CriadoEm { get; set; }

    [Column("atualizado_em")]
    public DateTime AtualizadoEm { get; set; }
}
