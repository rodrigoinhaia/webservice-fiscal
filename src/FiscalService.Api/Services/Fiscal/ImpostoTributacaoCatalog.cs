using FiscalService.Api.Models.Requests;

namespace FiscalService.Api.Services.Fiscal;

/// <summary>
/// CST/CSOSN suportados pelo mapeamento para classes DFe.NET (<c>NFe.Classes.Informacoes.Detalhe.Tributacao</c>).
/// </summary>
public static class ImpostoTributacaoCatalog
{
  public static readonly IReadOnlySet<string> CstRegimeNormal = new HashSet<string>(StringComparer.Ordinal)
  {
    "00", "10", "20", "30", "40", "41", "50", "51", "60", "70", "90"
  };

  public static readonly IReadOnlySet<string> CsosnSimplesNacional = new HashSet<string>(StringComparer.Ordinal)
  {
    "101", "102", "103", "201", "202", "203", "500", "900"
  };

  public static readonly IReadOnlySet<string> CstIpiTributado = new HashSet<string>(StringComparer.Ordinal)
  {
    "00", "49", "50", "99"
  };

  public static readonly IReadOnlySet<string> CstIpiNaoTributado = new HashSet<string>(StringComparer.Ordinal)
  {
    "01", "02", "03", "04", "05", "51", "52", "53", "54", "55"
  };

  public static readonly IReadOnlySet<string> CstPisCofinsAliquota = new HashSet<string>(StringComparer.Ordinal)
  {
    "01", "02"
  };

  public static readonly IReadOnlySet<string> CstPisCofinsNaoTributado = new HashSet<string>(StringComparer.Ordinal)
  {
    "04", "05", "06", "07", "08", "09"
  };

  public static readonly IReadOnlySet<string> CstPisCofinsOutros = new HashSet<string>(StringComparer.Ordinal)
  {
    "49", "99"
  };

  public static string? NormalizarCst(string? s)
  {
    if (string.IsNullOrWhiteSpace(s)) return null;
    var d = s.Trim();
    if (d.Length == 1 && char.IsDigit(d[0])) return "0" + d;
    return d.Length >= 2 ? d[..2] : d;
  }

  public static string? NormalizarCsosn(string? s)
  {
    if (string.IsNullOrWhiteSpace(s)) return null;
    var d = s.Trim();
    if (d.Length == 1 && char.IsDigit(d[0])) return d.PadLeft(3, '0');
    if (d.Length == 2 && d.All(char.IsDigit)) return d.PadLeft(3, '0');
    return d.Length >= 3 ? d[..3] : d;
  }

  public static string? NormalizarCstIpi(string? s) => NormalizarCst(s);

  public static string CstEfetivoRegimeNormal(ItemNFeRequest item) =>
    NormalizarCst(item.CstIcms) ?? "00";

  public static string CsosnEfetivoSimples(ItemNFeRequest item) =>
    NormalizarCsosn(item.CsosnIcms) ?? "102";

  public static bool ValidarItem(ItemNFeRequest item, int crt, out string? mensagem)
  {
    mensagem = null;
    if (crt is 1 or 2)
    {
      if (!string.IsNullOrWhiteSpace(item.CstIcms))
      {
        mensagem = "Para CRT 1 ou 2 (Simples Nacional) informe csosnIcms, não cstIcms.";
        return false;
      }

      var cs = NormalizarCsosn(item.CsosnIcms);
      if (cs is not null && !CsosnSimplesNacional.Contains(cs))
      {
        mensagem = $"CSOSN '{cs}' não suportado. Suportados: {string.Join(", ", CsosnSimplesNacional.OrderBy(x => x))}.";
        return false;
      }

      return ValidarIpiOpcional(item, out mensagem);
    }

    if (crt == 3)
    {
      if (!string.IsNullOrWhiteSpace(item.CsosnIcms))
      {
        mensagem = "Para CRT 3 (regime normal — Lucro Presumido/Real) informe cstIcms, não csosnIcms.";
        return false;
      }

      var cst = NormalizarCst(item.CstIcms);
      if (cst is not null && !CstRegimeNormal.Contains(cst))
      {
        mensagem = $"CST ICMS '{cst}' não suportado. Suportados: {string.Join(", ", CstRegimeNormal.OrderBy(x => x))}.";
        return false;
      }

      if (!ValidarIpiOpcional(item, out mensagem)) return false;
      if (!ValidarPisCofinsOpcional(item, out mensagem)) return false;
      return true;
    }

    mensagem = "CRT deve ser 1, 2 ou 3.";
    return false;
  }

  private static bool ValidarPisCofinsOpcional(ItemNFeRequest item, out string? mensagem)
  {
    mensagem = null;
    var cstPis = NormalizarCst(item.CstPis);
    if (cstPis is not null && !CstPisCofinsSuportado(cstPis))
    {
      mensagem = $"CST PIS '{cstPis}' não suportado.";
      return false;
    }

    var cstCofins = NormalizarCst(item.CstCofins);
    if (cstCofins is not null && !CstPisCofinsSuportado(cstCofins))
    {
      mensagem = $"CST COFINS '{cstCofins}' não suportado.";
      return false;
    }

    return true;
  }

  private static bool CstPisCofinsSuportado(string cst) =>
    CstPisCofinsAliquota.Contains(cst)
    || CstPisCofinsNaoTributado.Contains(cst)
    || CstPisCofinsOutros.Contains(cst);

  public static void ValidarItensOuLancar(int crt, List<ItemNFeRequest> itens)
  {
    for (var i = 0; i < itens.Count; i++)
    {
      if (!ValidarItem(itens[i], crt, out var msg))
        throw new TributacaoNaoSuportadaException($"Item {i + 1}: {msg}");
    }
  }

  private static bool ValidarIpiOpcional(ItemNFeRequest item, out string? mensagem)
  {
    mensagem = null;
    var cst = NormalizarCstIpi(item.CstIpi);
    if (cst is null) return true;

    if (CstIpiTributado.Contains(cst) || CstIpiNaoTributado.Contains(cst))
      return true;

    mensagem = $"CST IPI '{cst}' não suportado.";
    return false;
  }
}
