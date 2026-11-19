using System.Globalization;
using System.Threading.RateLimiting;
using BoletimPlus.Api.Seguranca;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BoletimPlus.Api.Infra;

/// <summary>
/// Rate limiting do ASP.NET Core (Microsoft.AspNetCore.RateLimiting):
/// <list type="bullet">
/// <item>global: janela fixa por cliente (usuário do token, ou IP sem token);</item>
/// <item>política "escrita": janela deslizante mais curta, nos POST de cadastro e criação.</item>
/// </list>
/// Passou do limite: 429 com o header Retry-After em segundos (o app mostra "Tente de novo em N s").
/// </summary>
public static class LimiteDeRequisicoes
{
    public const string PoliticaEscrita = "escrita";

    public sealed class Opcoes
    {
        public int LimitePorJanela { get; set; } = 100;
        public int JanelaSegundos { get; set; } = 60;
        public int LimiteEscrita { get; set; } = 20;
        public int JanelaEscritaSegundos { get; set; } = 60;
    }

    public static IServiceCollection AddLimiteDeRequisicoes(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<Opcoes>().Bind(config.GetSection("RateLimit"));
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<Opcoes>>((limitador, lidas) =>
        {
            var opcoes = lidas.Value;
            limitador.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limitador.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter(Cliente(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = opcoes.LimitePorJanela,
                    Window = TimeSpan.FromSeconds(opcoes.JanelaSegundos),
                    QueueLimit = 0,
                }));

            limitador.AddPolicy(PoliticaEscrita, http =>
                RateLimitPartition.GetSlidingWindowLimiter(Cliente(http), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = opcoes.LimiteEscrita,
                    Window = TimeSpan.FromSeconds(opcoes.JanelaEscritaSegundos),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                }));

            limitador.OnRejected = async (contexto, cancelamento) =>
            {
                var segundos = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera)
                    ? (int)Math.Ceiling(espera.TotalSeconds)
                    : opcoes.JanelaSegundos;
                var resposta = contexto.HttpContext.Response;
                resposta.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
                await Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Muitas requisições",
                        detail: $"Limite de requisições atingido. Tente de novo em {segundos} s.")
                    .ExecuteAsync(contexto.HttpContext);
            };
        });
        return services;
    }

    /// <summary>Partição do limite: o usuário logado; sem token, o IP.</summary>
    private static string Cliente(HttpContext http)
    {
        var sub = http.User.FindFirst(ClaimsDoBoletim.UsuarioCod)?.Value
                  ?? http.User.FindFirst("sub")?.Value;
        return sub is not null ? $"usuario:{sub}" : $"ip:{http.Connection.RemoteIpAddress}";
    }
}
