using System.ComponentModel.DataAnnotations;

namespace FiscalService.Api.Models.Requests;

public class NFSeEmitirRequest : IEmitenteConfigSource
{
    public string? EmitenteCnpj { get; set; }
    public ConfiguracaoEmitenteRequest? ConfiguracaoEmitente { get; set; }

    public string Serie { get; set; } = "1";

    /// <summary>Se omitido, reserva próximo número via NumeracaoService (modelo NS).</summary>
    public int? NumeroDps { get; set; }

    [Required]
    public DateTime Competencia { get; set; }

    public NFSePrestadorRequest? Prestador { get; set; }

    [Required]
    public NFSeTomadorRequest Tomador { get; set; } = null!;

    [Required]
    public NFSeServicoRequest Servico { get; set; } = null!;

    [Required]
    public NFSeValoresRequest Valores { get; set; } = null!;

    public NFSeReformaTributariaRequest? ReformaTributaria { get; set; }
}

public class NFSePrestadorRequest
{
    public string? Email { get; set; }
    public string? InscricaoMunicipal { get; set; }
}

public class NFSeTomadorRequest
{
    public string? Cnpj { get; set; }
    public string? Cpf { get; set; }

    [Required]
    public string Nome { get; set; } = string.Empty;

    public string? Email { get; set; }

    [Required]
    public EnderecoRequest Endereco { get; set; } = null!;
}

public class NFSeServicoRequest
{
    [Required]
    public string CodTributacaoNacional { get; set; } = string.Empty;

    public string? CodTributacaoMunicipio { get; set; }

    [Required]
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Código IBGE do município de prestação (fallback: emitente.endereco.codigoMunicipio).</summary>
    public string? CodigoMunicipioPrestacao { get; set; }

    /// <summary>Código NBS (9 dígitos). Obrigatório no layout 1.01 para vários cTribNac.</summary>
    public string? CodNbs { get; set; }
}

public class NFSeValoresRequest
{
    [Range(typeof(decimal), "0.01", "999999999999.99")]
    public decimal ValorServico { get; set; }

    /// <summary>OperacaoTributavel ou NaoTributavel (default OperacaoTributavel).</summary>
    public string Issqn { get; set; } = "OperacaoTributavel";

    /// <summary>NaoRetido ou RetidoTomador (default NaoRetido).</summary>
    public string TipoRetencaoIssqn { get; set; } = "NaoRetido";

    public decimal? AliquotaIss { get; set; }
    public decimal? ValorIss { get; set; }
}

public class NFSeReformaTributariaRequest
{
    /// <summary>Código indicador da operação (cIndOp, 6 dígitos). Default: 030101.</summary>
    public string? CodigoIndicadorOperacao { get; set; }

    /// <summary>Indicador de uso/consumo pessoal (indFinal). Default: false.</summary>
    public bool? IndicadorUsoFinal { get; set; }

    /// <summary>CST IBS/CBS (3 dígitos). Default: 000.</summary>
    public string? CodigoSituacaoTributaria { get; set; }

    /// <summary>Classificação tributária IBS/CBS (6 dígitos). Default: 000001.</summary>
    public string? CodigoClassificacaoTributaria { get; set; }

    public decimal? ValorIbs { get; set; }
    public decimal? ValorCbs { get; set; }
}
