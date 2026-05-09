using FiscalService.Api.Models.Requests;
using FluentValidation;

namespace FiscalService.Api.Validation;

public sealed class NFeEmitirRequestValidator : AbstractValidator<NFeEmitirRequest>
{
    public NFeEmitirRequestValidator(
        IValidator<ConfiguracaoEmitenteRequest> emitenteValidator,
        IValidator<ItemNFeRequest> itemValidator)
    {
        RuleFor(x => x.ConfiguracaoEmitente).SetValidator(emitenteValidator);

        RuleFor(x => x.NumeroNota).GreaterThan(0);

        RuleFor(x => x.Serie)
            .NotEmpty()
            .Must(s => int.TryParse(s, out var n) && n is >= 1 and <= 999)
            .WithMessage("Série deve ser um número entre 1 e 999.");

        RuleFor(x => x.Finalidade).InclusiveBetween(1, 4);
        RuleFor(x => x.TipoOperacao).InclusiveBetween(0, 1);
        RuleFor(x => x.IndicadorDestinatario).InclusiveBetween(1, 3);
        RuleFor(x => x.ModalidadeFrete).InclusiveBetween(0, 9);

        RuleFor(x => x.Destinatario).NotNull();
        RuleFor(x => x.Itens).NotEmpty();
        RuleForEach(x => x.Itens).SetValidator(itemValidator);
    }
}
