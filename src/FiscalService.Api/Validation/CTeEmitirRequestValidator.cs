using FiscalService.Api.Models.Requests;
using FluentValidation;

namespace FiscalService.Api.Validation;

public sealed class CTeEmitirRequestValidator : AbstractValidator<CTeEmitirRequest>
{
    public CTeEmitirRequestValidator(IValidator<ConfiguracaoEmitenteRequest> emitenteValidator)
    {
        RuleFor(x => x.ConfiguracaoEmitente).SetValidator(emitenteValidator);

        RuleFor(x => x.NumeroNota).GreaterThan(0);

        RuleFor(x => x.Serie)
            .NotEmpty()
            .Must(s => int.TryParse(s, out var n) && n is >= 1 and <= 999)
            .WithMessage("Série deve ser um número entre 1 e 999.");

        RuleFor(x => x.Cfop)
            .NotEmpty()
            .Must(c => c.Length == 4 && int.TryParse(c, out _))
            .WithMessage("CFOP deve ter 4 dígitos numéricos.");

        RuleFor(x => x.Modal)
            .NotEmpty()
            .Must(m => int.TryParse(m, out var v) && v is >= 1 and <= 6)
            .WithMessage("Modal deve ser 01 a 06.");

        RuleFor(x => x.Remetente).NotNull();
        RuleFor(x => x.Destinatario).NotNull();
        RuleFor(x => x.Tomador).NotNull();

        RuleFor(x => x.ValorTotalServico).GreaterThanOrEqualTo(0);
    }
}
