using System.Text.RegularExpressions;
using FiscalService.Api.Models.Requests;
using FluentValidation;

namespace FiscalService.Api.Validation;

public sealed class ConfiguracaoEmitenteRequestValidator : AbstractValidator<ConfiguracaoEmitenteRequest>
{
    public ConfiguracaoEmitenteRequestValidator()
    {
        RuleFor(x => x.Cnpj)
            .NotEmpty()
            .Must(c => Digitos(c).Length == 14)
            .WithMessage("CNPJ deve conter 14 dígitos.");

        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(120);

        RuleFor(x => x.Uf)
            .NotEmpty()
            .Length(2)
            .Matches("^[A-Za-z]{2}$")
            .WithMessage("UF deve ter 2 letras.");

        RuleFor(x => x.Ambiente)
            .Must(a => a is "Homologacao" or "Producao")
            .WithMessage("Ambiente deve ser Homologacao ou Producao.");

        RuleFor(x => x.Crt).InclusiveBetween(1, 3);

        RuleFor(x => x.CertificadoPath).NotEmpty();
        RuleFor(x => x.CertificadoSenha).NotEmpty();
    }

    private static string Digitos(string? valor) => Regex.Replace(valor ?? "", @"\D", "");
}
