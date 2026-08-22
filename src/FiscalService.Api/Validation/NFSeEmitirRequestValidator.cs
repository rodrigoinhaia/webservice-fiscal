using FiscalService.Api.Models.Requests;
using FluentValidation;

namespace FiscalService.Api.Validation;

public sealed class NFSeEmitirRequestValidator : AbstractValidator<NFSeEmitirRequest>
{
    public NFSeEmitirRequestValidator(IValidator<ConfiguracaoEmitenteRequest> emitenteValidator)
    {
        RuleFor(x => x).Custom((req, ctx) => EmitenteConfigSourceValidator.ValidarEmitenteOuConfig(ctx, req));
        RuleFor(x => x.ConfiguracaoEmitente!).SetValidator(emitenteValidator)
            .When(x => x.ConfiguracaoEmitente is not null);

        RuleFor(x => x.Serie)
            .NotEmpty()
            .MaximumLength(5);

        RuleFor(x => x.NumeroDps)
            .GreaterThan(0)
            .When(x => x.NumeroDps.HasValue);

        RuleFor(x => x.Competencia).NotEmpty();

        RuleFor(x => x.Tomador).NotNull();
        RuleFor(x => x.Tomador.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Tomador.Endereco).NotNull();
        RuleFor(x => x.Tomador.Endereco.CodigoMunicipio)
            .NotEmpty()
            .When(x => x.Tomador?.Endereco is not null)
            .WithMessage("Tomador.endereco.codigoMunicipio é obrigatório.");

        RuleFor(x => x).Custom((req, ctx) =>
        {
            var cnpj = SomenteDigitos(req.Tomador.Cnpj);
            var cpf = SomenteDigitos(req.Tomador.Cpf);
            if (string.IsNullOrEmpty(cnpj) && string.IsNullOrEmpty(cpf))
                ctx.AddFailure(nameof(NFSeTomadorRequest), "Informe tomador.cnpj ou tomador.cpf.");
            if (!string.IsNullOrEmpty(cnpj) && cnpj.Length != 14)
                ctx.AddFailure(nameof(NFSeTomadorRequest.Cnpj), "CNPJ do tomador deve ter 14 dígitos.");
            if (!string.IsNullOrEmpty(cpf) && cpf.Length != 11)
                ctx.AddFailure(nameof(NFSeTomadorRequest.Cpf), "CPF do tomador deve ter 11 dígitos.");
        });

        RuleFor(x => x.Servico).NotNull();
        RuleFor(x => x.Servico.CodTributacaoNacional)
            .NotEmpty()
            .Matches(@"^\d{6}$")
            .WithMessage("codTributacaoNacional deve ter 6 dígitos (lista nacional).");
        RuleFor(x => x.Servico.Descricao).NotEmpty().MaximumLength(2000);

        RuleFor(x => x.Valores).NotNull();
        RuleFor(x => x.Valores.ValorServico).GreaterThan(0);
    }

    private static string SomenteDigitos(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());
}
