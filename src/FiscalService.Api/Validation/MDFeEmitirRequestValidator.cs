using FiscalService.Api.Models.Requests;
using FluentValidation;

namespace FiscalService.Api.Validation;

public sealed class MDFeEmitirRequestValidator : AbstractValidator<MDFeEmitirRequest>
{
    public MDFeEmitirRequestValidator(IValidator<ConfiguracaoEmitenteRequest> emitenteValidator)
    {
        RuleFor(x => x.ConfiguracaoEmitente).SetValidator(emitenteValidator);

        RuleFor(x => x.NumeroNota).GreaterThan(0);

        RuleFor(x => x.Serie)
            .NotEmpty()
            .Must(s => int.TryParse(s, out var n) && n is >= 1 and <= 999)
            .WithMessage("Série deve ser um número entre 1 e 999.");

        RuleFor(x => x.Modal)
            .NotEmpty()
            .Must(m => m is "01" or "02" or "03" or "04")
            .WithMessage("Modal deve ser 01, 02, 03 ou 04.");

        RuleFor(x => x.UfInicio).NotEmpty().Length(2).Matches("^[A-Za-z]{2}$");
        RuleFor(x => x.UfFim).NotEmpty().Length(2).Matches("^[A-Za-z]{2}$");

        RuleFor(x => x.MunicipiosCarregamento).NotEmpty();
        RuleFor(x => x.Documentos).NotEmpty();
    }
}
