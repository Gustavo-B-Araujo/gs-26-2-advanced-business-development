using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using BoletimPlus.Api.Dados;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BoletimPlus.Api.Infra;

/// <summary>
/// Health checks:
/// <list type="bullet">
/// <item>/health/live: a API responde (liveness, sem dependências);</item>
/// <item>/health: API e banco Oracle (prontidão).</item>
/// </list>
/// A API atende o contrato sozinha (login, cadastro e boletim inclusos): o backend Java não é
/// dependência e não entra no health.
/// </summary>
public static class Saude
{
    public const string TagPronto = "pronto";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static IServiceCollection AddSaude(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("api", () => HealthCheckResult.Healthy("A API responde."), tags: ["live", TagPronto])
            .AddDbContextCheck<BoletimDbContext>("oracle", HealthStatus.Unhealthy, tags: [TagPronto]);
        return services;
    }

    public static IEndpointRouteBuilder MapSaude(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains(TagPronto),
            ResponseWriter = EscreverJson,
        }).DisableRateLimiting().WithTags("Saúde");

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("live"),
            ResponseWriter = EscreverJson,
        }).DisableRateLimiting().WithTags("Saúde");

        return app;
    }

    private static Task EscreverJson(HttpContext http, HealthReport relatorio)
    {
        http.Response.ContentType = "application/json; charset=utf-8";
        var corpo = new
        {
            status = relatorio.Status.ToString(),
            duracaoMs = Math.Round(relatorio.TotalDuration.TotalMilliseconds, 1),
            checagens = relatorio.Entries.Select(e => new
            {
                nome = e.Key,
                status = e.Value.Status.ToString(),
                descricao = e.Value.Description ?? e.Value.Exception?.Message ?? (e.Value.Status == HealthStatus.Healthy ? "OK" : "Sem conexão."),
                duracaoMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
            }),
        };
        return http.Response.WriteAsync(JsonSerializer.Serialize(corpo, Json));
    }
}
