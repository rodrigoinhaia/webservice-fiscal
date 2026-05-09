using FiscalService.Api.Models.Requests;
using FluentValidation;

namespace FiscalService.Api.Validation;

public sealed class ItemNFeRequestValidator : AbstractValidator<ItemNFeRequest>
{
    public ItemNFeRequestValidator()
    {
        RuleFor(x => x.CodigoProduto).NotEmpty().MaximumLength(60);
        RuleFor(x => x.DescricaoProduto).NotEmpty().MaximumLength(120);
        RuleFor(x => x.UnidadeComercial).NotEmpty().MaximumLength(6);

        RuleFor(x => x.QuantidadeComercial).GreaterThan(0);
        RuleFor(x => x.ValorUnitarioComercial).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValorTotalBruto).GreaterThanOrEqualTo(0);

        RuleFor(x => x.Cfop)
            .Must(c => string.IsNullOrEmpty(c) || (c.Length == 4 && int.TryParse(c, out _)))
            .WithMessage("CFOP deve ter 4 dígitos numéricos.");

        RuleFor(x => x.CstIcms)
            .Must(c => string.IsNullOrWhiteSpace(c) || (c.Trim().Length is >= 1 and <= 2 && int.TryParse(c.Trim(), out _)))
            .WithMessage("CST ICMS deve ser numérico (até 2 dígitos).");

        RuleFor(x => x.CsosnIcms)
            .Must(c => string.IsNullOrWhiteSpace(c) || (c.Trim().Length is >= 3 and <= 3 && int.TryParse(c.Trim(), out _)))
            .WithMessage("CSOSN deve ter 3 dígitos numéricos.");
    }
}
