using FiscalService.Api.Services.Fiscal;
using NFe.Classes.Servicos.Tipos;
using Xunit;

namespace FiscalService.Api.Tests;

public class ManifestacaoDestinatarioMapperTests
{
    [Theory]
    [InlineData("Ciencia", NFeTipoEvento.TeMdCienciaDaOperacao)]
    [InlineData("210210", NFeTipoEvento.TeMdCienciaDaOperacao)]
    [InlineData("Confirmacao", NFeTipoEvento.TeMdConfirmacaoDaOperacao)]
    [InlineData("210240", NFeTipoEvento.TeMdOperacaoNaoRealizada)]
    public void Resolve_tipo_manifestacao(string entrada, NFeTipoEvento esperado)
    {
        Assert.Equal(esperado, ManifestacaoDestinatarioMapper.Resolver(entrada));
    }

    [Fact]
    public void Operacao_nao_realizada_exige_justificativa()
    {
        Assert.True(ManifestacaoDestinatarioMapper.ExigeJustificativa(NFeTipoEvento.TeMdOperacaoNaoRealizada));
        Assert.False(ManifestacaoDestinatarioMapper.ExigeJustificativa(NFeTipoEvento.TeMdCienciaDaOperacao));
    }
}
