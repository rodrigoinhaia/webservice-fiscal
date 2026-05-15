using System.Text.RegularExpressions;
using FiscalService.Api.Models.Requests;
using FluentValidation;

namespace FiscalService.Api.Validation;

public static class EmitenteConfigSourceValidator
{
    public static void ValidarEmitenteOuConfig<T>(ValidationContext<T> ctx, IEmitenteConfigSource source)
        where T : class
    {
        var temCnpj = !string.IsNullOrWhiteSpace(source.EmitenteCnpj);
        var config = source.ConfiguracaoEmitente;
        var temConfig = config is not null
            && !string.IsNullOrWhiteSpace(config.Cnpj)
            && !string.IsNullOrWhiteSpace(config.CertificadoPath)
            && !string.IsNullOrWhiteSpace(config.CertificadoSenha);

        if (!temCnpj && !temConfig)
        {
            ctx.AddFailure("emitenteCnpj", "Informe emitenteCnpj (cadastro em /api/emitentes) ou configuracaoEmitente completo.");
            return;
        }

        if (temCnpj && config is not null && !string.IsNullOrWhiteSpace(config.Cnpj))
        {
            var a = Regex.Replace(source.EmitenteCnpj!, @"\D", "");
            var b = Regex.Replace(config.Cnpj, @"\D", "");
            if (a != b)
                ctx.AddFailure("emitenteCnpj", "emitenteCnpj deve ser igual a configuracaoEmitente.cnpj quando ambos forem informados.");
        }
    }
}
