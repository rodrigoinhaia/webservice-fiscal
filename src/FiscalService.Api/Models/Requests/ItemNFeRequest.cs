namespace FiscalService.Api.Models.Requests;

public class ItemNFeRequest
{
    public int NumeroItem { get; set; }
    public string CodigoProduto { get; set; } = string.Empty;
    public string? CodigoEan { get; set; } = "SEM GTIN";
    public string DescricaoProduto { get; set; } = string.Empty;
    public string? Ncm { get; set; }
    public string? Cest { get; set; }
    public string? Cfop { get; set; }
    public string UnidadeComercial { get; set; } = "UN";
    public decimal QuantidadeComercial { get; set; }
    public decimal ValorUnitarioComercial { get; set; }
    public decimal ValorTotalBruto { get; set; }

    public string? UnidadeTributavel { get; set; }
    public decimal? QuantidadeTributavel { get; set; }
    public decimal? ValorUnitarioTributavel { get; set; }

    public bool IndicadorTotal { get; set; } = true;

    // ICMS
    public string? CstIcms { get; set; }
    public string? CsosnIcms { get; set; }
    public string? OrigemMercadoria { get; set; } = "0";
    public decimal? BaseCalculoIcms { get; set; }
    public decimal? AliquotaIcms { get; set; }
    public decimal? ValorIcms { get; set; }
    public decimal? ReducaoBaseIcms { get; set; }

    /// <summary>CST 51 — valor da operação antes do diferimento.</summary>
    public decimal? ValorIcmsOperacao { get; set; }
    public decimal? PercentualDiferimentoIcms { get; set; }
    public decimal? ValorIcmsDiferido { get; set; }

    /// <summary>ICMS ST e retidos (CST 60, CSOSN 201/202/203/500 etc.).</summary>
    public decimal? BaseCalculoIcmsSt { get; set; }
    public decimal? AliquotaIcmsSt { get; set; }
    public decimal? ValorIcmsSt { get; set; }
    public decimal? PercentualMvaSt { get; set; }
    public decimal? PercentualReducaoBcSt { get; set; }
    public decimal? BaseCalculoIcmsStRetido { get; set; }
    public decimal? AliquotaSuportadaConsumidorFinal { get; set; }
    public decimal? ValorIcmsStRetido { get; set; }
    public decimal? ValorIcmsSubstituto { get; set; }
    public decimal? BaseCalculoFcpStRetido { get; set; }
    public decimal? AliquotaFcpStRetido { get; set; }
    public decimal? ValorFcpStRetido { get; set; }
    public decimal? PercentualReducaoBcEfetivo { get; set; }
    public decimal? BaseCalculoEfetivo { get; set; }
    public decimal? AliquotaIcmsEfetivo { get; set; }
    public decimal? ValorIcmsEfetivo { get; set; }
    public decimal? ValorIcmsDesonerado { get; set; }
    public int? MotivoDesoneracaoIcms { get; set; }
    public decimal? AliquotaCreditoSimples { get; set; }
    public decimal? ValorCreditoSimples { get; set; }

    // PIS
    public string? CstPis { get; set; }
    public decimal? BaseCalculoPis { get; set; }
    public decimal? AliquotaPis { get; set; }
    public decimal? ValorPis { get; set; }

    // COFINS
    public string? CstCofins { get; set; }
    public decimal? BaseCalculoCofins { get; set; }
    public decimal? AliquotaCofins { get; set; }
    public decimal? ValorCofins { get; set; }

    // IPI
    public string? CstIpi { get; set; }
    public decimal? BaseCalculoIpi { get; set; }
    public decimal? AliquotaIpi { get; set; }
    public decimal? ValorIpi { get; set; }

    public decimal? ValorDesconto { get; set; }
    public decimal? ValorFrete { get; set; }
    public decimal? ValorSeguro { get; set; }
    public decimal? ValorOutrasDespesas { get; set; }

    public string? InformacaoAdicional { get; set; }

    /// <summary>DIFAL — partilha ICMS para UF destino (informe quando o ERP calcular).</summary>
    public decimal? BaseCalculoUfDest { get; set; }
    public decimal? PercentualFcpUfDest { get; set; }
    public decimal? PercentualIcmsUfDest { get; set; }
    public decimal? PercentualIcmsInter { get; set; }
    public decimal? PercentualIcmsInterPartilha { get; set; }
    public decimal? ValorFcpUfDest { get; set; }
    public decimal? ValorIcmsUfDest { get; set; }
    public decimal? ValorIcmsUfRemet { get; set; }
}
