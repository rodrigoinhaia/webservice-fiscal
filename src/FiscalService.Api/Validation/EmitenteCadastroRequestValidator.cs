using System.Text.RegularExpressions;
using FiscalService.Api.Models.Requests;
using FluentValidation;

namespace FiscalService.Api.Validation;

public sealed class EmitenteCadastroRequestValidator : AbstractValidator<EmitenteCadastroRequest>
{
    public EmitenteCadastroRequestValidator()
    {
        RuleFor(x => x.Cnpj)
            .NotEmpty()
            .Must(c => Regex.Replace(c, @"\D", "").Length == 14)
            .WithMessage("CNPJ deve conter 14 dígitos.");

        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Uf).NotEmpty().Length(2);
        RuleFor(x => x.Crt).InclusiveBetween(1, 3);
        RuleFor(x => x.Ambiente).Must(a => a is "Homologacao" or "Producao");
        RuleFor(x => x.CertificadoPath).NotEmpty();
        RuleFor(x => x.CertificadoSenha).NotEmpty();
    }
}
