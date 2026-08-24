using FiscalService.Api.Services.Danfe;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FiscalService.Api.Tests;

public sealed class NFeDanfeLocalRendererTests
{
    private static string SampleNfeProcXml()
    {
        var chave = new string('3', 44);
        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<nfeProc versao="4.00" xmlns="http://www.portalfiscal.inf.br/nfe">
  <NFe><infNFe Id="NFe{chave}" versao="4.00">
    <ide><mod>55</mod><serie>1</serie><nNF>7</nNF><dhEmi>2024-06-01T12:00:00-03:00</dhEmi><tpAmb>2</tpAmb><natOp>Venda</natOp><finNFe>1</finNFe><tpNF>1</tpNF><idDest>1</idDest><cUF>35</cUF><cNF>12345678</cNF><indFinal>0</indFinal><indPres>1</indPres><tpEmis>1</tpEmis></ide>
    <emit><CNPJ>12345678000199</CNPJ><xNome>ACME</xNome><IE>123</IE><CRT>3</CRT>
      <enderEmit><xLgr>Rua A</xLgr><nro>10</nro><xBairro>Centro</xBairro><cMun>3550308</cMun><xMun>Sao Paulo</xMun><UF>SP</UF><CEP>01000000</CEP></enderEmit>
    </emit>
    <dest><CPF>12345678909</CPF><xNome>Cliente</xNome><indIEDest>9</indIEDest>
      <enderDest><xLgr>Av B</xLgr><nro>20</nro><xBairro>Bairro</xBairro><cMun>3550308</cMun><xMun>Sao Paulo</xMun><UF>SP</UF><CEP>02000000</CEP></enderDest>
    </dest>
    <det nItem="1"><prod><cProd>P1</cProd><xProd>Item um</xProd><NCM>12345678</NCM><CFOP>5102</CFOP><uCom>UN</uCom><qCom>1</qCom><vUnCom>10.00</vUnCom><vProd>10.00</vProd></prod>
      <imposto><ICMS><ICMS00><orig>0</orig><CST>00</CST><modBC>3</modBC><vBC>10.00</vBC><pICMS>18.00</pICMS><vICMS>1.80</vICMS></ICMS00></ICMS></imposto>
    </det>
    <total><ICMSTot><vBC>10.00</vBC><vICMS>1.80</vICMS><vProd>10.00</vProd><vNF>10.00</vNF></ICMSTot></total>
    <transp><modFrete>9</modFrete></transp>
  </infNFe></NFe>
  <protNFe versao="4.00"><infProt><chNFe>{chave}</chNFe><nProt>135000</nProt><dhRecbto>2024-06-01T12:01:00-03:00</dhRecbto><cStat>100</cStat><xMotivo>Autorizado</xMotivo></infProt></protNFe>
</nfeProc>
""";
    }

    [Fact]
    public void TentarGerarDeXml_retorna_pdf_valido()
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddDebug());
        var renderer = new NFeDanfeLocalRenderer(loggerFactory.CreateLogger<NFeDanfeLocalRenderer>());

        var xml = SampleNfeProcXml();
        var pdf = renderer.TentarGerarDeXml(xml);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 100);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    [Fact]
    public void NFeProcComposer_rejeita_retEnviNFe()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NFeProcComposer.NormalizarParaDanfe("<retEnviNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\"/>"));
        Assert.Contains("retEnviNFe", ex.Message, StringComparison.Ordinal);
    }
}
