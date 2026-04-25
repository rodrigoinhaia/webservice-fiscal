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
    public decimal? ValorIpi { get; set; }

    public decimal? ValorDesconto { get; set; }
    public decimal? ValorFrete { get; set; }
    public decimal? ValorSeguro { get; set; }
    public decimal? ValorOutrasDespesas { get; set; }

    public string? InformacaoAdicional { get; set; }
}
