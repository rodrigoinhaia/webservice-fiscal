using FiscalService.Api.Models.Requests;
using FiscalService.Api.Services.Fiscal;
using FluentValidation;

namespace FiscalService.Api.Validation;

public sealed class NFCeEmitirRequestValidator : AbstractValidator<NFCeEmitirRequest>
{
    public NFCeEmitirRequestValidator(
        IValidator<ConfiguracaoEmitenteRequest> emitenteValidator,
        IValidator<ItemNFeRequest> itemValidator)
    {
        RuleFor(x => x.ConfiguracaoEmitente).SetValidator(emitenteValidator);

        RuleFor(x => x.NumeroNota).GreaterThan(0);

        RuleFor(x => x.Serie)
            .NotEmpty()
            .Must(s => int.TryParse(s, out var n) && n is >= 1 and <= 999)
            .WithMessage("Série deve ser um número entre 1 e 999.");

        RuleFor(x => x.IdCsc).NotEmpty();
        RuleFor(x => x.Csc).NotEmpty();

        RuleFor(x => x.QrCodeVersao)
            .Must(v => string.IsNullOrWhiteSpace(v) || v.Trim() is "1" or "2" or "3")
            .WithMessage("qrCodeVersao deve ser 1, 2 ou 3.");

        RuleFor(x => x.Itens).NotEmpty();
        RuleForEach(x => x.Itens).SetValidator(itemValidator);

        RuleForEach(x => x.Itens).Custom((item, ctx) =>
        {
            var crt = ctx.InstanceToValidate.ConfiguracaoEmitente.Crt;
            if (!ImpostoTributacaoCatalog.ValidarItem(item, crt, out var msg))
                ctx.AddFailure(nameof(ItemNFeRequest), msg);
        });

        RuleFor(x => x.Pagamentos).NotEmpty();
    }
}
