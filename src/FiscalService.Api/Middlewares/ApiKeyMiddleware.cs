namespace FiscalService.Api.Middlewares;

public sealed class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Permite health check sem autenticação
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            _logger.LogWarning("Requisição sem header {Header} de {IP}", ApiKeyHeaderName, context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                sucesso = false,
                erro = new
                {
                    tipo = "NaoAutorizado",
                    mensagem = $"Header '{ApiKeyHeaderName}' ausente.",
                    timestamp = DateTime.UtcNow
                }
            });
            return;
        }

        var configuredKey = _configuration["ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey) || !string.Equals(extractedApiKey, configuredKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("API Key inválida recebida de {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                sucesso = false,
                erro = new
                {
                    tipo = "NaoAutorizado",
                    mensagem = "API Key inválida.",
                    timestamp = DateTime.UtcNow
                }
            });
            return;
        }

        await _next(context);
    }
}
