using FiscalService.Api.Models.Requests;
using FluentValidation;

namespace FiscalService.Api.Validation;

public sealed class NFSeCancelarRequestValidator : AbstractValidator<NFSeCancelarRequest>
{
    private static readonly HashSet<string> MotivosValidos =
    [
        "ErroEmissao", "ServicoNaoPrestado", "Outros"
    ];

    public NFSeCancelarRequestValidator(IValidator<ConfiguracaoEmitenteRequest> emitenteValidator)
    {
        RuleFor(x => x).Custom((req, ctx) => EmitenteConfigSourceValidator.ValidarEmitenteOuConfig(ctx, req));
        RuleFor(x => x.ConfiguracaoEmitente!).SetValidator(emitenteValidator)
            .When(x => x.ConfiguracaoEmitente is not null);

        RuleFor(x => x.ChaveAcesso)
            .NotEmpty()
            .Length(50)
            .Matches(@"^\d{50}$")
            .WithMessage("Chave de acesso NFS-e Nacional deve ter 50 dígitos.");

        RuleFor(x => x.DescricaoMotivo).NotEmpty().MinimumLength(15);

        RuleFor(x => x.CodigoMotivo)
            .Must(m => MotivosValidos.Contains(m))
            .WithMessage($"codigoMotivo deve ser um de: {string.Join(", ", MotivosValidos)}.");
    }
}
