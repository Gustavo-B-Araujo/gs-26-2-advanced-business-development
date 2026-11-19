using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using BoletimPlus.Api.Seguranca;
using Microsoft.Extensions.Caching.Distributed;

namespace BoletimPlus.Api.Infra;

/// <summary>
/// Idempotência de POST pelo header Idempotency-Key (padrão do draft IETF "The Idempotency-Key
/// HTTP Header Field"). A primeira resposta de sucesso fica guardada por 24 h, por usuário e rota:
/// <list type="bullet">
/// <item>mesma chave e mesmo corpo: devolve a resposta guardada, sem executar de novo
/// (header Idempotent-Replayed: true);</item>
/// <item>mesma chave e corpo diferente: 422;</item>
/// <item>mesma chave com a primeira ainda em andamento: 409.</item>
/// </list>
/// O armazenamento é o IDistributedCache: em memória aqui, troca por Redis sem mudar o filtro.
/// </summary>
public sealed class IdempotenciaFilter(IDistributedCache cache, ILogger<IdempotenciaFilter> logger) : IEndpointFilter
{
    public const string Header = "Idempotency-Key";
    public const string HeaderRepeticao = "Idempotent-Replayed";
    public const int ChaveMax = 255;
    private static readonly TimeSpan Validade = TimeSpan.FromHours(24);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> EmAndamento = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
    {
        var http = contexto.HttpContext;
        var chave = http.Request.Headers[Header].ToString();
        if (string.IsNullOrWhiteSpace(chave) || chave.Length > ChaveMax)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key obrigatória",
                detail: $"Envie o header {Header} (até {ChaveMax} caracteres), um valor novo por operação.");
        }

        var usuario = http.User.FindFirst(ClaimsDoBoletim.UsuarioCod)?.Value ?? "anonimo";
        var chaveCache = $"idem:{usuario}:{http.Request.Method}:{http.Request.Path}:{chave}";
        var impressao = ImpressaoDoCorpo(contexto);

        var trava = EmAndamento.GetOrAdd(chaveCache, _ => new SemaphoreSlim(1, 1));
        if (!await trava.WaitAsync(TimeSpan.Zero))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Requisição em andamento",
                detail: "Uma requisição com esta Idempotency-Key ainda está sendo processada.");
        }

        try
        {
            var guardada = await cache.GetStringAsync(chaveCache, http.RequestAborted);
            if (guardada is not null)
            {
                var anterior = JsonSerializer.Deserialize<RespostaGuardada>(guardada, Json)!;
                if (anterior.Impressao != impressao)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        title: "Idempotency-Key reutilizada",
                        detail: "Esta Idempotency-Key já foi usada com outro corpo. Gere uma chave nova.");
                }
                logger.LogInformation("Requisição repetida: devolvendo a resposta guardada de {Chave}", chaveCache);
                http.Response.Headers[HeaderRepeticao] = "true";
                return Results.Json(anterior.Corpo, Json, statusCode: anterior.Status);
            }

            var resultado = await proximo(contexto);
            if (resultado is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 } sucesso)
            {
                var corpo = resultado is IValueHttpResult valor
                    ? JsonSerializer.SerializeToElement(valor.Value, Json)
                    : (JsonElement?)null;
                var nova = new RespostaGuardada(sucesso.StatusCode!.Value, corpo, impressao);
                await cache.SetStringAsync(
                    chaveCache,
                    JsonSerializer.Serialize(nova, Json),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Validade },
                    http.RequestAborted);
            }
            return resultado;
        }
        finally
        {
            trava.Release();
        }
    }

    /// <summary>SHA-256 dos argumentos do endpoint que são contratos (corpo e rota), sem os serviços.</summary>
    private static string ImpressaoDoCorpo(EndpointFilterInvocationContext contexto)
    {
        var dados = contexto.Arguments
            .Where(a => a is null || a is int || a.GetType().Namespace == typeof(Contratos.CodResponse).Namespace)
            .ToArray();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dados, Json)));
        return Convert.ToHexString(bytes);
    }

    private sealed record RespostaGuardada(int Status, JsonElement? Corpo, string Impressao);
}
