using FiscalService.Api.Services.DanfeHtml;
using Xunit;

namespace FiscalService.Api.Tests;

public sealed class DanfeHtmlRendererTests
{
    [Fact]
    public void GerarNFe_inclui_banner_homologacao_quando_tpAmb_2()
    {
        var chave = new string('3', 44);
        var xml = $"""
<?xml version="1.0" encoding="UTF-8"?>
<nfeProc versao="4.00" xmlns="http://www.portalfiscal.inf.br/nfe">
  <NFe><infNFe Id="NFe{chave}" versao="4.00">
    <ide><mod>55</mod><serie>1</serie><nNF>99</nNF><dhEmi>2024-06-01T12:00:00-03:00</dhEmi><tpAmb>2</tpAmb><natOp>Venda</natOp></ide>
    <emit><CNPJ>12345678000199</CNPJ><xNome>ACME</xNome></emit>
    <dest><CPF>12345678909</CPF><xNome>Cliente</xNome></dest>
    <det nItem="1"><prod><cProd>P1</cProd><xProd>Item um</xProd><qCom>2</qCom><vUnCom>5.00</vUnCom><vProd>10.00</vProd></prod></det>
    <total><ICMSTot><vProd>10.00</vProd><vNF>10.00</vNF></ICMSTot></total>
  </infNFe></NFe>
  <protNFe versao="4.00"><infProt><chNFe>{chave}</chNFe><nProt>135000</nProt><dhRecbto>2024-06-01T12:01:00-03:00</dhRecbto><cStat>100</cStat><xMotivo>Autorizado</xMotivo></infProt></protNFe>
</nfeProc>
""";

        var html = DanfeHtmlRenderer.GerarNFe(xml);

        Assert.Contains("SEM VALOR FISCAL", html, StringComparison.Ordinal);
        Assert.Contains("Item um", html, StringComparison.Ordinal);
        Assert.Contains("3333 3333", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GerarNFCe_inclui_secao_pagamentos()
    {
        var chave = new string('5', 44);
        var xml = $"""
<?xml version="1.0" encoding="UTF-8"?>
<nfeProc versao="4.00" xmlns="http://www.portalfiscal.inf.br/nfe">
  <NFe>
    <infNFe Id="NFe{chave}" versao="4.00">
      <ide><mod>65</mod><serie>2</serie><nNF>10</nNF><dhEmi>2024-06-01T14:00:00-03:00</dhEmi><tpAmb>1</tpAmb></ide>
      <emit><CNPJ>98765432000100</CNPJ><xNome>Loja</xNome></emit>
      <dest><CPF>11122233344</CPF><xNome>Consumidor</xNome></dest>
      <det nItem="1"><prod><xProd>Produto NFC</xProd><qCom>1</qCom><vUnCom>15.00</vUnCom><vProd>15.00</vProd></prod></det>
      <total><ICMSTot><vNF>15.00</vNF></ICMSTot></total>
      <pag><detPag><tPag>01</tPag><vPag>15.00</vPag></detPag></pag>
    </infNFe>
    <infNFeSupl><qrCode>https://example.com/consulta?chave={chave}</qrCode><urlChave>https://example.com/chave</urlChave></infNFeSupl>
  </NFe>
  <protNFe versao="4.00"><infProt><chNFe>{chave}</chNFe><nProt>1</nProt><cStat>100</cStat><xMotivo>OK</xMotivo></infProt></protNFe>
</nfeProc>
""";

        var html = DanfeHtmlRenderer.GerarNFCe(xml);

        Assert.Contains("NFC-e", html, StringComparison.Ordinal);
        Assert.Contains("Produto NFC", html, StringComparison.Ordinal);
        Assert.Contains("Pagamentos", html, StringComparison.Ordinal);
        Assert.Contains("https://example.com/consulta", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GerarNFe_exibe_cobranca_infAdic_e_totais()
    {
        var chave = new string('3', 44);
        var xml = $"""
<?xml version="1.0" encoding="UTF-8"?>
<nfeProc versao="4.00" xmlns="http://www.portalfiscal.inf.br/nfe">
  <NFe><infNFe Id="NFe{chave}" versao="4.00">
    <ide><mod>55</mod><serie>1</serie><nNF>7</nNF><dhEmi>2024-06-01T12:00:00-03:00</dhEmi><tpAmb>1</tpAmb><natOp>Venda</natOp><finNFe>1</finNFe><tpNF>1</tpNF><idDest>1</idDest><cUF>35</cUF><cNF>12345678</cNF><indFinal>0</indFinal><indPres>1</indPres></ide>
    <emit><CNPJ>12345678000199</CNPJ><xNome>ACME</xNome><IE>123</IE><CRT>3</CRT>
      <enderEmit><xLgr>Rua A</xLgr><nro>10</nro><xBairro>Centro</xBairro><cMun>3550308</cMun><xMun>São Paulo</xMun><UF>SP</UF><CEP>01000000</CEP></enderEmit>
    </emit>
    <dest><CNPJ>07070707070707</CNPJ><xNome>Dest</xNome><indIEDest>9</indIEDest>
      <enderDest><xLgr>Av B</xLgr><nro>20</nro><xBairro>Bairro</xBairro><cMun>3550308</cMun><xMun>São Paulo</xMun><UF>SP</UF><CEP>02000000</CEP></enderDest>
    </dest>
    <det nItem="1"><prod><cProd>P1</cProd><xProd>Item</xProd><NCM>12345678</NCM><CFOP>5102</CFOP><uCom>UN</uCom><qCom>1</qCom><vUnCom>10.00</vUnCom><vProd>10.00</vProd></prod>
      <imposto><ICMS><ICMS00><orig>0</orig><CST>00</CST><modBC>3</modBC><vBC>10.00</vBC><pICMS>18.00</pICMS><vICMS>1.80</vICMS></ICMS00></ICMS></imposto>
    </det>
    <total><ICMSTot><vBC>10.00</vBC><vICMS>1.80</vICMS><vProd>10.00</vProd><vNF>10.00</vNF></ICMSTot></total>
    <transp><modFrete>9</modFrete><vol><qVol>2</qVol><esp>CAIXA</esp><marca>X</marca><pesoL>1.5</pesoL><pesoB>1.6</pesoB></vol></transp>
    <cobr><fat><nFat>001</nFat><vOrig>10.00</vOrig><vDesc>0.00</vDesc><vLiq>10.00</vLiq></fat>
      <dup><nDup>001-1</nDup><dVenc>2024-07-01</dVenc><vDup>10.00</vDup></dup>
    </cobr>
    <pag><detPag><tPag>01</tPag><vPag>10.00</vPag></detPag></pag>
    <infAdic><infCpl>Texto complementar do emitente.</infCpl></infAdic>
  </infNFe></NFe>
  <protNFe versao="4.00"><infProt><chNFe>{chave}</chNFe><nProt>135</nProt><cStat>100</cStat><xMotivo>Autorizado</xMotivo></infProt></protNFe>
</nfeProc>
""";

        var html = DanfeHtmlRenderer.GerarNFe(xml);
        Assert.Contains("Duplicatas", html, StringComparison.Ordinal);
        Assert.Contains("001-1", html, StringComparison.Ordinal);
        Assert.Contains("Texto complementar", html, StringComparison.Ordinal);
        Assert.Contains("vICMS", html, StringComparison.Ordinal);
        Assert.Contains("Modalidade frete", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GerarNFe_lanca_quando_xml_invalido()
    {
        Assert.ThrowsAny<Exception>(() => DanfeHtmlRenderer.GerarNFe("<nao-xml"));
    }
}
