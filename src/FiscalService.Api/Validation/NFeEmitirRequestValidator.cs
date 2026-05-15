using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services.Fiscal;
using FluentValidation;

namespace FiscalService.Api.Validation;

public sealed class NFeEmitirRequestValidator : AbstractValidator<NFeEmitirRequest>
{
    public NFeEmitirRequestValidator(
        IValidator<ConfiguracaoEmitenteRequest> emitenteValidator,
        IValidator<ItemNFeRequest> itemValidator)
    {
        RuleFor(x => x).Custom((req, ctx) => EmitenteConfigSourceValidator.ValidarEmitenteOuConfig(ctx, req));
        RuleFor(x => x.ConfiguracaoEmitente!).SetValidator(emitenteValidator)
            .When(x => x.ConfiguracaoEmitente is not null);

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

        RuleForEach(x => x.Itens).Custom((item, ctx) =>
        {
            var cfg = ctx.InstanceToValidate.ConfiguracaoEmitente;
            if (cfg is null) return;
            if (!ImpostoTributacaoCatalog.ValidarItem(item, cfg.Crt, out var msg))
                ctx.AddFailure(nameof(ItemNFeRequest), msg);
        });
    }
}
